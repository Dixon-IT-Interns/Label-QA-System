# SQL Server initialization

`db.sql` is the primary first-time database setup method. It creates `DixonLabelVerificationDB`, all application tables and constraints, useful indexes, supported-brand seeds, the Admin/Supervisor/Operator role catalog, and initial settings. It is designed to be safe to run repeatedly. It does not create a user or contain a password.

Run from the repository root with an account that can create a database:

```powershell
sqlcmd -S 10.52.2.78 -U Sakshamjain -d master -C -i .\database\db.sql
```

`sqlcmd` prompts for the password; do not put it in the command line or this file. The login must have permission to create the database. These settings match the supplied SQL Server connection dialog: SQL authentication, encryption enabled, and Trust Server Certificate enabled. SQL Server Management Studio can also execute the script.

For local development, copy `backend/Dixon.LabelVerification.API/appsettings.Local.example.json` to `backend/Dixon.LabelVerification.API/appsettings.Local.json` once and replace `REPLACE_WITH_SQL_PASSWORD` with the SQL login password. The API loads this git-ignored file automatically, so you do not need to set the connection string in each terminal. Never commit the local file or put credentials in checked-in settings. For deployed environments, use a secret provider or environment variables instead. The API does not create the database or run EF migrations at startup. Do not use EF migrations as the database initializer; use `db.sql` and review any future schema changes separately.

Back up an existing database before re-running `db.sql`. Its idempotent schema updates add Brand assignment and user-attribution relationships, keep Admin roles, map legacy Viewer users to Operator, and remove the old user `IsActive` field. Accounts marked inactive are permanently deleted as part of this transition; their user-attribution references are set to null so inspections, OCR results, master labels, and reports are retained. For legacy Supervisor/Operator accounts, Brand assignment is backfilled only when their inspection history identifies exactly one Brand. An account with no unambiguous Brand remains unassigned and cannot sign in until an Admin assigns one. The API does not apply EF migration history.

The script seeds the four supported brands, the role catalog, the `OcrMinimumConfidence` setting, status definitions, and the HP `15-fd1652TU` sample model. It also creates the `MasterLabels` and `MasterLabelFields` tables used to persist model-specific COO/Config reference images, OCR evidence, and normalized field snapshots. Supervisors configure or replace those references; inspections copy the reference images and extracted evidence into each inspection so historical results remain unchanged after a master is replaced. Operators upload only actual labels, and an inspection cannot be created until both master types are configured for that model.

Default profiles are provisioned automatically the first time a master label is saved for a model, so supervisors do not need to configure profiles per model. The default CO fields are Model No., QR Code, and P/N; the default CONFIG fields are Model No. and Queue No. Existing active profiles are retained when they contain these required fields. The HP CONFIG profile's Queue No. field uses a dedicated text-band ROI below the barcode: decoded barcode geometry is preferred, with image-based linear-barcode/text-band localization and a profile-coordinate fallback when decoding is unavailable. Four preprocessing variants and three Tesseract PSM modes feed candidate consensus; decoded barcode content is used only for cross-validation. Re-running the initializer deactivates obsolete CO and CONFIG fields and restores the required comparison fields.

The one-time first-Admin bootstrap is performed after database initialization (the `BOOTSTRAP_ADMIN_*` variable names remain for compatibility). It can create an Admin when no Admin exists, even if other users are already present:

```powershell
$env:BOOTSTRAP_ADMIN_FULL_NAME = "System Administrator"
$env:BOOTSTRAP_ADMIN_USERNAME = "admin"
$env:BOOTSTRAP_ADMIN_EMAIL = "admin@example.com"
$securePassword = Read-Host "Enter a unique password (12+ characters)" -AsSecureString
$env:BOOTSTRAP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new("", $securePassword).Password
Set-Location .\backend
dotnet run --project .\Dixon.LabelVerification.API -- --bootstrap-admin
```

The bootstrap command uses the same database configuration as the API, including `appsettings.Local.json`. Clear temporary bootstrap variables after the command. Do not run it if an Admin already exists.

Admins can manage Supervisor and Operator accounts and all Brands. Admin accounts are hidden from User Management and cannot be created, edited, or deleted there. Supervisors can manage only Operators for their assigned Brand; Operators can access only Label QA/OCR data for their assigned Brand. Only Admins can permanently delete non-Admin user accounts. Deleting a user preserves business history by setting nullable attribution fields to null.

The EF migration `AddUserBrandAssignmentAndAdmin` provides the equivalent schema change for databases managed through EF migrations. Do not apply it to a `db.sql`-initialized database unless the EF migration history has been correctly baselined. The API does not automatically apply either schema-update path at startup.

## Add the `saksham` Admin account

After applying the current schema, open `add-admin.sql` and replace the `@PasswordHash` placeholder with the raw Base64 ASP.NET Core Identity hash. Do not include SQLCMD variable syntax or wrap the value in parentheses. Run the edited script in SSMS or execute it with `sqlcmd`:

```powershell
sqlcmd -S <server> -d DixonLabelVerificationDB -C -i .\database\add-admin.sql
```

The script creates `saksham` with the configured full name and email, no Brand assignment, and the Admin role. If that username already exists, it updates the same account to Admin and replaces its password hash; it fails if the email belongs to a different account. The hash is a credential—do not commit the edited script or share the hash. At login, enter the plaintext password corresponding to the hash, not the hash itself.

Back up SQL Server and the configured private inspection storage directory together. Inspection, master-label, and report binaries are stored in private file storage; SQL Server stores their metadata, paths, OCR evidence, and extracted fields.

## Clear application data while preserving users

`clear-app-data-except-users.sql` removes all rows from the application data tables, including inspection history, model configuration, master labels, and system settings. It preserves `ApplicationUsers` and `UserRoles`; the latter is required by the user-role foreign key. The script does not drop tables or modify the database schema.

Back up the database first. Edit `@ConfirmDatabase` in the script to `DixonLabelVerificationDB`, then run it against that database:

```powershell
sqlcmd -S <server> -U <login> -d DixonLabelVerificationDB -C -i .\database\clear-app-data-except-users.sql
```

`sqlcmd` prompts for the password. The script checks the connected database and confirmation value, and performs deletions in a transaction. Inspection, master-label, and report files in private file storage are not deleted by this SQL script; clean up those files separately if they should also be removed.
