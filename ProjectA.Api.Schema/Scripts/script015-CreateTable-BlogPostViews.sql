-- One row per view of a published blog post/page, recorded by ProjectA.Blog.Web. Feeds the
-- admin dashboard's daily-views charts and the per-day visitor (IP address + user agent)
-- breakdown. blog_posts.post_views stays as the all-time counter (it also carries the totals
-- migrated from Blogifier, which never recorded individual views).
--
-- Days are reported in UTC (viewed_at is a TIMESTAMPTZ, grouped "AT TIME ZONE 'UTC'").
CREATE TABLE IF NOT EXISTS blog_post_views (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id         BIGINT NOT NULL REFERENCES blog_posts(id) ON DELETE CASCADE,
    viewed_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    ip_address      INET NULL,
    user_agent      VARCHAR(512) NULL
);
CREATE INDEX IF NOT EXISTS idx_blog_post_views_viewed_at ON blog_post_views (viewed_at);
CREATE INDEX IF NOT EXISTS idx_blog_post_views_post_id_viewed_at ON blog_post_views (post_id, viewed_at);
