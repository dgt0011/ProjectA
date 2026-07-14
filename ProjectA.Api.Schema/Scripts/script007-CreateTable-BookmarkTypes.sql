CREATE TABLE IF NOT EXISTS bookmark_types (
    id                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title               VARCHAR(255) NOT NULL,
    color               VARCHAR(7) NULL,
    icon                BYTEA NULL,
    icon_content_type   VARCHAR(100) NULL
);

-- Nullable: a bookmark has zero or one bookmark type. REFERENCES with no ON DELETE clause
-- means a bookmark type still assigned to at least one bookmark cannot be deleted (same
-- "block, don't cascade" convention as todos.category_id -> categories(id)).
ALTER TABLE bookmarks ADD COLUMN IF NOT EXISTS bookmark_type_id BIGINT NULL REFERENCES bookmark_types(id);
CREATE INDEX IF NOT EXISTS idx_bookmarks_bookmark_type_id ON bookmarks(bookmark_type_id);
