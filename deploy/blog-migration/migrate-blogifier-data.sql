-- =============================================================================================
-- One-off copy of the existing Blogifier blog (darrentuer.net) into ProjectA's blog_* tables.
--
-- Run against the *ProjectA* database, after ProjectA.Api has started at least once (so DbUp
-- has applied script014-CreateTables-Blog.sql). Blogifier's tables ("Blogs", "Posts",
-- "Categories", "PostCategories", "Authors") must be in that same database - they are read
-- directly, no dblink or second connection:
--
--   psql "<ProjectA connection string>" \
--     -v author_username="<ProjectA username that becomes the author of every migrated post>" \
--     -f migrate-blogifier-data.sql
--
-- Optional: -v source_schema=<schema> if the Blogifier tables aren't in "public".
--
-- Safe to re-run: categories are matched by (case-insensitive) title and posts by slug, so
-- anything already copied is skipped rather than duplicated. Blog settings and the author's
-- profile fields are overwritten from Blogifier each run (profile fields only where empty).
--
-- Not copied (dropped features): Subscribers, Newsletters, MailSettings, and Blogifier's own
-- Authors logins (posts are re-owned by the ProjectA user given above).
-- =============================================================================================

\set ON_ERROR_STOP on

\if :{?author_username}
\else
    \echo 'Pass -v author_username=<ProjectA username> (see the header of this script).'
    \quit
\endif

\if :{?source_schema}
\else
    \set source_schema public
\endif

BEGIN;

SELECT set_config('blogmig.author_username', :'author_username', true);
SELECT set_config('blogmig.source_schema', :'source_schema', true);

DO $$
BEGIN
    IF to_regclass('public.blog_posts') IS NULL THEN
        RAISE EXCEPTION 'blog_posts does not exist - start ProjectA.Api once so DbUp applies script014 first.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM users WHERE LOWER(username) = LOWER(current_setting('blogmig.author_username'))) THEN
        RAISE EXCEPTION 'No ProjectA user named "%" - create it (Users page) or pass a different author_username.',
            current_setting('blogmig.author_username');
    END IF;
    IF to_regclass(format('%I."Posts"', current_setting('blogmig.source_schema'))) IS NULL THEN
        RAISE EXCEPTION 'No Blogifier "Posts" table in schema "%" of this database - pass -v source_schema=<schema>.',
            current_setting('blogmig.source_schema');
    END IF;
END $$;

-- Blogifier's tables are found through the search_path (this transaction only), so the queries
-- below can name them unqualified whichever schema they live in.
SELECT set_config('search_path', format('%I, public', :'source_schema'), true);

-- ---------------------------------------------------------------------------------------------
-- 1. Snapshot the Blogifier tables into temp tables (later steps clean them up in place).
--    Blogifier stored "timestamp without time zone" values in UTC.
-- ---------------------------------------------------------------------------------------------
CREATE TEMP TABLE src_blog ON COMMIT DROP AS
SELECT "Title"::varchar AS title, "Description"::varchar AS description,
       "IncludeFeatured"::bool AS include_featured, "ItemsPerPage"::int AS items_per_page,
       "Cover"::varchar AS cover, "Logo"::varchar AS logo,
       "HeaderScript"::varchar AS header_script, "FooterScript"::varchar AS footer_script
FROM "Blogs" ORDER BY "Id" LIMIT 1;

CREATE TEMP TABLE src_authors ON COMMIT DROP AS
SELECT a."Id"::int AS id, a."DisplayName"::varchar AS display_name, a."Bio"::varchar AS bio,
       a."Avatar"::varchar AS avatar,
       (SELECT COUNT(*) FROM "Posts" p WHERE p."AuthorId" = a."Id")::int AS post_count
FROM "Authors" a;

CREATE TEMP TABLE src_categories ON COMMIT DROP AS
SELECT "Id"::int AS id, "Content"::varchar AS title, "Description"::varchar AS description,
       "DateCreated"::timestamp AS date_created, "DateUpdated"::timestamp AS date_updated
