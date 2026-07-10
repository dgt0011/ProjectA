CREATE TABLE IF NOT EXISTS attachments (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title           VARCHAR(255),
    description     TEXT,
    s3_arn          TEXT NOT NULL,
    date_created    TIMESTAMPTZ NOT NULL DEFAULT now(),
    date_modified   TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS categories  (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title           VARCHAR(255) NOT NULL,
    description     TEXT
);

CREATE TABLE IF NOT EXISTS bookmarks (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    url             TEXT NOT NULL,
    description     TEXT
);

ALTER TABLE bookmarks ADD COLUMN IF NOT EXISTS title VARCHAR(255) NULL;
ALTER TABLE bookmarks ADD COLUMN IF NOT EXISTS rating SMALLINT DEFAULT 1 CHECK (rating BETWEEN 1 AND 10);
ALTER TABLE bookmarks ADD COLUMN IF NOT EXISTS date_created TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE bookmarks ADD COLUMN IF NOT EXISTS date_modified TIMESTAMPTZ NULL;

CREATE TABLE IF NOT EXISTS notes (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title           VARCHAR(255),
    body            TEXT,
    date_created    TIMESTAMPTZ NOT NULL DEFAULT now(),
    date_modified   TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS projects (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title           VARCHAR(255) NOT NULL,
    description     TEXT,
    start_date      TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS todos (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title           TEXT NOT NULL,
    actioned         BOOLEAN NOT NULL DEFAULT FALSE
);

-- category_id/project_id are nullable: no application code populates them yet (ToDo
-- still uses the free-text `category` column from script001), so NOT NULL here would
-- reject every future insert once this script finally parses successfully.
ALTER TABLE todos ADD COLUMN IF NOT EXISTS category_id BIGINT NULL REFERENCES categories(id);
ALTER TABLE todos ADD COLUMN IF NOT EXISTS project_id BIGINT NULL REFERENCES projects(id);
ALTER TABLE todos ADD COLUMN IF NOT EXISTS date_created TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE todos ADD COLUMN IF NOT EXISTS date_modified TIMESTAMPTZ NULL;

-- Indexes must come after the columns they index exist.
CREATE INDEX IF NOT EXISTS idx_todos_category_id ON todos(category_id);
CREATE INDEX IF NOT EXISTS idx_todos_project_id ON todos(project_id);

-- ---
-- Relationship Tables
-- ---

CREATE TABLE IF NOT EXISTS bookmark_categories (
    bookmark_id     BIGINT NOT NULL REFERENCES bookmarks(id),
    category_id     BIGINT NOT NULL REFERENCES categories(id),
    PRIMARY KEY (bookmark_id, category_id)
);
CREATE INDEX idx_bookmark_categories_category_id ON bookmark_categories(category_id);

CREATE TABLE note_categories (
    note_id         BIGINT NOT NULL REFERENCES notes(id),
    category_id     BIGINT NOT NULL REFERENCES categories(id),
    PRIMARY KEY (note_id, category_id)
);
CREATE INDEX idx_note_categories_category_id ON note_categories(category_id);

CREATE TABLE note_bookmarks (
    note_id         BIGINT NOT NULL REFERENCES notes(id),
    bookmark_id     BIGINT NOT NULL REFERENCES bookmarks(id),
    PRIMARY KEY (note_id, bookmark_id)
);
CREATE INDEX idx_note_bookmarks_bookmark_id ON note_bookmarks(bookmark_id);

CREATE TABLE note_attachments (
    note_id         BIGINT NOT NULL REFERENCES notes(id),
    attachment_id   BIGINT NOT NULL REFERENCES attachments(id),
    PRIMARY KEY (note_id, attachment_id)
);
CREATE INDEX idx_note_attachments_attachment_id ON note_attachments(attachment_id);

CREATE TABLE project_bookmarks (
    project_id      BIGINT NOT NULL REFERENCES projects(id),
    bookmark_id     BIGINT NOT NULL REFERENCES bookmarks(id),
    PRIMARY KEY (project_id, bookmark_id)
);
CREATE INDEX idx_project_bookmarks_bookmark_id ON project_bookmarks(bookmark_id);

CREATE TABLE project_attachments (
    project_id      BIGINT NOT NULL REFERENCES projects(id),
    attachment_id   BIGINT NOT NULL REFERENCES attachments(id),
    PRIMARY KEY (project_id, attachment_id)
);
CREATE INDEX idx_project_attachments_attachment_id ON project_attachments(attachment_id);

CREATE TABLE project_notes (
    project_id      BIGINT NOT NULL REFERENCES projects(id),
    note_id         BIGINT NOT NULL REFERENCES notes(id),
    PRIMARY KEY (project_id, note_id)
);
CREATE INDEX idx_project_notes_note_id ON project_notes(note_id);