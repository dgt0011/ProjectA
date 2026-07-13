-- The free-text `category` column is replaced by the `category_id` foreign key that
-- script003 already added (but left unused). Nothing populates `category_id` from the old
-- text values here - this project has no meaningful data to migrate, so the column is just
-- dropped along with its now-orphaned index.
DROP INDEX IF EXISTS idx1_todos;
ALTER TABLE todos DROP COLUMN IF EXISTS category;
