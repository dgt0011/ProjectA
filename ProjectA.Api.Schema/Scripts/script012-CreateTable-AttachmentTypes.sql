CREATE TABLE IF NOT EXISTS attachment_types (
    id                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title               VARCHAR(255) NOT NULL,
    color               VARCHAR(7) NULL,
    icon                BYTEA NULL,
    icon_content_type   VARCHAR(100) NULL
);

-- Nullable: an attachment has zero or one attachment type. REFERENCES with no ON DELETE
-- clause means an attachment type still assigned to at least one attachment cannot be deleted
-- (same "block, don't cascade" convention as bookmarks.bookmark_type_id -> bookmark_types(id)).
ALTER TABLE attachments ADD COLUMN IF NOT EXISTS attachment_type_id BIGINT NULL REFERENCES attachment_types(id);
CREATE INDEX IF NOT EXISTS idx_attachments_attachment_type_id ON attachments(attachment_type_id);
