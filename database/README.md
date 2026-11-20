# SQL Server initialization

`db.sql` is the primary first-time database setup method. It creates `DixonLabelVerificationDB`, all application tables and constraints, useful indexes, supported-brand seeds, the Supervisor/Operator role catalog, and initial settings. It is designed to be safe to run repeatedly. It does not create a user or contain a password.

Run from the repository root with an account that can create a database:

```powershell
sqlcmd -S 10.52.2.78 -U Sakshamjain -d master -C -i .\database\db.sql
```

`sqlcmd` prompts for the password; do not put it in the command line or this file. The login must have permission to create the database. These settings match the supplied SQL Server connection dialog: SQL authentication, encryption enabled, and Trust Server Certificate enabled. SQL Server Management Studio can also execute the script.

For local development, copy `backend/Dixon.LabelVerification.API/appsettings.Local.example.json` to `backend/Dixon.LabelVerification.API/appsettings.Local.json` once and replace `REPLACE_WITH_SQL_PASSWORD` with the SQL login password. The API loads this git-ignored file automatically, so you do not need to set the connection string in each terminal. Never commit the local file or put credentials in checked-in settings. For deployed environments, use a secret provider or environment variables instead. The API does not create the database or run EF migrations at startup. Do not use EF migrations as the database initializer; use `db.sql` and review any future schema changes separately.

Back up an existing database before re-running `db.sql`. In addition to its idempotent schema updates, the script adds missing password-reset columns, maps Admin users to Supervisor and Viewer users to Operator, removes the obsolete role catalog values, and enforces exactly Supervisor and Operator. Existing users and inspections are retained. The API does not apply EF migration history.

The script seeds the four supported brands, the role catalog, the `OcrMinimumConfidence` setting, status definitions, and the HP `15-fd1652TU` sample model. It also creates the `MasterLabels` and `MasterLabelFields` tables used to persist model-specific COO/Config reference images, OCR evidence, and normalized field snapshots. Supervisors configure or replace those references; inspections copy the reference images and extracted evidence into each inspection so historical results remain unchanged after a master is replaced. Operators upload only actual labels, and an inspection cannot be created until both master types are configured for that model.

Default profiles are provisioned automatically the first time a master label is saved for a model, so supervisors do not need to configure profiles per model. The default CO fields are Model No., QR Code, and P/N; the default CONFIG fields are Model No. and Queue No. Existing active profiles are retained when they contain these required fields. The HP CONFIG profile's Queue No. field uses a dedicated text-band ROI below the barcode: decoded barcode geometry is preferred, with image-based linear-barcode/text-band localization and a profile-coordinate fallback when decoding is unavailable. Four preprocessing variants and three Tesseract PSM modes feed candidate consensus; decoded barcode content is used only for cross-validation. Re-running the initializer deactivates obsolete CO and CONFIG fields and restores the required comparison fields.

The one-time first-Supervisor bootstrap is performed after database initialization (the `BOOTSTRAP_ADMIN_*` variable names remain for compatibility):

```powershell
$env:BOOTSTRAP_ADMIN_FULL_NAME = "System Administrator"
$env:BOOTSTRAP_ADMIN_USERNAME = "admin"
$env:BOOTSTRAP_ADMIN_EMAIL = "admin@example.com"
$securePassword = Read-Host "Enter a unique password (12+ characters)" -AsSecureString
$env:BOOTSTRAP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new("", $securePassword).Password
Set-Location .\backend
dotnet run --project .\Dixon.LabelVerification.API -- --bootstrap-admin
```

The bootstrap command uses the same database configuration as the API, including `appsettings.Local.json`. Clear temporary bootstrap variables after the command. Do not run this command against a populated database.

Back up SQL Server and the configured private inspection storage directory together. Inspection, master-label, and report binaries are stored in private file storage; SQL Server stores their metadata, paths, OCR evidence, and extracted fields.
