# Dixon Laptop Label Verification

Configuration-driven CO and CONFIG label verification for HP, ASUS, Acer, Gigabyte, and additional configured brands. ASP.NET Core owns authentication, persistence, workflows, comparisons, audit records, and reports. The Python service only processes images and provides OCR/barcode evidence; it never accesses SQL Server or decides an inspection result.

## Architecture

```text
React + Vite ── Axios/JWT ──> ASP.NET Core .NET 10 ──> SQL Server
                                      │
                                      ├── original images and PDF files in private local storage
                                      └── FastAPI / PaddleOCR / OpenCV / zxing-cpp
```

- **Frontend:** React, Vite, React Router, Axios, and Lucide React. Screens cover login, dashboard, new inspection, inspection history/details, brands, models, profiles/fields, users, and system settings.
- **Backend:** .NET 10 Web API and EF Core SQL Server provider. JWT authentication supports Admin, Supervisor, and Operator roles with server-enforced Brand assignment. SQL initialization is the standalone `database/db.sql` script; the API does not run EF migrations or create the database at startup.
- **OCR:** Python 3.11, FastAPI, Tesseract OCR via `pytesseract`, OpenCV, Pillow, and zxing-cpp. CO processing extracts only P/N, Model No., and decoded QR payloads from targeted regions; other CO text and barcode values are neither saved nor compared. CONFIG processing remains profile-targeted. The project keeps the older PaddleOCR provider as a configurable fallback via `OCR_ENGINE=paddle` but defaults to Tesseract for reliability on the current workstation setup.
- **Storage:** Original JPEG/PNG uploads and generated PDFs are stored outside SQL Server under `storage/inspections` by default. SQL stores paths, hashes, sizes, and other metadata. Files are served only through authenticated API endpoints.
- **Comparison policy:** OCR text is not compared line-by-line. CO compares its active configured profile fields. CONFIG compares only `MODEL_NO` and `QUEUE_NO`: OCR is cropped to x=8–92%, y=4–17% for the top model and x=8–92%, y=87–95% for the below-barcode human-readable Queue No. Barcodes remain separate metadata and are never substituted for Queue No. Blue annotation-like pixels are masked only from the OCR working copy; original images remain unchanged. Missing, ambiguous, or low-confidence configured fields require review; unrelated OCR text and image-level warnings do not change a comparison result.

## Project layout

```text
backend/
  Dixon.LabelVerification.sln
  Dixon.LabelVerification.API/
    Configuration/ Controllers/ Data/ DTOs/ Entities/
    Interfaces/ Middleware/ Repositories/ Services/
frontend/src/
  assets/ components/ context/ hooks/ layouts/ pages/ services/ styles/ utils/
ocr-service/
  app/api/ core/ image_processing/ ocr/ schemas/ services/
database/db.sql
docs/
storage/inspections/
```

## Prerequisites

- SQL Server 2019+ (Developer, Express, or a supported managed SQL Server) and `sqlcmd` or SQL Server Management Studio
- .NET SDK 10
- Node.js compatible with the installed Vite version (20.19+ or 22.12+)
- Python 3.11 (32/64-bit runtime, with a Windows-friendly environment)
- Tesseract OCR installed and available on `PATH`; use `where tesseract` to confirm or set `TESSERACT_CMD` if the executable lives outside the default install location. Example Windows install: `winget install --id Tesseract.Tesseract`.
- A QuestPDF license tier applicable to your organization. Review the current QuestPDF terms and confirm eligibility before using reports in production.

## 1. Initialize SQL Server

From the repository root, run the idempotent database initializer against the intended SQL Server instance:

```powershell
sqlcmd -S 10.52.2.78 -U Sakshamjain -d master -C -i .\database\db.sql
```

