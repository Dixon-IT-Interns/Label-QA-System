# SQL Server initialization

`db.sql` is the primary first-time database setup method. It creates `DixonLabelVerificationDB`, all application tables and constraints, useful indexes, supported-brand seeds, role/status catalogs, and initial settings. It is designed to be safe to run repeatedly. It does not create an administrator or contain a password.

Run from the repository root with an account that can create a database:

```powershell
sqlcmd -S 10.52.2.78 -U Sakshamjain -d master -C -i .\database\db.sql
```

`sqlcmd` prompts for the password; do not put it in the command line or this file. The login must have permission to create the database. These settings match the supplied SQL Server connection dialog: SQL authentication, encryption enabled, and Trust Server Certificate enabled. SQL Server Management Studio can also execute the script.

The application settings contain the server address, database name, and username but intentionally omit the password. Set the complete `ConnectionStrings__DefaultConnection` environment variable from a secret provider before starting the API. The API does not create the database or run EF migrations at startup. Do not use EF migrations as the database initializer; use `db.sql` and review any future schema changes separately.

Run `db.sql` again against an existing database to apply the confidence-column type correction for `OCRResults.OverallConfidence` and `ExtractedFields.Confidence`.

The script seeds the four supported brands, the role catalog, the `OcrMinimumConfidence` setting, status definitions, and the HP `15-fd1652TU` sample model. The HP CO profile includes active Model, Processor, Storage, RAM, Operating System, Display, and Battery fields. The HP CONFIG profile has exactly two active comparison fields: Model No. and Queue No. Queue No. retains the 0.55 confidence threshold and uses a dedicated text-band ROI below the barcode: decoded barcode geometry is preferred, with image-based linear-barcode/text-band localization and a profile-coordinate fallback when decoding is unavailable. Four preprocessing variants and three Tesseract PSM modes feed candidate consensus; decoded barcode content is used only for cross-validation. Re-running the initializer deactivates the old CONFIG specification fields and adds or updates these two required fields without changing the CO profile.

The one-time first-admin bootstrap is performed after database initialization:

```powershell
$env:BOOTSTRAP_ADMIN_FULL_NAME = "System Administrator"
$env:BOOTSTRAP_ADMIN_USERNAME = "admin"
$env:BOOTSTRAP_ADMIN_EMAIL = "admin@example.com"
$securePassword = Read-Host "Enter a unique password (12+ characters)" -AsSecureString
$env:BOOTSTRAP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new("", $securePassword).Password
Set-Location .\backend
dotnet run --project .\Dixon.LabelVerification.API -- --bootstrap-admin
```

Set the connection string and JWT configuration before running the bootstrap command; it uses the same validated startup configuration as the API. Clear temporary bootstrap variables after the command. Do not run this command against a populated database.

Back up SQL Server and the configured private inspection storage directory together. Inspection image and report binaries are never stored in SQL Server.
