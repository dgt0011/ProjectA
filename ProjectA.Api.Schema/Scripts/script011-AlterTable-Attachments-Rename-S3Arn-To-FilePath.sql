-- Attachments are no longer stored in AWS S3 - they're saved locally under
-- ProjectA.Web/wwwroot/files instead, so this column now holds a web-relative path (e.g.
-- "/files/3f2c1a9e-report.pdf") rather than an S3 ARN. Renaming instead of dropping and
-- re-adding preserves any existing rows' values in place (their contents are stale S3 ARNs
-- until re-uploaded/edited, but the column itself carries over with no data loss).
-- Guarded (rather than a plain ALTER TABLE ... RENAME COLUMN) so this script stays safe to
-- re-run if it's ever re-applied outside DbUp's normal one-time-per-script journal tracking.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'attachments' AND column_name = 's3_arn'
    ) THEN
        ALTER TABLE attachments RENAME COLUMN s3_arn TO file_path;
    END IF;
END $$;
