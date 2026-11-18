-- =============================================================================================
-- One-off copy of the existing Blogifier blog (darrentuer.net) into ProjectA's blog_* tables.
--
-- Written for DBeaver (plain SQL - no psql \commands, no DBeaver variables). Run it against the
-- *ProjectA* database, after ProjectA.Api has started at least once (so DbUp has applied
-- script014-CreateTables-Blog.sql). Blogifier's tables ("Blogs", "Posts", "Categories",
-- "PostCategories", "Authors") must be in that same database; they are only read, never changed.
--
-- 1. Edit the two settings at the top of the DO block below (author username, Blogifier schema).
-- 2. Execute the whole file as a script: SQL Editor > Execute SQL Script (Alt+X).
-- 3. The migration summary is printed as server output: open the "Output" panel (Show server
--    output, Ctrl+Shift+O) to see it. The final query opens as a result tab listing the
--    data/... files the posts reference.
--
-- The migration is a single DO block, so it runs as one statement in one transaction whatever
-- DBeaver's auto-commit setting is: if anything fails, nothing is changed.
--
-- Safe to re-run: categories are matched by (case-insensitive) title and posts by slug, so
-- anything already copied is skipped rather than duplicated. Blog settings are overwritten from
-- Blogifier each run; the author's profile fields are only filled where empty.
--
-- Not copied (dropped features): Subscribers, Newsletters, MailSettings, and Blogifier's own
-- Authors logins (posts are re-owned by the ProjectA user named below).
-- =============================================================================================

DO $migrate$
DECLARE
    -- ============================ SETTINGS - edit these two values ============================
    -- ProjectA username that becomes the author of every migrated post (must already exist).
    v_author_username text := 'CHANGE-ME';
    -- Schema holding Blogifier's tables in this database.
    v_source_schema   text := 'public';
    -- ===========================================================================================

    v_author_id bigint;
    v_count     bigint;
    r           record;
