-- ---
-- Blog (migrated from Blogifier). Authors are ProjectA users - these columns carry the public
-- author profile shown on the blog (Blogifier's separate Authors table is not carried over).
-- ---
ALTER TABLE users ADD COLUMN IF NOT EXISTS display_name VARCHAR(160) NULL;
ALTER TABLE users ADD COLUMN IF NOT EXISTS bio TEXT NULL;
ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar VARCHAR(400) NULL;

-- Single-row table: the blog's site-wide settings (Blogifier's "Blogs" table, minus the theme
-- and newsletter/analytics-display columns that no longer apply).
CREATE TABLE IF NOT EXISTS blog_settings (
    id                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title               VARCHAR(160) NOT NULL,
    description         VARCHAR(450) NULL,
    items_per_page      INTEGER NOT NULL DEFAULT 10 CHECK (items_per_page BETWEEN 1 AND 100),
    include_featured    BOOLEAN NOT NULL DEFAULT TRUE,
    cover               VARCHAR(400) NULL,
    logo                VARCHAR(400) NULL,
    header_script       TEXT NULL,
    footer_script       TEXT NULL,
    date_created        TIMESTAMPTZ NOT NULL DEFAULT now(),
    date_modified       TIMESTAMPTZ NULL
);

INSERT INTO blog_settings (title, description)
SELECT 'Blog Title', 'Short Blog Description'
WHERE NOT EXISTS (SELECT 1 FROM blog_settings);

-- Kept separate from ProjectA's own `categories` table: blog categories are public-facing
-- tags that appear in blog URLs (/categories/{title}), whereas ProjectA categories organise
-- private notes/bookmarks.
CREATE TABLE IF NOT EXISTS blog_categories (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title           VARCHAR(120) NOT NULL,
    description     VARCHAR(255) NULL,
    date_created    TIMESTAMPTZ NOT NULL DEFAULT now(),
    date_modified   TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS idx_blog_categories_title ON blog_categories (LOWER(title));

-- post_type: 1 = Post, 2 = Page (same values Blogifier used).
-- published_at NULL means draft (Blogifier used DateTime.MinValue as that sentinel).
CREATE TABLE IF NOT EXISTS blog_posts (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    author_id       BIGINT NULL REFERENCES users(id) ON DELETE SET NULL,
    post_type       SMALLINT NOT NULL DEFAULT 1 CHECK (post_type IN (1, 2)),
    title           VARCHAR(160) NOT NULL,
    slug            VARCHAR(160) NOT NULL,
    description     VARCHAR(450) NOT NULL DEFAULT '',
    content         TEXT NOT NULL DEFAULT '',
    cover           VARCHAR(400) NULL,
    post_views      INTEGER NOT NULL DEFAULT 0,
    is_featured     BOOLEAN NOT NULL DEFAULT FALSE,
    published_at    TIMESTAMPTZ NULL,
    date_created    TIMESTAMPTZ NOT NULL DEFAULT now(),
    date_modified   TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_blog_posts_slug ON blog_posts (slug);
CREATE INDEX IF NOT EXISTS idx_blog_posts_published_at ON blog_posts (published_at DESC);
CREATE INDEX IF NOT EXISTS idx_blog_posts_author_id ON blog_posts (author_id);

CREATE TABLE IF NOT EXISTS blog_post_categories (
    post_id         BIGINT NOT NULL REFERENCES blog_posts(id) ON DELETE CASCADE,
    category_id     BIGINT NOT NULL REFERENCES blog_categories(id) ON DELETE CASCADE,
    PRIMARY KEY (post_id, category_id)
);
CREATE INDEX IF NOT EXISTS idx_blog_post_categories_category_id ON blog_post_categories (category_id);
