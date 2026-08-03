# Deploying `parameters.yaml` from GitHub Actions

This document covers the workflow at
`.github/workflows/deploy-cloudformation-parameters.yml`, which deploys
`deploy/cloudformation/parameters.yaml` (the shared SSM parameters stack other
templates in `deploy/cloudformation` depend on) to AWS, and how to configure AWS
and GitHub so the workflow can authenticate without ever storing an AWS access
key anywhere.

A pre-existing bug was fixed as part of this change: every resource in
`parameters.yaml` was declared as `AWS:SSM:Parameter` (single colons), which is
not a valid CloudFormation type and would fail template validation. It has been
corrected to `AWS::SSM::Parameter`.

## How the workflow behaves

| Trigger | Job that runs | What it does |
|---|---|---|
| Pull request touching `parameters.yaml` | `plan` | Creates a CloudFormation change set, prints the resource-level diff to the job summary, deletes the change set. Never executes it. |
| Push to `main` touching `parameters.yaml`, or manual `workflow_dispatch` | `deploy` | Actually deploys the change, gated behind the `aws-production` GitHub Environment. |

Both jobs authenticate to AWS the same way: via OpenID Connect (OIDC), not
stored access keys.

## Why OIDC instead of access keys

The traditional approach - creating an IAM user, generating an access key pair,
and pasting it into `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` GitHub
secrets - has a specific weakness: that key pair is long-lived and portable. If
it ever leaks (a misconfigured log, a compromised third-party Action, a secret
accidentally committed), it remains valid until someone notices and manually
revokes it, and it works from anywhere, not just GitHub Actions.

OIDC federation removes the shared secret entirely:

1. GitHub's Actions runner mints a short-lived JSON Web Token (JWT) for the
   running job, signed by GitHub, asserting claims like which repo, branch, and
   (if used) which Environment triggered it.
2. `aws-actions/configure-aws-credentials` exchanges that JWT for temporary AWS
   credentials via `sts:AssumeRoleWithWebIdentity`, against an IAM role whose
   trust policy only accepts tokens matching specific claims (repo + Environment
   name, in this setup).
3. AWS never sees, and GitHub never stores, any long-lived AWS credential. The
   session AWS hands back expires within the hour and cannot be reused outside
   that job.

Net effect: there is no static credential to leak. A compromised workflow run
can only obtain a credential scoped to exactly what that IAM role allows, for
as long as that one job runs.

## Two-role split (why there are two IAM roles, not one)

`deploy/cloudformation/iam/github-actions-oidc-deploy-role.yaml` creates two
roles instead of one:

- **`gha-<repo>-cfn-deploy`** - the role GitHub Actions assumes directly via
  OIDC. It can only call CloudFormation APIs (create/update stacks, work with
  change sets) against the one stack name pattern, plus `iam:PassRole` for
  exactly one target role, restricted to when the receiving service is
  `cloudformation.amazonaws.com`. It cannot call `ssm:PutParameter` or anything
  else directly.
- **`<repo>-cfn-execution`** - assumed only by the CloudFormation *service*
  (never by GitHub Actions), passed via `--role-arn` on `aws cloudformation
  deploy`. This is the role that actually holds `ssm:PutParameter` /
  `ssm:DeleteParameter` / etc., scoped to the `Primary-*` parameter name prefix.

If the first role's temporary credentials were ever exposed (for example, via a
bug in a third-party Action the workflow depends on), the attacker could still
only drive CloudFormation deployments of this one stack - not call SSM, EC2, or
any other service directly, since the deploy role itself has no such
permissions. This mirrors the same reasoning AWS documents for
[CloudFormation service roles](https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-iam-servicerole.html).

## One-time AWS setup

These steps use an administrator identity, run once, out-of-band from GitHub
Actions (the workflow cannot bootstrap its own IAM permissions - that would be
circular).

1. Deploy the IAM template:

   ```bash
   aws cloudformation deploy \
     --stack-name projecta-github-actions-iam \
     --template-file deploy/cloudformation/iam/github-actions-oidc-deploy-role.yaml \
     --capabilities CAPABILITY_NAMED_IAM \
     --parameter-overrides \
       GitHubOrg=<your-github-org-or-username> \
       GitHubRepo=<your-repo-name> \
     --region ap-southeast-2
   ```

   If this AWS account already has a `token.actions.githubusercontent.com` OIDC
   provider from a previous project (an account can only have one), add
   `CreateOidcProvider=false ExistingOidcProviderArn=<its arn>` to
   `--parameter-overrides` instead.

2. Read the two role ARNs from the stack outputs:

   ```bash
   aws cloudformation describe-stacks \
     --stack-name projecta-github-actions-iam \
     --query 'Stacks[0].Outputs' \
     --region ap-southeast-2
   ```

   You'll need `GitHubActionsDeployRoleArn` and `CfnExecutionRoleArn` for the
   GitHub-side configuration below.

## GitHub-side configuration

### 1. Create two Environments

In the repository: **Settings → Environments → New environment**.