BEGIN
    IF v_author_username = 'CHANGE-ME' THEN
        RAISE EXCEPTION 'Set v_author_username at the top of the DO block first.';
    END IF;
    IF to_regclass('public.blog_posts') IS NULL THEN
        RAISE EXCEPTION 'blog_posts does not exist - start ProjectA.Api once so DbUp applies script014 first.';
    END IF;
    SELECT id INTO v_author_id FROM public.users WHERE LOWER(username) = LOWER(v_author_username);
    IF v_author_id IS NULL THEN
        RAISE EXCEPTION 'No ProjectA user named "%" - create it (Users page) or set a different v_author_username.',
            v_author_username;
    END IF;
    IF to_regclass(format('%I."Posts"', v_source_schema)) IS NULL THEN
        RAISE EXCEPTION 'No Blogifier "Posts" table in schema "%" of this database - set v_source_schema.',
            v_source_schema;
    END IF;

    -- Blogifier's tables are found through the search_path (this transaction only), so the
    -- queries below can name them unqualified whichever schema they live in.
    PERFORM set_config('search_path', format('%I, public', v_source_schema), true);

    -- -----------------------------------------------------------------------------------------
    -- 1. Snapshot the Blogifier tables into temp tables (later steps clean them up in place).
    --    Blogifier stored "timestamp without time zone" values in UTC. ON COMMIT DROP (and a
    --    rollback on failure) means they never outlive this run.
    -- -----------------------------------------------------------------------------------------
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

    -- -----------------------------------------------------------------------------------------
    -- 2. Site settings (single row) - theme/analytics display columns are dropped.
    -- -----------------------------------------------------------------------------------------
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

    -- -----------------------------------------------------------------------------------------
    -- 3. Author profile: the Blogifier author with the most posts supplies display name/bio/
    --    avatar for the ProjectA user, without overwriting anything already filled in on ProjectA.
    -- -----------------------------------------------------------------------------------------
    UPDATE users u
    SET display_name  = COALESCE(u.display_name, NULLIF(a.display_name, '')),
        bio           = COALESCE(u.bio, NULLIF(a.bio, '')),
        avatar        = COALESCE(u.avatar, NULLIF(a.avatar, '')),
        date_modified = now()
    FROM (SELECT * FROM src_authors ORDER BY post_count DESC, id LIMIT 1) a
    WHERE u.id = v_author_id;

    -- -----------------------------------------------------------------------------------------
    -- 4. Categories, matched case-insensitively by title. ProjectA doesn't allow commas in
    --    category titles (the editor takes categories as a comma-separated list), so they become
    --    " - " ("Cloud, DevOps" -> "Cloud - DevOps"); step 6 matches on the same cleaned title.
    -- -----------------------------------------------------------------------------------------
    UPDATE src_categories SET title = LEFT(btrim(regexp_replace(title, '\s*,\s*', ' - ', 'g')), 120);

    INSERT INTO blog_categories (title, description, date_created, date_modified)
    SELECT DISTINCT ON (LOWER(c.title))
           c.title, LEFT(NULLIF(c.description, ''), 255),
           c.date_created AT TIME ZONE 'UTC', c.date_updated AT TIME ZONE 'UTC'
    FROM src_categories c
    WHERE NOT EXISTS (SELECT 1 FROM blog_categories t WHERE LOWER(t.title) = LOWER(c.title))
    ORDER BY LOWER(c.title), c.id;
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RAISE NOTICE 'categories added: %', v_count;

    -- -----------------------------------------------------------------------------------------
    -- 5a. blog_posts.slug is unique, but Blogifier's "Slug" column wasn't. If two source posts
    --     share a slug, keep the oldest (lowest Id - the one Blogifier's slug lookup returned, so
    --     the one readers actually saw) instead of aborting the whole copy; the others are listed.
    -- -----------------------------------------------------------------------------------------
    CREATE TEMP TABLE src_posts_skipped ON COMMIT DROP AS
    SELECT p.id, p.slug, p.title
    FROM src_posts p
    WHERE EXISTS (SELECT 1 FROM src_posts o WHERE o.slug = p.slug AND o.id < p.id);

    DELETE FROM src_posts p USING src_posts_skipped s WHERE p.id = s.id;

    -- -----------------------------------------------------------------------------------------
    -- 5. Posts and pages, matched by slug. Published <= year 1900 is Blogifier's draft marker
    --    (DateTime.MinValue, or -infinity depending on the Npgsql version that wrote it).
    -- -----------------------------------------------------------------------------------------
    INSERT INTO blog_posts (author_id, post_type, title, slug, description, content, cover,
                            post_views, is_featured, published_at, date_created, date_modified)
    SELECT v_author_id,
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
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RAISE NOTICE 'posts/pages added: %', v_count;

    -- -----------------------------------------------------------------------------------------
    -- 6. Post <-> category links, resolved through slug and title.
    -- -----------------------------------------------------------------------------------------
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
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RAISE NOTICE 'post-category links added: %', v_count;

    -- -----------------------------------------------------------------------------------------
    -- 7. Summary (server output).
    -- -----------------------------------------------------------------------------------------
    RAISE NOTICE 'source posts copied or already present: %', (SELECT COUNT(*) FROM src_posts);
    RAISE NOTICE 'source posts skipped (duplicate slug): %', (SELECT COUNT(*) FROM src_posts_skipped);
    RAISE NOTICE 'blog_posts now: %', (SELECT COUNT(*) FROM blog_posts);
    RAISE NOTICE 'source categories: %', (SELECT COUNT(*) FROM src_categories);
    RAISE NOTICE 'blog_categories now: %', (SELECT COUNT(*) FROM blog_categories);
    RAISE NOTICE 'blog_post_categories now: %', (SELECT COUNT(*) FROM blog_post_categories);

    FOR r IN SELECT id, slug, title FROM src_posts_skipped ORDER BY slug, id LOOP
        RAISE NOTICE 'skipped duplicate slug: Blogifier post % "%" (slug %)', r.id, r.title, r.slug;
    END LOOP;
END
$migrate$;

-- Every local image/file path the migrated content references - each must exist under the blog
-- storage folder (BlogStorage:RootPath) for the site to render completely. Absolute http(s)
-- URLs are not listed; they keep pointing wherever they already did. Read-only; it can be
-- re-run on its own at any time.
SELECT DISTINCT path AS referenced_storage_path
FROM (
    SELECT regexp_replace(cover, '^/', '') AS path FROM blog_posts WHERE cover ~ '^/?data/'
    UNION
    SELECT regexp_replace((regexp_matches(content, '\(/?(data/[^)\s"]+)', 'g'))[1], '^/', '') FROM blog_posts
    UNION
    SELECT regexp_replace((regexp_matches(content, 'src="/?(data/[^"]+)"', 'g'))[1], '^/', '') FROM blog_posts
) refs
ORDER BY 1;
