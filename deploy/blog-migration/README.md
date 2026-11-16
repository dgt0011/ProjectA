# Blogifier → ProjectA blog cutover

The public blog (www.darrentuer.net) moves from the Blogifier ECS service to
`ProjectA.Blog.Web` on the ProjectA EC2 instance. Its data moves from Blogifier's
RDS database into new `blog_*` tables in the ProjectA database, and admin moves
into ProjectA.Web (**Tools → Blog**, or the **Blog** nav link).

The source for everything below is Blogifier's `origin/dgt0011-master` branch. That's
the code actually deployed to ECS (Blogifier v2 + Postgres), not the v3 `master`.

## 1. Schema

Deploy ProjectA.Api (workflow 5). On startup DbUp applies
`script014-CreateTables-Blog.sql`, which creates `blog_settings`, `blog_categories`,
`blog_posts` and `blog_post_categories`, and adds `display_name`, `bio` and `avatar`
to `users`.

## 2. Author account

All migrated posts are attributed to one ProjectA user. Use an existing account or
create one on the Users page. Blogifier's own logins are not migrated.

## 3. Copy the data

Run [`migrate-blogifier-data.sql`](migrate-blogifier-data.sql) against the
**ProjectA** database from somewhere that can reach it. The ProjectA database server
itself must be able to reach the Blogifier database, because the script uses
`dblink`:

```sh
read -rs BLOGIFIER_PASSWORD && export BLOGIFIER_PASSWORD   # prompts; nothing in shell history
psql "host=<projecta-rds> dbname=<projecta-db> user=<master-user> sslmode=require" \
  -v source_host=<blogifier-rds> -v source_db=<blogifier-db> -v source_user=<user> \
  -v author_username="<projecta username>" \
  -f deploy/blog-migration/migrate-blogifier-data.sql
unset BLOGIFIER_PASSWORD
```

This needs psql 15 or later. The Blogifier password is read from the
`BLOGIFIER_PASSWORD` environment variable, so it never appears on the command line.
Inside the database it is held only in a temporary user mapping, which is dropped before
the script commits (or rolled back if the script fails). If RDS is set to log DDL
(`log_statement = ddl` or `all`), turn that off for the run, or the mapping statement
will be logged with the password.

The script:

- Can be re-run safely. Posts are matched by slug and categories by
  case-insensitive title, so nothing is duplicated.
- Copies drafts as drafts.
- If two old posts share a slug, keeps the oldest (the one readers saw) and lists
  the others at the end instead of failing.
- Replaces commas in category titles with " - " ("Cloud, DevOps" becomes
  "Cloud - DevOps"), because ProjectA's editor takes categories as a comma-separated list.
- Removes the `dblink` extension afterwards if it wasn't installed before the run.
- Does not copy subscribers, newsletters or mail settings.
- Ends by listing every `data/...` file path the migrated posts reference. Step 4
  needs these.

## 4. Copy the uploaded images

Blogifier served uploads from `wwwroot/data/...`. ProjectA serves the same files at
the same `/data/...` URLs from `/mnt/data/blog-storage` on EFS. ProjectA.Blog.Web
reads them from there, and ProjectA.Web writes new uploads there.

The images committed to the deployed branch can be extracted with:

```sh
git -C <Blogifier repo> archive origin/dgt0011-master src/Blogifier/wwwroot/data \
  | tar -x --strip-components=4 -C <local folder>
# then copy <local folder>/* to /mnt/data/blog-storage/ on the instance
```

Anything uploaded through the live admin after that commit exists only inside the
running ECS container. Compare against the path list printed in step 3, and copy any
missing files out of the container, e.g. with `aws ecs execute-command`, or by
downloading each `https://www.darrentuer.net/data/...` URL before the cutover.

## 5. Deploy the blog site

1. Run the ansible workflow (4). With `blog_site_enabled: true` it:
   - installs the `projecta-blog` systemd service on port 5240;
   - creates `/mnt/data/blog` and `/mnt/data/blog-storage`;
   - adds an HTTP nginx vhost for `www.darrentuer.net` and `darrentuer.net`.
2. Run **7. Deploy ProjectA.Blog.Web**. It also runs automatically on pushes that
   touch `ProjectA.Blog.Web/**`.
3. Check the site before switching DNS. Point a hosts-file entry for
   `www.darrentuer.net` at the instance, or `curl -H "Host: www.darrentuer.net"`
   from the instance itself.

## 6. Switch DNS, then enable TLS

1. Point the `darrentuer.net` and `www.darrentuer.net` records at the EC2 instance
   instead of the ECS load balancer.
2. Set `blog_tls_enabled: true` in `deploy/ansible/projects-darrentuerrnet.yml` and
   re-run the ansible workflow. certbot's HTTP-01 challenge only succeeds once DNS
   points here.
3. When everything checks out, retire the Blogifier ECS service and its database.

## URL compatibility

| URL | Behaviour |
|---|---|
| `/`, `/?page=N` | home page, same pagination |
| `/posts/{slug}` | posts |
| `/{slug}` | pages (post type "page"); a post requested this way redirects (301) to `/posts/{slug}` |
| `/categories/{name}` | category listing |
| `/search?term=` | search (the old search form POSTed to `/Home/Search`) |
| `/feed/rss`, `/sitemap` | unchanged |
| `/data/...` | uploaded files, unchanged |