- **`aws-plan`** - used by the read-only `plan` job. No protection rules are
  required (it can't mutate anything), but you may still want to restrict which
  branches/PRs can use it.
- **`aws-production`** - used by the `deploy` job. Configure:
  - **Required reviewers**: at least one person who is not the PR author must
    approve before the job runs. This is what actually turns "push to main" into
    "push to main, then a human confirms the deploy."
  - **Deployment branches**: restrict to `main` only, so the environment (and
    the AWS credentials it grants access to) can never be targeted from a
    feature branch or a workflow_dispatch off some other ref.

Because the IAM trust policy above restricts the OIDC `sub` claim to
`repo:<org>/<repo>:environment:aws-plan` and
`repo:<org>/<repo>:environment:aws-production`, only workflow runs that
reference one of these two Environments - and, for `aws-production`, that have
passed its required-reviewer gate - can ever obtain a token AWS will accept.
Any other workflow, branch, or fork trying to assume the role is rejected by
AWS before this repository's own logic is even involved.

### 2. Add the role ARNs as Environment variables

On each Environment (`aws-plan` and `aws-production`), add:

| Name | Value |
|---|---|
| `AWS_DEPLOY_ROLE_ARN` | the `GitHubActionsDeployRoleArn` output |
| `AWS_CFN_EXECUTION_ROLE_ARN` | the `CfnExecutionRoleArn` output |

Use repository/environment **Variables**, not **Secrets**, for these. An IAM
role ARN is not a credential by itself - nothing can be done with it without
also satisfying that role's trust policy - so it doesn't need secret-masking,
and using a variable makes the change set preview in `$GITHUB_STEP_SUMMARY`
easier to read if it's ever printed for debugging. The actual sensitive
material (the temporary session credentials) is generated fresh by
`configure-aws-credentials` at runtime and is automatically masked in logs by
the Actions runner.

### 3. Confirm branch protection covers the workflow file itself

A workflow file is code that runs with access to your AWS account - a
malicious edit to `.github/workflows/deploy-cloudformation-parameters.yml` is
just as dangerous as a malicious edit to the IAM policy itself. Make sure
**Settings → Branches → main** requires pull request review before merging, so
changes to this file (and to the CloudFormation templates it deploys) go
through the same review as any other change, and that GitHub Actions is
configured to require approval for workflow runs from first-time contributors
(**Settings → Actions → General → Fork pull request workflows**).

## Additional hardening already applied in the workflow

- **Minimal `GITHUB_TOKEN` permissions**: `permissions: contents: read, id-token:
  write` at the top of the workflow. No `write` access to issues, PRs, packages,
  or anything else is granted, and no permissions are granted to the default
  `GITHUB_TOKEN` beyond what's listed.
- **Pinned third-party Actions**: `aws-actions/configure-aws-credentials` is
  pinned to a full commit SHA (`@e7f100c...` / v6.2.0), not a mutable tag like
  `@v6` or `@latest`, so a compromised or force-pushed tag on that Action's repo
  can't silently change what runs in this workflow. `actions/checkout` is
  GitHub's own first-party Action; if your organisation's policy requires SHA
  pinning for first-party Actions too, resolve and pin it the same way:
  ```bash
  git ls-remote --tags https://github.com/actions/checkout v5.0.0
  ```
  and use `actions/checkout@<sha> # v5.0.0`. Consider adding Dependabot
  (`.github/dependabot.yml`, `package-ecosystem: "github-actions"`) so pinned
  SHAs get version-bump PRs instead of silently going stale.
- **Concurrency guard**: `concurrency.group` prevents two deploys of the same
  stack racing each other; `cancel-in-progress: false` avoids ever cancelling a
  CloudFormation operation mid-flight (which can leave a stack in a
  `ROLLBACK_FAILED` state requiring manual console intervention).
- **PRs can only plan, never deploy**: the `deploy` job's `if:` condition
  excludes `pull_request` entirely, so no PR - including one from a fork - can
  reach the job that holds `aws-production` credentials.
- **Least-privilege, resource-scoped IAM**: both roles in
  `github-actions-oidc-deploy-role.yaml` are scoped by resource ARN (stack name
  pattern, SSM parameter name prefix) rather than `Resource: "*"`. If you add
  more SSM parameters under a different name prefix later, update
  `SsmParameterPrefix` accordingly rather than widening it to `*`.

## Testing the setup

1. Open a PR that touches `deploy/cloudformation/parameters.yaml` (even a
   comment-only change) and confirm the `plan` job runs, authenticates
   successfully, and posts a change set preview to the job summary.
2. Merge to `main` and confirm the `deploy` job appears in **Actions**, waits
   for the `aws-production` required reviewer, and - once approved - completes
   with `UPDATE_COMPLETE` (or `CREATE_COMPLETE` on first run).
3. Confirm in the AWS Console (SSM → Parameter Store, `ap-southeast-2`) that the
   `Primary-Vpc-*` parameters exist with the expected values.
