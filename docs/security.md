# Aurora Cloud Storage - Security and Data Retention

## Encryption
All files uploaded to Aurora Cloud Storage are encrypted at rest using AES-256 and
in transit using TLS 1.3. Encryption keys are rotated automatically every 90 days.

## Data Retention
Deleted files are moved to a recovery trash folder and permanently purged after 30
days. Enterprise customers can configure a custom retention window between 7 and 365
days through the admin console.

## Access Control
Aurora supports role-based access control (RBAC) with four roles: Owner, Admin,
Editor, and Viewer. Business and Enterprise plans additionally support single
sign-on (SSO) via SAML 2.0 and enforce two-factor authentication (2FA) for all
members.

## Compliance
Aurora Cloud Storage is SOC 2 Type II certified and undergoes an independent
third-party security audit every year. Enterprise customers can request a signed
copy of the latest audit report and a Business Associate Agreement (BAA) for
healthcare-related use cases.

## Incident Response
In the event of a confirmed data breach, affected customers are notified within 72
hours by email and through the status page at status.aurorastorage.example.
