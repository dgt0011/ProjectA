CREATE TABLE IF NOT EXISTS users (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username        VARCHAR(100) NOT NULL,
    password_hash   TEXT NOT NULL,
    date_created    TIMESTAMPTZ NOT NULL DEFAULT now(),
    date_modified   TIMESTAMPTZ NULL
);

-- Case-insensitive uniqueness: "Admin" and "admin" are the same account.
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_username ON users (LOWER(username));
