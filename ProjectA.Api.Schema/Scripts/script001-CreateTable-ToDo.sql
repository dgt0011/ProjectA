CREATE TABLE IF NOT EXISTS todos (
    id int8 GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title text NOT NULL,
    actioned bool NOT NULL,
    category varchar(128) NOT NULL
);

CREATE INDEX IF NOT EXISTS idx1_todos ON todos (category);

