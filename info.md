# Verify data from terminal
psql -U xmltcp_user -d xmltcp_accounts
# Query you should run 
SELECT
p.document_number,
p.full_name,
a.account_number,
a.currency,
a.balance,
a.status
FROM persons p
LEFT JOIN accounts a ON a.person_id = p.id
ORDER BY p.document_number, a.account_number;