`sqlcmd` prompts for the SQL login password because it is not included in the command. The login must have permission to create the database. `database/db.sql` creates `DixonLabelVerificationDB`, schema, indexes, role/status/settings seeds, supported brands, and the sample HP model/profiles. The default CO fields are Model No., QR Code, and P/N; the default CONFIG fields are Model No. and Queue No. The API automatically provisions these defaults for a model when a Supervisor first saves one of its master labels, so profiles do not need to be created manually for every model. Re-run the initializer to apply its idempotent schema and sample-profile updates. Do not point setup commands at a production instance without review and approval.

## 2. Configure and bootstrap the backend

The backend loads `appsettings.Local.json` automatically for machine-local settings. The repository includes `backend/Dixon.LabelVerification.API/appsettings.Local.example.json`; copy it to `backend/Dixon.LabelVerification.API/appsettings.Local.json` once, then replace `REPLACE_WITH_SQL_PASSWORD` with the SQL Server password. This local file is git-ignored, so the connection settings persist between runs without putting the password in checked-in files or re-entering it in each terminal. Keep the file on your machine and never commit it. Environment variables can still override local settings when needed.

The server requires encryption; `TrustServerCertificate=True` matches the provided connection dialog. Development generates a temporary signing key if none is configured, so tokens become invalid after the API restarts. Configure a persistent secret-managed JWT signing key for production.

Public sign-up is disabled. Admins can manage Supervisor and Operator accounts and manage all Brands. Admin accounts are hidden from User Management and cannot be created, edited, or deleted there. A Supervisor can manage only Operators assigned to their single Brand; an Operator can access Label QA/OCR data only for their assigned Brand. Only Admins can permanently delete non-Admin user accounts. User deletion preserves inspection and OCR data by clearing only user-attribution references. Password reset and Google sign-in are available to existing accounts with a valid role/Brand assignment. Google sign-in requires the verified Google email to match the account. To enable Google sign-in, set backend `Google__ClientId` and frontend `VITE_GOOGLE_CLIENT_ID` to the same Google OAuth web client ID. To enable password-reset email, configure `Smtp__Host`, `Smtp__Port` (typically `587`), `Smtp__UserName`, `Smtp__Password`, `Smtp__FromEmail`, and optionally `Smtp__FromName` / `Smtp__UseSsl` through environment variables or a secret store. Set `Frontend__BaseUrl` to the URL users open in their browser. Do not commit provider credentials.

Back up an existing database, then re-run `database/db.sql` before deploying these authentication changes. The initializer applies the Brand assignment and user-attribution schema updates in place, keeps Admin roles, maps legacy Viewer users to Operator, and permanently removes accounts marked inactive before the old `IsActive` column is dropped. Existing inspection/OCR records are retained; legacy Supervisor/Operator accounts are assigned a Brand only when their inspection history unambiguously identifies one. An unassigned Supervisor or Operator must be assigned a Brand by an Admin before signing in. The EF migration `AddUserBrandAssignmentAndAdmin` carries the same schema change for databases managed through EF migrations; do not apply it to a database initialized with `database/db.sql` unless its migration history has been correctly baselined.

Bootstrap the first Admin after the SQL script has run. The existing `BOOTSTRAP_ADMIN_*` environment variable names are retained for compatibility. The one-time command refuses to run if an Admin already exists:

```powershell
$env:BOOTSTRAP_ADMIN_FULL_NAME = "System Administrator"
$env:BOOTSTRAP_ADMIN_USERNAME = "admin"
$env:BOOTSTRAP_ADMIN_EMAIL = "admin@example.com"
$securePassword = Read-Host "Enter a unique password (12+ characters)" -AsSecureString
$env:BOOTSTRAP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new("", $securePassword).Password
Set-Location .\backend
dotnet run --project .\Dixon.LabelVerification.API -- --bootstrap-admin
```

The password is hashed using ASP.NET Core Identity's password hasher. Clear bootstrap environment variables after the one-time command. Then start the API; its database connection is read from the local settings file:

```powershell
dotnet run --project .\Dixon.LabelVerification.API
```

