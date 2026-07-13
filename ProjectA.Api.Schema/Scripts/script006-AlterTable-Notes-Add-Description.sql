-- A short, plain-text summary shown in the Notes list, separate from the long Markdown
-- Body - keeps the list scannable without needing to open each note.
ALTER TABLE notes ADD COLUMN IF NOT EXISTS description VARCHAR(500) NULL;