FROM "Categories";

CREATE TEMP TABLE src_posts ON COMMIT DROP AS
SELECT "Id"::int AS id, "PostType"::int AS post_type, "Title"::varchar AS title,
       "Slug"::varchar AS slug, "Description"::varchar AS description, "Content"::text AS content,
       "Cover"::varchar AS cover, "PostViews"::int AS post_views, "IsFeatured"::bool AS is_featured,
       "Published"::timestamp AS published, "DateCreated"::timestamp AS date_created,
       "DateUpdated"::timestamp AS date_updated
FROM "Posts";

CREATE TEMP TABLE src_post_categories ON COMMIT DROP AS
SELECT "PostId"::int AS post_id, "CategoryId"::int AS category_id FROM "PostCategories";

-- ---------------------------------------------------------------------------------------------
-- 2. Site settings (single row) - theme/analytics display columns are dropped.
-- ---------------------------------------------------------------------------------------------
UPDATE blog_settings s
SET title            = COALESCE(NULLIF(b.title, ''), s.title),
    description      = b.description,
    include_featured = b.include_featured,
    items_per_page   = CASE WHEN b.items_per_page BETWEEN 1 AND 100 THEN b.items_per_page ELSE 10 END,
    cover            = NULLIF(b.cover, ''),
    logo             = NULLIF(b.logo, ''),
    header_script    = NULLIF(b.header_script, ''),
    footer_script    = NULLIF(b.footer_script, ''),
    date_modified    = now()
FROM src_blog b
WHERE s.id = (SELECT MIN(id) FROM blog_settings);

-- ---------------------------------------------------------------------------------------------
-- 3. Author profile: the Blogifier author with the most posts supplies display name/bio/avatar
--    for the ProjectA user, without overwriting anything already filled in on ProjectA.
-- ---------------------------------------------------------------------------------------------
UPDATE users u
SET display_name = COALESCE(u.display_name, NULLIF(a.display_name, '')),
    bio          = COALESCE(u.bio, NULLIF(a.bio, '')),
    avatar       = COALESCE(u.avatar, NULLIF(a.avatar, '')),
    date_modified = now()
FROM (SELECT * FROM src_authors ORDER BY post_count DESC, id LIMIT 1) a
WHERE LOWER(u.username) = LOWER(current_setting('blogmig.author_username'));

-- ---------------------------------------------------------------------------------------------
-- 4. Categories, matched case-insensitively by title. ProjectA doesn't allow commas in
--    category titles (the editor takes categories as a comma-separated list), so they become
--    " - " ("Cloud, DevOps" -> "Cloud - DevOps"); step 6 matches on the same cleaned title.
-- ---------------------------------------------------------------------------------------------
UPDATE src_categories SET title = LEFT(btrim(regexp_replace(title, '\s*,\s*', ' - ', 'g')), 120);

INSERT INTO blog_categories (title, description, date_created, date_modified)
SELECT DISTINCT ON (LOWER(c.title))
       c.title, LEFT(NULLIF(c.description, ''), 255),
       c.date_created AT TIME ZONE 'UTC', c.date_updated AT TIME ZONE 'UTC'
FROM src_categories c
WHERE NOT EXISTS (SELECT 1 FROM blog_categories t WHERE LOWER(t.title) = LOWER(c.title))
ORDER BY LOWER(c.title), c.id;

-- ---------------------------------------------------------------------------------------------
-- 5a. blog_posts.slug is unique, but Blogifier's "Slug" column wasn't. If two source posts share
--     a slug, keep the oldest (lowest Id - the one Blogifier's slug lookup returned, so the one
--     readers actually saw) instead of aborting the whole copy; the others are listed at the end.
-- ---------------------------------------------------------------------------------------------
CREATE TEMP TABLE src_posts_skipped ON COMMIT DROP AS
SELECT p.id, p.slug, p.title
FROM src_posts p
WHERE EXISTS (SELECT 1 FROM src_posts o WHERE o.slug = p.slug AND o.id < p.id);

