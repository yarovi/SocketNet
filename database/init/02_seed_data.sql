
INSERT INTO persons (id, document_number, full_name)
    OVERRIDING SYSTEM VALUE
VALUES
    (1, '40581711', 'JUAN PEREZ'),
    (2, '70451236', 'MARIA LOPEZ'),
    (3, '75841236', 'CARLOS RAMIREZ'),
    (4, '12345678', 'ANA TORRES');

INSERT INTO accounts (
    person_id,
    account_number,
    currency,
    balance,
    status
)
VALUES
    (1, '00123456789', 'PEN', 1500.00, 'ACTIVE'),
    (1, '00123456790', 'USD', 250.00, 'INACTIVE'),
    (2, '00987654321', 'PEN', 800.00, 'ACTIVE'),
    (3, '00555111222', 'PEN', 0.00, 'ACTIVE');

SELECT setval(
               pg_get_serial_sequence('persons', 'id'),
               (SELECT MAX(id) FROM persons)
       );
