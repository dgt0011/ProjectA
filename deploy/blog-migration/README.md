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

Run [`migrate-blogifier-data.sql`](migrate-blogifier-data.sql) in DBeaver against
the **ProjectA** database. Blogifier's tables (`"Blogs"`, `"Posts"`, `"Categories"`,
`"PostCategories"`, `"Authors"`) already live in that same database, so the script
reads them directly. They are only read, never changed or dropped.

1. Open the script in a SQL editor connected to the ProjectA database.
2. At the top of the `DO` block, set `v_author_username` to the ProjectA username.
   If the Blogifier tables aren't in the `public` schema, also set `v_source_schema`.
3. Run the whole file with **Execute SQL Script** (Alt+X).
4. Open the **Output** panel (Show server output, Ctrl+Shift+O) to see the summary.
   The last query opens as a result tab listing the referenced `data/...` files.

The migration is a single `DO` block, so it runs in one transaction whatever the
auto-commit setting is. If anything fails, nothing is changed. The script is plain
SQL, so it also runs with `psql -f`.

The script:

- Can be re-run safely. Posts are matched by slug and categories by
  case-insensitive title, so nothing is duplicated.
- Copies drafts as drafts.
- If two old posts share a slug, keeps the oldest (the one readers saw) and lists
  the others at the end instead of failing.
- Replaces commas in category titles with " - " ("Cloud, DevOps" becomes
  "Cloud - DevOps"), because ProjectA's editor takes categories as a comma-separated list.
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