The development API listens on `http://localhost:5200`; Swagger is at `/swagger`, and `/health` is a liveness endpoint (it does not test SQL). Local Vite CORS origins are `http://localhost:5173` and `http://127.0.0.1:5173`.

Before downloading a PDF, set `Reports__QuestPDFLicense` to the tier that has been reviewed and approved for your organization (`Community`, `Professional`, or `Enterprise`). The service intentionally does not select a license tier for you.

## 3. Start the OCR service

Open another PowerShell terminal:

```powershell
Set-Location .\ocr-service
py -3.11 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
```

Install the native Tesseract executable separately from the Python package. On Windows, run `winget install --id UB-Mannheim.TesseractOCR --exact`. If the executable is not on `PATH` or was installed after the current terminal opened, set its path before starting the service:

```powershell
$env:TESSERACT_CMD = "C:\Program Files\Tesseract-OCR\tesseract.exe"
```

The service detects that standard Windows install path automatically. Verify the executable with `& $env:TESSERACT_CMD --version` (or `tesseract --version` when it is on `PATH`).

The project defaults to the Tesseract engine, which reads from a cleaned working copy and preserves raw OCR text plus structured artifacts in `storage/ocr`. Set `OCR_ENGINE=paddle` only if you intentionally want the older PaddleOCR provider. OpenCV applies preprocessing selectively (`auto`, `clahe`, `grayscale`, `otsu`, or `adaptive`) without altering the original uploaded image. Blue annotation-like pixels are masked only from OCR input and do not force a comparison review. OCR candidates are limited to the active CO/CONFIG fields, support same-line, adjacent-column, and nearby aligned multi-line labels, and use field-specific specification patterns when labels are absent. Ambiguous configured values remain unreadable. Default CO thresholds are 0.55 for Model No. and 0.65 for P/N and QR Code; CONFIG fields use 0.55. P/N can be cross-checked against the decoded QR payload. Keep labels in focus, fill the frame, and avoid glare.

The service health endpoint is `http://127.0.0.1:8000/health`; OpenAPI docs are at `/docs`. Optional thresholds, engine selection, and artifact storage are listed in `ocr-service/.env.example` and can be set as process environment variables. The service supports `/ocr/extract` and `/ocr/barcode`. Install the test client dependencies with `python -m pip install -r requirements-test.txt`, then run `.\.venv\Scripts\python.exe -m unittest discover -s tests -v` from `ocr-service`. The OCR API end-to-end test uses native Tesseract and generated master/actual label images.

The inspection API accepts exactly one image for each role: `MASTER_CO`, `ACTUAL_CO`, `MASTER_CONFIG`, and `ACTUAL_CONFIG`. After all four are uploaded, validate the set and start processing. Each image independently passes through OpenCV and Tesseract; the original, cleaned image, raw TXT, and parsed JSON are saved under a unique `storage/ocr/<job-id>/` folder. ASP.NET stores the OCR evidence and compares only configured profile fields. After a successful comparison, the final field-by-field JSON is written to `storage/results/<inspection-id>/comparison.json`.

To test one image directly against the OCR service from PowerShell:

```powershell
curl.exe -F "file=@C:\labels\master-co.png" `
  -F 'field_config=[{"field_name":"MODEL","display_name":"Model","aliases":["Product Model"]}]' `
  -F "minimum_confidence=0.85" `
  http://127.0.0.1:8000/ocr/extract
```

The JSON response includes `raw_text`, confidence-bearing OCR `lines`, configured `field_candidates`, a unique `job_id`, and relative paths to the saved original, cleaned image, TXT, and parsed JSON artifacts.

## 4. Start the frontend

Open another PowerShell terminal:

```powershell
Set-Location .\frontend
npm ci
npm run dev
```

Vite runs at `http://localhost:5173`. Copy `.env.example` to `.env.local` and set `VITE_API_BASE_URL` if the API is hosted elsewhere. Set `VITE_GOOGLE_CLIENT_ID` to the Google OAuth web client ID to enable the Google button.