DELETE FROM src_posts p USING src_posts_skipped s WHERE p.id = s.id;

-- ---------------------------------------------------------------------------------------------
-- 5. Posts and pages, matched by slug. Published <= year 1900 is Blogifier's draft marker
--    (DateTime.MinValue, or -infinity depending on the Npgsql version that wrote it).
-- ---------------------------------------------------------------------------------------------
INSERT INTO blog_posts (author_id, post_type, title, slug, description, content, cover,
                        post_views, is_featured, published_at, date_created, date_modified)
SELECT (SELECT id FROM users WHERE LOWER(username) = LOWER(current_setting('blogmig.author_username'))),
       CASE WHEN p.post_type = 2 THEN 2 ELSE 1 END,
       LEFT(p.title, 160),
       p.slug,
       LEFT(COALESCE(p.description, ''), 450),
       COALESCE(p.content, ''),
       NULLIF(p.cover, ''),
       COALESCE(p.post_views, 0),
       COALESCE(p.is_featured, false),
       CASE WHEN p.published > '1900-01-01' THEN p.published AT TIME ZONE 'UTC' END,
       CASE WHEN p.date_created > '1900-01-01' THEN p.date_created AT TIME ZONE 'UTC' ELSE now() END,
       CASE WHEN p.date_updated > '1900-01-01' THEN p.date_updated AT TIME ZONE 'UTC' END
FROM src_posts p
WHERE NOT EXISTS (SELECT 1 FROM blog_posts t WHERE t.slug = p.slug);

-- ---------------------------------------------------------------------------------------------
-- 6. Post <-> category links, resolved through slug and title.
-- ---------------------------------------------------------------------------------------------
INSERT INTO blog_post_categories (post_id, category_id)
SELECT DISTINCT tp.id, tc.id
FROM src_post_categories pc
JOIN src_posts sp ON sp.id = pc.post_id
JOIN src_categories sc ON sc.id = pc.category_id
JOIN blog_posts tp ON tp.slug = sp.slug
JOIN LATERAL (
    SELECT id FROM blog_categories t WHERE LOWER(t.title) = LOWER(sc.title) ORDER BY id LIMIT 1
) tc ON true
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------------------------------
-- 7. Summary.
-- ---------------------------------------------------------------------------------------------
SELECT 'source posts copied or already present' AS item, COUNT(*) AS count FROM src_posts
UNION ALL SELECT 'source posts skipped (duplicate slug)', COUNT(*) FROM src_posts_skipped
UNION ALL SELECT 'blog_posts now', COUNT(*) FROM blog_posts
UNION ALL SELECT 'source categories', COUNT(*) FROM src_categories
UNION ALL SELECT 'blog_categories now', COUNT(*) FROM blog_categories
UNION ALL SELECT 'blog_post_categories now', COUNT(*) FROM blog_post_categories;

-- Duplicate-slug posts that were not copied (empty when there were none).
SELECT id AS skipped_blogifier_post_id, slug, title FROM src_posts_skipped ORDER BY slug, id;

-- Every local image/file path the migrated content references - each must exist under the
-- blog storage folder (BlogStorage:RootPath) for the site to render completely. Absolute
-- http(s) URLs are not listed; they keep pointing wherever they already did.
SELECT DISTINCT path AS referenced_storage_path
FROM (
    SELECT regexp_replace(cover, '^/', '') AS path FROM blog_posts WHERE cover ~ '^/?data/'
    UNION
    SELECT regexp_replace((regexp_matches(content, '\(/?(data/[^)\s"]+)', 'g'))[1], '^/', '') FROM blog_posts
    UNION
    SELECT regexp_replace((regexp_matches(content, 'src="/?(data/[^"]+)"', 'g'))[1], '^/', '') FROM blog_posts
) refs
ORDER BY 1;

COMMIT;
