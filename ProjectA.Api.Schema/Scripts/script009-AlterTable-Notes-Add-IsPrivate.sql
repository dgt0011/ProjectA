-- A Private note (and anything that inherits privacy from it - see NotePrivacy.cs on the API
-- side) is only visible to authenticated callers. Defaults to false so every existing note
-- stays publicly visible after this migration runs.
ALTER TABLE notes ADD COLUMN IF NOT EXISTS is_private BOOLEAN NOT NULL DEFAULT FALSE;