## Inspection workflow

1. Sign in as a Supervisor or Operator. Supervisors can view system-wide history and administer users, labels, and settings; Operators can run inspections and access only their own history and reports.
2. Supervisors configure Master COO and Master Config as a pair from **Master Labels**. They can select an existing model or detect/create one from a COO image. Both label images are OCR-checked together: COO Model No. must match Config Model No., and COO P/N must match Config Queue No. The API saves or replaces neither label unless both comparisons pass.
3. Select a configured brand/model and upload or capture the Actual COO and Actual Config labels. The API automatically attaches that model's saved master labels; inspection creation is blocked with a Supervisor-facing message if either master is missing or stale.
4. Validate the saved master evidence and two actual images, then explicitly process the inspection. Master OCR is performed when the Supervisor saves/replaces a master; each inspection receives its own image/OCR snapshot so historical comparisons do not change when the master is later replaced.
5. Review the inspection status, stage comparisons, OCR evidence, audit log, and PDF report.

Brand/model/profile configuration, Master Labels, and user/settings management require Supervisor. Default CO (Model No., QR Code, P/N) and CONFIG (Model No., Queue No.) profiles are provisioned automatically per model during master-label setup; they can be overridden with active model-specific profiles. OCR associates configured labels with same-line, adjacent-column, and nearby aligned multi-line values using OCR coordinates. Ambiguous or unassociated text remains review-only; OCR does not guess values from unrelated text. The system reports PASS only if all required fields were read above their thresholds and comparisons succeeded.

Editing, adding, or deleting fields on a profile referenced by inspection history automatically creates a new active profile version. Existing inspections remain pinned to their original version.

## API route summary

- `POST /api/auth/login`, `/api/auth/google`, `/api/auth/forgot-password`, `/api/auth/reset-password`; `GET /api/auth/me`. Public sign-up is disabled.
- `GET /api/dashboard`
- `GET|POST /api/brands`; `GET|PUT|DELETE /api/brands/{id}`
- `GET|POST /api/models`; `GET|PUT|DELETE /api/models/{id}`
- `GET|POST /api/label-profiles`; profile field CRUD under `/api/label-profiles/{id}/fields` and `/api/field-definitions`
- Supervisor-only `GET /api/master-labels`, `GET /api/master-labels/models/{modelId}`, `PUT /api/master-labels/{modelId}/pair`, `POST /api/master-labels/detect-model`, and `GET /api/master-labels/{modelId}/{labelType}/image`; authenticated Operators and Supervisors can check model availability
- `GET|POST /api/inspections`; `GET /api/inspections/{id}` and history filtering/pagination
- Inspection image upload, list, authorized retrieval, and delete under `/api/inspections/{id}/images`
- `POST /api/inspections/{id}/validate|process|cancel`; `GET /api/inspections/{id}/logs|report`
- Supervisor-only `GET|POST /api/users`; `PUT /api/users/{id}` (only Operator accounts); `GET /api/settings`; `PUT /api/settings/ocr-confidence`

## Validation and genuine limitations

The .NET solution and React production build can be validated locally with:

```powershell
Set-Location .\backend
dotnet build .\Dixon.LabelVerification.sln --configuration Release
dotnet test .\Dixon.LabelVerification.sln --configuration Release
Set-Location ..\frontend
npm ci
npm run lint
npm run build
```

The OCR dependencies are large and may require platform-specific PaddlePaddle wheels/model downloads. PaddleOCR CPU inference disables oneDNN for compatibility with the PaddlePaddle runtime used during development. This workspace has been checked with a generated OCR sample, but not a representative production label dataset; calibrate profile aliases, confidence thresholds, and image-capture guidance with approved samples before deployment. Handwritten annotations are never erased from the source image. QuestPDF commercial-license eligibility must be verified by the deploying organization.
