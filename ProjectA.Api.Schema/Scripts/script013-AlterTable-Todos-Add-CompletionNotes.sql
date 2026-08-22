-- Project-scoped ToDos: when a ToDo is completed via the Project Details page's "Complete"
-- modal, the user may enter free-text completion notes in Markdown. Rendered back as Markdown
-- under the ToDo's title wherever a completed ToDo is displayed. Optional - a ToDo can be
-- completed with no notes at all.
ALTER TABLE todos ADD COLUMN IF NOT EXISTS completion_notes TEXT NULL;
