
CREATE TABLE persons (
                         id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                         document_number VARCHAR(8) NOT NULL UNIQUE,
                         full_name VARCHAR(150) NOT NULL,
                         created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

                         CONSTRAINT ck_person_document
                             CHECK (document_number ~ '^[0-9]{8}$')
    );

CREATE TABLE accounts (
                          id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                          person_id BIGINT NOT NULL REFERENCES persons(id),
                          account_number VARCHAR(20) NOT NULL UNIQUE,
                          currency VARCHAR(3) NOT NULL,
                          balance NUMERIC(18,2) NOT NULL,
                          status VARCHAR(10) NOT NULL DEFAULT 'ACTIVE',
                          updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

                          CONSTRAINT ck_account_currency
                              CHECK (currency IN ('PEN', 'USD')),

                          CONSTRAINT ck_account_balance
                              CHECK (balance >= 0),

                          CONSTRAINT ck_account_status
                              CHECK (status IN ('ACTIVE', 'INACTIVE'))
);

CREATE INDEX idx_accounts_person_id
    ON accounts(person_id);
