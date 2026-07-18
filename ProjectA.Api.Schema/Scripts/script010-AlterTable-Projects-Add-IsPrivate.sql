-- A Private project is only visible to authenticated callers (see ProjectA.Api's
-- GetProjectById/GetProjectList endpoints). Defaults to false so every existing project stays
-- publicly visible after this migration runs.
ALTER TABLE projects ADD COLUMN IF NOT EXISTS is_private BOOLEAN NOT NULL DEFAULT FALSE;
