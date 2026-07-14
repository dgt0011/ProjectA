-- Self-referencing: a note can have zero or one parent, and therefore zero to many children
-- (found via the reverse lookup). No ON DELETE clause, matching every other FK in this schema -
-- deleting a note that still has children is blocked rather than cascading or orphaning them.
ALTER TABLE notes ADD COLUMN IF NOT EXISTS parent_note_id BIGINT NULL REFERENCES notes(id);
CREATE INDEX IF NOT EXISTS idx_notes_parent_note_id ON notes(parent_note_id);
