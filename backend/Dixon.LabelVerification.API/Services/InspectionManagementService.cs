using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class InspectionManagementService(
    LabelVerificationDbContext db,
    IHttpClientFactory httpClientFactory,
    IFieldComparisonService fieldComparisonService,
    IInspectionWorkflowService inspectionWorkflowService,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<InspectionManagementService> logger,
    AuthenticatedUserContext currentUser,
    IActivityLogService activityLog) : IInspectionManagementService
{
    private readonly string _storageRoot = InspectionStoragePaths.ResolveRoot(configuration, environment);

    public async Task<InspectionDetailsResponse> CreateAsync(
        CreateInspectionRequest request,
        Guid operatorUserId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        currentUser.EnsureCanAccessBrand(request.BrandId);
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var model = await db.Models.Include(x => x.Brand)
            .SingleOrDefaultAsync(x => x.Id == request.ModelId && x.BrandId == request.BrandId, cancellationToken)
            ?? throw new InspectionValidationException("The selected model does not belong to the selected brand.");
        if (!model.IsActive || !model.Brand.IsActive)
        {
            throw new InspectionConflictException("Only active brands and models can be inspected.");
        }

        if (!await db.Users.AnyAsync(x => x.Id == operatorUserId, cancellationToken))
        {
            throw new InspectionValidationException("The authenticated operator account is unavailable.");
        }

        var profiles = await db.LabelProfiles
            .Include(x => x.FieldDefinitions).ThenInclude(x => x.Aliases)
            .Where(x => x.ModelId == model.Id && x.IsActive)
            .ToListAsync(cancellationToken);
        var coProfile = profiles.SingleOrDefault(x => x.LabelType == LabelType.CO)
            ?? throw new InspectionConflictException("No active COO label profile is configured for this model.");
        var configProfile = profiles.SingleOrDefault(x => x.LabelType == LabelType.CONFIG)
            ?? throw new InspectionConflictException("No active CONFIG label profile is configured for this model.");
        var coFields = CoComparisonFieldSelector.Select(coProfile.FieldDefinitions);
        var configFields = ConfigComparisonFieldSelector.Select(configProfile.FieldDefinitions);
        var masters = await db.MasterLabels.AsNoTracking().Include(x => x.Fields)
            .Where(x => x.ModelId == model.Id)
            .ToListAsync(cancellationToken);
        var coMaster = masters.SingleOrDefault(x => x.LabelType == LabelType.CO)
            ?? throw new InspectionConflictException(
                "Master COO label is not configured for this model. Ask a Supervisor to configure it in Master Labels.");
        var configMaster = masters.SingleOrDefault(x => x.LabelType == LabelType.CONFIG)
            ?? throw new InspectionConflictException(
                "Master Config label is not configured for this model. Ask a Supervisor to configure it in Master Labels.");
        EnsureMasterProfile(coMaster, coProfile, coFields);
        EnsureMasterProfile(configMaster, configProfile, configFields);

        var now = DateTimeOffset.UtcNow;
        var inspection = new InspectionSession
        {
            InspectionNumber = $"DIX-{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..32],
            BrandId = model.BrandId,
            ModelId = model.Id,
            LabelProfileCOId = coProfile.Id,
            LabelProfileConfigId = configProfile.Id,
            CreatedByUserId = operatorUserId,
            ProductionOrderNumber = TrimOrNull(request.ProductionOrderNumber),
            WorkOrderNumber = TrimOrNull(request.WorkOrderNumber),
            Status = InspectionStatus.READY,
            COStatus = InspectionStatus.READY,
            ConfigStatus = InspectionStatus.READY,
            SerialStatus = InspectionStatus.NOT_APPLICABLE,
            StartedAtUtc = now
        };
        var copiedPaths = new List<string>();
        try
        {
            inspection.Images.Add(await CopyMasterToInspectionAsync(
                inspection, coMaster, coFields, operatorUserId, copiedPaths, cancellationToken));
            inspection.Images.Add(await CopyMasterToInspectionAsync(
                inspection, configMaster, configFields, operatorUserId, copiedPaths, cancellationToken));
            db.InspectionSessions.Add(inspection);
            AddLog(inspection, operatorUserId, "CREATED",
            "Inspection created with pinned COO and CONFIG profiles and saved model master labels.",
                null, null, ipAddress);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await activityLog.RecordAsync("INSPECTION_STARTED", $"Started inspection {inspection.InspectionNumber}.", inspection.BrandId, inspection.Id, entityId: inspection.ModelId, entityType: "Inspection", ipAddress: ipAddress, cancellationToken: cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var path in copiedPaths)
                DeleteFile(path);
            throw;
        }
        logger.LogInformation("Inspection {InspectionNumber} created for model {ModelId}",
            inspection.InspectionNumber, model.Id);
        return await GetByIdAsync(inspection.Id, cancellationToken);
    }

    public async Task<PagedInspectionResponse> GetAllAsync(
        InspectionListQuery query,
        Guid userId,
        string role,
        Guid? brandId,
        CancellationToken cancellationToken)
    {
        if (userId != currentUser.UserId || role != currentUser.Role || brandId != currentUser.BrandId)
            throw new UnauthorizedAccessException("The inspection query does not match the authenticated account.");
        if (query.BrandId.HasValue)
            currentUser.EnsureCanAccessBrand(query.BrandId.Value);
        var inspections = InspectionQueryScope.Apply(
                db.InspectionSessions.AsNoTracking(), userId, role, brandId)
            .Include(x => x.Brand).Include(x => x.Model).Include(x => x.CreatedByUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            inspections = inspections.Where(x => x.InspectionNumber.Contains(search)
                || (x.SerialNumber != null && x.SerialNumber.Contains(search))
                || (x.ProductionOrderNumber != null && x.ProductionOrderNumber.Contains(search))
                || (x.WorkOrderNumber != null && x.WorkOrderNumber.Contains(search)));
        }
        if (query.BrandId.HasValue) inspections = inspections.Where(x => x.BrandId == query.BrandId);
        if (query.ModelId.HasValue) inspections = inspections.Where(x => x.ModelId == query.ModelId);
        if (query.Status.HasValue) inspections = inspections.Where(x => x.Status == query.Status);
        if (query.FromUtc.HasValue) inspections = inspections.Where(x => x.CreatedAtUtc >= query.FromUtc);
        if (query.ToUtc.HasValue) inspections = inspections.Where(x => x.CreatedAtUtc < query.ToUtc);

        inspections = (query.SortBy, query.Descending) switch
        {
            ("inspectionNumber", false) => inspections.OrderBy(x => x.InspectionNumber),
            ("inspectionNumber", true) => inspections.OrderByDescending(x => x.InspectionNumber),
            ("status", false) => inspections.OrderBy(x => x.Status),
            ("status", true) => inspections.OrderByDescending(x => x.Status),
            (_, false) => inspections.OrderBy(x => x.CreatedAtUtc),
            _ => inspections.OrderByDescending(x => x.CreatedAtUtc)
        };
        var total = await inspections.CountAsync(cancellationToken);
        var rows = await inspections.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedInspectionResponse(rows.Select(ToSummary).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<InspectionDetailsResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await db.InspectionSessions.AsNoTracking()
            .Include(x => x.Brand).Include(x => x.Model).Include(x => x.CreatedByUser)
            .Include(x => x.LabelProfileCO).Include(x => x.LabelProfileConfig)
            .Include(x => x.Images).ThenInclude(x => x.OcrResults).ThenInclude(x => x.ExtractedFields)
            .Include(x => x.StageResults).ThenInclude(x => x.FieldComparisons)
            .Include(x => x.Logs)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InspectionNotFoundException($"Inspection '{id}' was not found.");
        return ToDetails(inspection);
    }

    public async Task<InspectionDetailsResponse> UpdateAsync(
        Guid id,
        CreateInspectionRequest request,
        Guid operatorUserId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var inspection = await db.InspectionSessions.Include(x => x.Brand).Include(x => x.Model)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InspectionNotFoundException($"Inspection '{id}' was not found.");
        currentUser.EnsureCanAccessBrand(inspection.BrandId);
        if (inspection.Status is not (InspectionStatus.READY or InspectionStatus.ERROR))
            throw new InspectionConflictException("Inspection details cannot be changed after processing starts.");
        if (inspection.BrandId != request.BrandId || inspection.ModelId != request.ModelId)
            throw new InspectionConflictException("The selected model and profile versions are fixed when an inspection is created.");
        if (!await db.Users.AnyAsync(x => x.Id == operatorUserId, cancellationToken))
            throw new InspectionValidationException("The authenticated operator account is unavailable.");

        inspection.ProductionOrderNumber = TrimOrNull(request.ProductionOrderNumber);
        inspection.WorkOrderNumber = TrimOrNull(request.WorkOrderNumber);
        AddLog(inspection, operatorUserId, "UPDATED", "Inspection order metadata updated.", null, null, ipAddress);
        await db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<InspectionImageResponse> UploadImageAsync(
        Guid inspectionId,
        InspectionImageType imageType,
        IFormFile file,
        Guid operatorUserId,
        string? ipAddress,
        string requestId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[UPLOAD] request started; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} timeUtc={TimeUtc}",
            requestId, inspectionId, imageType, DateTimeOffset.UtcNow);
        var inspection = await db.InspectionSessions.AsNoTracking().Include(x => x.Images).Include(x => x.Model)
            .SingleOrDefaultAsync(x => x.Id == inspectionId, cancellationToken);
        if (inspection is null)
        {
            logger.LogWarning(
                "[UPLOAD] inspection not found; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType}",
                requestId, inspectionId, imageType);
            throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
        }
        if (!InspectionImageRequirements.OperatorUploadTypes.Contains(imageType))
            throw new InspectionValidationException(
                "Only Actual COO and Actual Config images can be uploaded. Master labels are managed by a Supervisor.");
        if (file.Length is <= 0 or > ImageFileInspector.MaximumFileSizeBytes)
            throw new InspectionValidationException("Images must be non-empty and 20 MB or smaller.");

        logger.LogInformation(
            "[UPLOAD] inspection loaded; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} status={Status} rowVersion={RowVersion} existingImageCount={ImageCount} timeUtc={TimeUtc}",
            requestId, inspectionId, imageType, inspection.Status,
            Convert.ToHexString(inspection.RowVersion), inspection.Images.Count, DateTimeOffset.UtcNow);
        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, cancellationToken);
        var bytes = stream.ToArray();
        var (contentType, width, height) = ImageFileInspector.Inspect(bytes, file.FileName, file.ContentType);
        var imageHash = Convert.ToHexString(SHA256.HashData(bytes));
        var existingImage = inspection.Images.SingleOrDefault(x => x.ImageType == imageType);
        logger.LogInformation(
            "[UPLOAD] image slot checked; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} status={Status} bytes={ByteCount} imagePresent={ImagePresent}",
            requestId, inspectionId, imageType, inspection.Status, bytes.Length, existingImage is not null);
        if (existingImage is not null)
        {
            if (string.Equals(existingImage.Sha256Hash, imageHash, StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "[UPLOAD] idempotent duplicate; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} imageId={ImageId}",
                    requestId, inspectionId, imageType, existingImage.Id);
                return ToImageResponse(existingImage);
            }
            logger.LogWarning(
                "[UPLOAD] slot occupied by a different image; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} existingImageId={ImageId}",
                requestId, inspectionId, imageType, existingImage.Id);
            throw new InspectionConflictException(
                $"A different image already exists for {imageType}. Remove it before uploading a replacement.");
        }
        if (inspection.Status is not (InspectionStatus.READY or InspectionStatus.ERROR))
        {
            logger.LogWarning(
                "[UPLOAD] inspection state rejects image upload; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} status={Status}",
                requestId, inspectionId, imageType, inspection.Status);
            throw new InspectionConflictException("Images can only be uploaded before processing starts.");
        }

        var storedFileName = imageType == InspectionImageType.ACTUAL_CO ? "ActualCO.jpg" : "ActualConfig.jpg";
        var relativePath = InspectionStoragePaths.ActualImagePath(inspection.Model.Code, inspectionId, imageType);
        var target = GetStoragePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            await output.WriteAsync(bytes, cancellationToken);
            await output.FlushAsync(cancellationToken);
        }

        try
        {
            var (labelType, role) = GetLabelParts(imageType);
            var image = new InspectionImage
            {
                InspectionSessionId = inspectionId,
                ImageType = imageType,
                LabelType = labelType,
                Role = role,
                OriginalFileName = SanitizeOriginalName(file.FileName),
                StoredFileName = storedFileName,
                StoragePath = relativePath,
                ContentType = contentType,
                SizeBytes = bytes.LongLength,
                Width = width,
                Height = height,
                Sha256Hash = imageHash,
                UploadedByUserId = operatorUserId,
                UploadedAtUtc = DateTimeOffset.UtcNow,
                ImageQualityStatus = "PENDING"
            };
            logger.LogInformation(
                "[UPLOAD] image insert started; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} rowVersion={RowVersion} timeUtc={TimeUtc}",
                requestId, inspectionId, imageType, Convert.ToHexString(inspection.RowVersion), DateTimeOffset.UtcNow);
            db.InspectionImages.Add(image);
            db.InspectionLogs.Add(new InspectionLog
            {
                InspectionSessionId = inspectionId,
                UserAccountId = operatorUserId,
                EventType = "IMAGE_UPLOADED",
                Details = $"Uploaded {imageType} image.",
                NewValue = image.Sha256Hash,
                IPAddress = ipAddress
            });
            try
            {
                logger.LogInformation(
                    "[UPLOAD] SaveChanges started; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} trackedStates={TrackedStates} timeUtc={TimeUtc}",
                    requestId, inspectionId, imageType,
                    string.Join(",", db.ChangeTracker.Entries()
                        .Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}")),
                    DateTimeOffset.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                await activityLog.RecordAsync("IMAGE_UPLOADED", $"Uploaded {imageType} image.", inspection.BrandId, inspectionId, image.Id, "InspectionImage", ipAddress, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "[UPLOAD] SaveChanges completed; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} rowVersion={RowVersion} timeUtc={TimeUtc}",
                    requestId, inspectionId, imageType, Convert.ToHexString(inspection.RowVersion), DateTimeOffset.UtcNow);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                logger.LogWarning(
                    exception,
                    "[UPLOAD] unexpected EF concurrency exception; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} entries={Entries}",
                    requestId, inspectionId, imageType,
                    string.Join(",", exception.Entries.Select(entry =>
                    {
                        var version = entry.Properties.SingleOrDefault(
                            property => property.Metadata.Name == nameof(InspectionSession.RowVersion));
                        var expected = version?.OriginalValue is byte[] original
                            ? Convert.ToHexString(original)
                            : "n/a";
                        var current = version?.CurrentValue is byte[] currentValue
                            ? Convert.ToHexString(currentValue)
                            : "n/a";
                        return $"{entry.Metadata.ClrType.Name}:{entry.State}:expected={expected}:current={current}";
                    })));
                throw;
            }
            catch (DbUpdateException exception)
            {
                logger.LogWarning(
                    exception,
                    "[UPLOAD] persistence failed; checking the unique image slot for a concurrent duplicate; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType}",
                    requestId, inspectionId, imageType);
                db.ChangeTracker.Clear();
                var currentImage = await db.InspectionImages.AsNoTracking()
                    .SingleOrDefaultAsync(
                        x => x.InspectionSessionId == inspectionId && x.ImageType == imageType,
                        cancellationToken);
                if (currentImage is null)
                    throw;
                if (!string.Equals(currentImage.Sha256Hash, imageHash, StringComparison.Ordinal))
                    throw new InspectionConflictException(
                        $"A different image already exists for {imageType}. Remove it before uploading a replacement.");
                File.Delete(target);
                logger.LogInformation(
                    "[UPLOAD] same-image unique-key race recovered; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} imageId={ImageId}",
                    requestId, inspectionId, imageType, currentImage.Id);
                return ToImageResponse(currentImage);
            }
            logger.LogInformation(
                "[UPLOAD] image insert completed; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} imageId={ImageId} timeUtc={TimeUtc}",
                requestId, inspectionId, imageType, image.Id, DateTimeOffset.UtcNow);
            return ToImageResponse(image);
        }
        catch
        {
            File.Delete(target);
            throw;
        }
    }

    public async Task<IReadOnlyList<InspectionImageResponse>> GetImagesAsync(
        Guid inspectionId,
        CancellationToken cancellationToken)
    {
        if (!await db.InspectionSessions.AnyAsync(x => x.Id == inspectionId, cancellationToken))
            throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
        return await db.InspectionImages.AsNoTracking().Where(x => x.InspectionSessionId == inspectionId)
            .OrderBy(x => x.ImageType).Select(x => new InspectionImageResponse(
                x.Id, x.ImageType, x.OriginalFileName, x.ContentType, x.SizeBytes, x.Width, x.Height,
                x.Sha256Hash, x.UploadedAtUtc, x.ImageQualityStatus)).ToListAsync(cancellationToken);
    }

    public async Task<InspectionImageContent> GetImageAsync(
        Guid inspectionId,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var image = await db.InspectionImages.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == imageId && x.InspectionSessionId == inspectionId, cancellationToken)
            ?? throw new InspectionNotFoundException("The image was not found for this inspection.");
        var path = GetStoragePath(image.StoragePath);
        if (!File.Exists(path)) throw new InspectionNotFoundException("The image file is missing from storage.");
        return new InspectionImageContent(
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true),
            image.ContentType,
            image.OriginalFileName);
    }

    public async Task<InspectionDetailsResponse> CorrectFieldAsync(
        Guid inspectionId,
        Guid imageId,
        Guid fieldDefinitionId,
        ManualFieldCorrectionRequest request,
        Guid operatorUserId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var inspection = await db.InspectionSessions.Include(x => x.Images).ThenInclude(x => x.OcrResults)
            .ThenInclude(x => x.ExtractedFields)
            .SingleOrDefaultAsync(x => x.Id == inspectionId, cancellationToken)
            ?? throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
        if (inspection.Status is not (InspectionStatus.REVIEW_REQUIRED or InspectionStatus.FAIL))
            throw new InspectionConflictException("Manual corrections are allowed only after an inspection requires review or fails.");
        if (!await db.Users.AnyAsync(x => x.Id == operatorUserId, cancellationToken))
            throw new InspectionValidationException("The authenticated operator account is unavailable.");

        var image = inspection.Images.SingleOrDefault(x => x.Id == imageId)
            ?? throw new InspectionNotFoundException("The image was not found for this inspection.");
        var expectedProfileId = image.LabelType == LabelType.CO
            ? inspection.LabelProfileCOId
            : inspection.LabelProfileConfigId;
        var definition = await db.FieldDefinitions.SingleOrDefaultAsync(
            x => x.Id == fieldDefinitionId && x.LabelProfileId == expectedProfileId,
            cancellationToken)
            ?? throw new InspectionValidationException("The field does not belong to the pinned profile for this image.");
        var extracted = image.OcrResults.OrderByDescending(x => x.CreatedAtUtc)
            .SelectMany(x => x.ExtractedFields)
            .FirstOrDefault(x => x.FieldDefinitionId == fieldDefinitionId)
            ?? throw new InspectionValidationException("No OCR extraction record exists for this field and image.");

        var previousValue = extracted.ExtractedValue;
        var correctedValue = request.Value.Trim();
        if (correctedValue.Length == 0)
            throw new InspectionValidationException("A corrected value cannot be empty.");
        extracted.ExtractedValue = correctedValue;
        extracted.NormalizedValue = FieldComparisonService.NormalizeForStorage(definition, correctedValue);
        extracted.Confidence = 1;
        extracted.IsManuallyCorrected = true;
        extracted.CorrectedByUserId = operatorUserId;
        extracted.CorrectionReason = request.Reason.Trim();
        AddLog(inspection, operatorUserId, "FIELD_CORRECTED",
            $"Manually corrected {definition.DisplayName} on {image.ImageType}. Reason: {request.Reason.Trim()}",
            previousValue, correctedValue, ipAddress);

        var coProfile = await db.LabelProfiles.AsNoTracking()
            .Include(x => x.FieldDefinitions).ThenInclude(x => x.Aliases)
            .SingleAsync(x => x.Id == inspection.LabelProfileCOId, cancellationToken);
        var configProfile = await db.LabelProfiles.AsNoTracking()
            .Include(x => x.FieldDefinitions).ThenInclude(x => x.Aliases)
            .SingleAsync(x => x.Id == inspection.LabelProfileConfigId, cancellationToken);
        var coFields = CoComparisonFieldSelector.Select(coProfile.FieldDefinitions);
        var configFields = ConfigComparisonFieldSelector.Select(configProfile.FieldDefinitions);
        var extractions = inspection.Images.ToDictionary(
            x => x.ImageType,
            x => BuildFieldExtractions(x, x.LabelType == LabelType.CO ? coFields : configFields));

        var coStage = CreateComparisonStage(
            inspection.Id, LabelType.CO, InspectionStageType.CO_COMPARISON, coFields,
            extractions[InspectionImageType.MASTER_CO], extractions[InspectionImageType.ACTUAL_CO],
            DateTimeOffset.UtcNow);
        var configStage = CreateComparisonStage(
            inspection.Id, LabelType.CONFIG, InspectionStageType.CONFIG_COMPARISON, configFields,
            extractions[InspectionImageType.MASTER_CONFIG], extractions[InspectionImageType.ACTUAL_CONFIG],
            DateTimeOffset.UtcNow);
        var serialStage = CoConfigValidationService.Create(
            inspection.Id, coFields, configFields,
            extractions[InspectionImageType.ACTUAL_CO], extractions[InspectionImageType.ACTUAL_CONFIG],
            DateTimeOffset.UtcNow,
            coStage.Status == InspectionStatus.PASS && configStage.Status == InspectionStatus.PASS,
            fieldComparisonService);
        var completedAtUtc = DateTimeOffset.UtcNow;
        coStage.CompletedAtUtc = completedAtUtc;
        configStage.CompletedAtUtc = completedAtUtc;
        serialStage.CompletedAtUtc = completedAtUtc;
        db.StageResults.AddRange(coStage, configStage, serialStage);
        inspection.COStatus = coStage.Status;
        inspection.ConfigStatus = configStage.Status;
        inspection.SerialStatus = serialStage.Status;
        var result = inspectionWorkflowService.EvaluateFinalStatus(
            coStage.Status, configStage.Status, serialStage.Status);
        inspection.Status = result.Status;
        inspection.CompletedAtUtc = completedAtUtc;
        inspection.FinalRemarks = result.Reason;
        AddLog(inspection, operatorUserId, "COMPARISONS_REASSESSED",
            "Inspection comparisons and final status were recalculated using saved OCR evidence and manual corrections.",
            null, result.Status.ToString(), ipAddress);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(inspectionId, cancellationToken);
    }

    public async Task DeleteImageAsync(
        Guid inspectionId,
        Guid imageId,
        Guid operatorUserId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var image = await db.InspectionImages.Include(x => x.InspectionSession)
            .SingleOrDefaultAsync(x => x.Id == imageId && x.InspectionSessionId == inspectionId, cancellationToken)
            ?? throw new InspectionNotFoundException("The image was not found for this inspection.");
        if (image.Role == ImageRole.MASTER)
            throw new InspectionConflictException(
                "Saved master label images cannot be removed from an inspection.");
        var inspection = image.InspectionSession;
        if (inspection.Status is not (InspectionStatus.READY or InspectionStatus.ERROR))
            throw new InspectionConflictException("Images cannot be deleted after processing starts.");
        var path = GetStoragePath(image.StoragePath);
        inspection.Status = InspectionStatus.READY;
        db.InspectionImages.Remove(image);
        AddLog(inspection, operatorUserId, "IMAGE_DELETED", $"Deleted {image.ImageType} image.",
            image.Sha256Hash, null, ipAddress);
        await db.SaveChangesAsync(cancellationToken);
        if (File.Exists(path)) File.Delete(path);
    }

    public async Task<InspectionStatus> ValidateAsync(
        Guid inspectionId,
        Guid operatorUserId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var inspection = await db.InspectionSessions.AsNoTracking().Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == inspectionId, cancellationToken)
            ?? throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
        EnsureAllImages(inspection);
        if (inspection.Status is not (InspectionStatus.READY or InspectionStatus.ERROR))
            throw new InspectionConflictException($"Inspection in {inspection.Status} state cannot be validated.");

        if (inspection.Status == InspectionStatus.ERROR)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var transitioned = await db.InspectionSessions
                .Where(x => x.Id == inspectionId
                    && x.Status == InspectionStatus.ERROR
                    && x.RowVersion == inspection.RowVersion)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.Status, InspectionStatus.READY),
                    cancellationToken);

            if (transitioned == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                var currentStatus = await db.InspectionSessions.AsNoTracking()
                    .Where(x => x.Id == inspectionId)
                    .Select(x => (InspectionStatus?)x.Status)
                    .SingleOrDefaultAsync(cancellationToken);
                if (currentStatus is null)
                    throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
                if (currentStatus == InspectionStatus.READY)
                    return InspectionStatus.READY;
                throw new InspectionConflictException(
                    $"Inspection changed to {currentStatus} while validation was in progress.");
            }

            db.InspectionLogs.Add(new InspectionLog
            {
                InspectionSessionId = inspectionId,
                UserAccountId = operatorUserId,
                EventType = "VALIDATED",
                Details = "All four required label images are present.",
                IPAddress = ipAddress
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InspectionStatus.READY;
        }

        db.InspectionLogs.Add(new InspectionLog
        {
            InspectionSessionId = inspectionId,
            UserAccountId = operatorUserId,
            EventType = "VALIDATED",
            Details = "All four required label images are present.",
            IPAddress = ipAddress
        });
        await db.SaveChangesAsync(cancellationToken);
        return InspectionStatus.READY;
    }

    public async Task<InspectionStatus> ProcessAsync(
        Guid inspectionId,
        Guid operatorUserId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "PROCESS_START inspectionId={InspectionId} timeUtc={TimeUtc}",
            inspectionId, DateTimeOffset.UtcNow);
        var inspection = await db.InspectionSessions.AsNoTracking().Include(x => x.Images)
            .ThenInclude(x => x.OcrResults).ThenInclude(x => x.ExtractedFields)
            .SingleOrDefaultAsync(x => x.Id == inspectionId, cancellationToken)
            ?? throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
        logger.LogInformation(
            "PROCESS_STATE_CHECK inspectionId={InspectionId} status={Status} rowVersion={RowVersion} imageCount={ImageCount}",
            inspectionId, inspection.Status, Convert.ToHexString(inspection.RowVersion), inspection.Images.Count);
        if (inspection.Status != InspectionStatus.READY)
        {
            if (inspection.Status == InspectionStatus.PROCESSING)
            {
                logger.LogWarning(
                    "PROCESSING_STATE_CONFLICT inspectionId={InspectionId} status={Status} reason={Reason}",
                    inspectionId, inspection.Status, "already processing");
                throw new InspectionConflictException("Inspection is already being processed.");
            }
            throw new InspectionConflictException("Validate the complete four-image set before processing.");
        }
        EnsureAllImages(inspection);

        var startedAtUtc = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        logger.LogInformation(
            "PROCESSING_STATE_TRANSITION inspectionId={InspectionId} fromStatus={CurrentStatus} toStatus={IntendedStatus} rowVersion={RowVersion} stage={Stage}",
            inspectionId, inspection.Status, InspectionStatus.PROCESSING,
            Convert.ToHexString(inspection.RowVersion), "PROCESSING_STATUS_SAVE");
        var transitioned = await db.InspectionSessions
            .Where(x => x.Id == inspectionId
                && x.Status == InspectionStatus.READY
                && x.RowVersion == inspection.RowVersion)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Status, InspectionStatus.PROCESSING),
                cancellationToken);
        if (transitioned == 0)
        {
            var currentStatus = await db.InspectionSessions.AsNoTracking()
                .Where(x => x.Id == inspectionId)
                .Select(x => (InspectionStatus?)x.Status)
                .SingleOrDefaultAsync(cancellationToken);
            if (currentStatus is null)
                throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
            if (currentStatus == InspectionStatus.PROCESSING)
            {
                logger.LogWarning(
                    "PROCESSING_STATE_CONFLICT inspectionId={InspectionId} status={Status} reason={Reason}",
                    inspectionId, currentStatus, "another request acquired the processing state");
                throw new InspectionConflictException("Inspection is already being processed.");
            }
            throw new InspectionConflictException(
                $"Inspection changed to {currentStatus} before processing could start. Refresh and retry.");
        }

        var processingRowVersion = await db.InspectionSessions.AsNoTracking()
            .Where(x => x.Id == inspectionId)
            .Select(x => x.RowVersion)
            .SingleAsync(cancellationToken);
        inspection.RowVersion = processingRowVersion;
        inspection.Images = await db.InspectionImages.AsNoTracking()
            .Include(x => x.OcrResults).ThenInclude(x => x.ExtractedFields)
            .Where(x => x.InspectionSessionId == inspectionId)
            .OrderBy(x => x.ImageType)
            .ToListAsync(cancellationToken);
        EnsureAllImages(inspection);

        var job = await db.InspectionProcessingJobs
            .SingleOrDefaultAsync(x => x.InspectionSessionId == inspectionId, cancellationToken);
        if (job is null)
        {
            job = new InspectionProcessingJob { InspectionSessionId = inspectionId, AttemptCount = 0 };
            db.InspectionProcessingJobs.Add(job);
        }
        job.Status = "PROCESSING";
        job.AttemptCount++;
        job.StartedAtUtc = startedAtUtc;
        job.CompletedAtUtc = null;
        job.LastError = null;
        db.InspectionLogs.Add(new InspectionLog
        {
            InspectionSessionId = inspectionId,
            UserAccountId = operatorUserId,
            EventType = "PROCESSING_STARTED",
            Details = "OCR evidence processing started.",
            IPAddress = ipAddress
        });
        await SaveProcessChangesAsync(
            "PROCESSING_STATUS_SAVE", inspectionId, InspectionStatus.PROCESSING, cancellationToken);
        await activityLog.RecordAsync("OCR_PROCESSING_STARTED", "OCR processing started.", inspection.BrandId, inspectionId, entityType: "Inspection", ipAddress: ipAddress, cancellationToken: cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "PROCESSING_STATE_SAVE_SUCCESS inspectionId={InspectionId} status={Status} attempt={AttemptCount} rowVersion={RowVersion}",
            inspectionId, InspectionStatus.PROCESSING, job.AttemptCount, Convert.ToHexString(inspection.RowVersion));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient("OcrService");
            var ocrByImageType = new Dictionary<InspectionImageType, (OcrResult Result, OcrServiceResponse Response)>();
            var globalThresholdValue = await db.SystemSettings.AsNoTracking()
                .Where(x => x.SettingKey == "OcrMinimumConfidence")
                .Select(x => x.SettingValue)
                .SingleOrDefaultAsync(cancellationToken);
            if (!double.TryParse(
                    globalThresholdValue, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var globalThreshold)
                || !double.IsFinite(globalThreshold)
                || globalThreshold is < 0 or > 1)
                throw new InspectionProcessingException("The configured global OCR confidence threshold is invalid.");
            var profileIds = new[] { inspection.LabelProfileCOId, inspection.LabelProfileConfigId };
            var profiles = await db.LabelProfiles.AsNoTracking()
                .Include(x => x.FieldDefinitions).ThenInclude(x => x.Aliases)
                .Where(x => profileIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
            var coProfile = profiles.SingleOrDefault(x => x.Id == inspection.LabelProfileCOId)
                ?? throw new InspectionProcessingException("The inspection's pinned COO profile is unavailable.");
            var configProfile = profiles.SingleOrDefault(x => x.Id == inspection.LabelProfileConfigId)
                ?? throw new InspectionProcessingException("The inspection's pinned CONFIG profile is unavailable.");
            var coFields = CoComparisonFieldSelector.Select(coProfile.FieldDefinitions);
            var configFields = ConfigComparisonFieldSelector.Select(configProfile.FieldDefinitions);

            foreach (var image in inspection.Images
                         .Where(x => x.ImageType is InspectionImageType.ACTUAL_CO or InspectionImageType.ACTUAL_CONFIG)
                         .OrderBy(x => x.ImageType))
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogInformation(
                    "IMAGE_PROCESS_START inspectionId={InspectionId} imageId={ImageId} imageType={ImageType} stage={Stage}",
                    inspectionId, image.Id, image.ImageType, "IMAGE_PROCESS_START");
                var path = GetStoragePath(image.StoragePath);
                if (!File.Exists(path)) throw new FileNotFoundException("A stored inspection image is missing.", path);
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                using var content = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(bytes);
                imageContent.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
                content.Add(imageContent, "file", image.OriginalFileName);
                var fieldDefinitions = image.ImageType == InspectionImageType.ACTUAL_CO
                    ? coFields
                    : configFields;
                var fieldConfig = fieldDefinitions.Select(x => new
                {
                    field_name = x.FieldName,
                    display_name = x.DisplayName,
                    aliases = x.Aliases.Where(alias => alias.IsActive).Select(alias => alias.Value).ToArray(),
                    confidence_threshold = x.OcrConfidenceThreshold
                }).ToArray();
                content.Add(new StringContent(JsonSerializer.Serialize(fieldConfig)), "field_config");
                content.Add(new StringContent(image.LabelType.ToString()), "label_type");
                content.Add(new StringContent(globalThreshold.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)), "minimum_confidence");
                logger.LogInformation(
                    "OCR_REQUEST_START inspectionId={InspectionId} imageId={ImageId} imageType={ImageType} endpoint={Endpoint}",
                    inspectionId, image.Id, image.ImageType, "ocr/extract");
                using var response = await client.PostAsync("ocr/extract", content, cancellationToken);
                logger.LogInformation(
                    "OCR_REQUEST_RESPONSE inspectionId={InspectionId} imageId={ImageId} imageType={ImageType} httpStatus={HttpStatus} success={Success}",
                    inspectionId, image.Id, image.ImageType, (int)response.StatusCode, response.IsSuccessStatusCode);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await ReadOcrServiceErrorAsync(response, cancellationToken);
                    throw new InspectionProcessingException(
                        $"OCR service returned HTTP {(int)response.StatusCode} for {image.ImageType}: {error}");
                }

                var result = await response.Content.ReadFromJsonAsync<OcrServiceResponse>(cancellationToken: cancellationToken)
                    ?? throw new InspectionProcessingException("OCR service returned an empty response.");
                logger.LogInformation(
                    "OCR_REQUEST_SUCCESS inspectionId={InspectionId} imageId={ImageId} imageType={ImageType} engine={Engine} status={OcrStatus} confidence={Confidence} candidateCount={CandidateCount}",
                    inspectionId, image.Id, image.ImageType, result.Engine, result.Status, result.OcrConfidence,
                    result.FieldCandidates?.Count ?? 0);
                OcrBarcodeResponse? barcodeResult = null;
                if (image.LabelType == LabelType.CONFIG)
                {
                    using var barcodeContent = new MultipartFormDataContent();
                    var barcodeImageContent = new ByteArrayContent(bytes);
                    barcodeImageContent.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
                    barcodeContent.Add(barcodeImageContent, "file", image.OriginalFileName);
                    using var barcodeResponse = await client.PostAsync("ocr/barcode", barcodeContent, cancellationToken);
                    if (!barcodeResponse.IsSuccessStatusCode)
                    {
                        var error = await ReadOcrServiceErrorAsync(barcodeResponse, cancellationToken);
                        throw new InspectionProcessingException(
                            $"OCR barcode service returned HTTP {(int)barcodeResponse.StatusCode} for {image.ImageType}: {error}");
                    }
                    barcodeResult = await barcodeResponse.Content.ReadFromJsonAsync<OcrBarcodeResponse>(
                        cancellationToken: cancellationToken)
                        ?? throw new InspectionProcessingException("OCR barcode service returned an empty response.");
                }
                if (result.Status is not ("PROCESSING" or "REVIEW_REQUIRED")
                    || result.ImageQuality is null
                    || result.ImageQuality.Width <= 0
                    || result.ImageQuality.Height <= 0
                    || result.Lines is null
                    || string.IsNullOrWhiteSpace(result.Engine)
                    || string.IsNullOrWhiteSpace(result.EngineVersion)
                    || result.RawText is { Length: > 20000 }
                    || result.OcrConfidence is { } ocrConfidence
                        && (!double.IsFinite(ocrConfidence) || ocrConfidence is < 0 or > 1)
                    || result.Lines.Any(line => !double.IsFinite(line.Confidence)
                        || line.Confidence is < 0 or > 1)
                    || result.FieldCandidates?.Any(candidate =>
                        string.IsNullOrWhiteSpace(candidate.FieldName)
                        || !double.IsFinite(candidate.Confidence)
                        || candidate.Confidence is < 0 or > 1
                        || candidate.Status is not ("DETECTED" or "AMBIGUOUS" or "NOT_FOUND")) == true)
                {
                    throw new InspectionProcessingException("OCR service returned an invalid response.");
                }

                if (image.LabelType == LabelType.CO)
                {
                    var configuredCoKeys = coFields
                        .Select(field => field.FieldName)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var coCandidates = (result.FieldCandidates ?? [])
                        .Where(candidate => configuredCoKeys.Contains(candidate.FieldName))
                        .Select(candidate => candidate with { Debug = null })
                        .ToList();
                    if (coCandidates.Count != configuredCoKeys.Count
                        || coCandidates.Select(candidate => candidate.FieldName)
                            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuredCoKeys.Count)
                    {
                        throw new InspectionProcessingException(
                            "COO OCR must return one candidate for each configured Model No., QR Code, P/N, and R Number field.");
                    }

                    result = result with
                    {
                        FieldCandidates = coCandidates,
                        OcrConfidence = coCandidates.Average(candidate => candidate.Confidence)
                    };
                }

                var rawText = result.RawText ?? string.Join(Environment.NewLine, result.Lines.Select(x => x.Text));
                if (rawText.Length > 20000)
                    throw new InspectionProcessingException($"OCR text for {image.ImageType} exceeds the supported size.");
                double? confidence = result.OcrConfidence
                    ?? (result.Lines.Count == 0 ? null : result.Lines.Average(x => x.Confidence));
                var imageNeedsReview = result.Status == "REVIEW_REQUIRED"
                    || result.Lines.Count == 0 && (result.FieldCandidates?.Count ?? 0) == 0;
                image.ImageQualityStatus = imageNeedsReview ? "REVIEW_REQUIRED" : "ACCEPTED";
                var ocrResult = new OcrResult
                {
                    InspectionSessionId = inspectionId,
                    InspectionImageId = image.Id,
                    Engine = result.Engine,
                    EngineVersion = result.EngineVersion,
                    Status = imageNeedsReview ? InspectionStatus.REVIEW_REQUIRED : InspectionStatus.PASS,
                    RawExtractedText = rawText,
                    ExtractedFieldsJson = JsonSerializer.Serialize(new
                    {
                        rawText,
                        ocrConfidence = confidence,
                        jobId = result.JobId,
                        artifacts = result.Artifacts,
                        lines = result.Lines,
                        configuredFieldCandidates = (result.FieldCandidates ?? []).Select(candidate => new
                        {
                            fieldName = candidate.FieldName,
                            value = candidate.Value,
                            confidence = candidate.Confidence,
                            box = candidate.Box,
                            status = candidate.Status,
                            reason = candidate.Reason,
                            debug = candidate.Debug
                        }).ToArray(),
                        barcodes = barcodeResult?.Barcodes ?? [],
                        warnings = (result.Warnings ?? []).Concat(barcodeResult?.Warnings ?? []).ToArray(),
                        imageQuality = result.ImageQuality,
                        processingDurationMs = result.ProcessingDurationMs + (barcodeResult?.ProcessingDurationMs ?? 0)
                    }),
                    OverallConfidence = confidence,
                    ProcessingDurationMs = result.ProcessingDurationMs + (barcodeResult?.ProcessingDurationMs ?? 0)
                };
                db.OcrResults.Add(ocrResult);
                await activityLog.RecordAsync("OCR_RESULT", $"OCR result saved for {image.ImageType}: {ocrResult.Status}.", inspection.BrandId, inspectionId, image.Id, "InspectionImage", ipAddress, cancellationToken);
                logger.LogInformation(
                    "IMAGE_RESULT_SAVE inspectionId={InspectionId} imageId={ImageId} imageType={ImageType} resultStatus={ResultStatus} persistence=FINAL_STATUS_SAVE",
                    inspectionId, image.Id, image.ImageType, ocrResult.Status);
                ocrByImageType.Add(image.ImageType, (ocrResult, result));
            }

            var coMaster = BuildFieldExtractions(
                inspection.Images.Single(x => x.ImageType == InspectionImageType.MASTER_CO), coFields);
            var coActual = ExtractConfiguredFields(
                coFields, ocrByImageType[InspectionImageType.ACTUAL_CO].Result,
                ocrByImageType[InspectionImageType.ACTUAL_CO].Response);
            var configMaster = BuildFieldExtractions(
                inspection.Images.Single(x => x.ImageType == InspectionImageType.MASTER_CONFIG), configFields);
            var configActual = ExtractConfiguredFields(
                configFields, ocrByImageType[InspectionImageType.ACTUAL_CONFIG].Result,
                ocrByImageType[InspectionImageType.ACTUAL_CONFIG].Response);

            var coStage = CreateComparisonStage(
                inspection.Id, LabelType.CO, InspectionStageType.CO_COMPARISON, coFields,
                coMaster, coActual, job.StartedAtUtc);
            var configStage = CreateComparisonStage(
                inspection.Id, LabelType.CONFIG, InspectionStageType.CONFIG_COMPARISON, configFields,
                configMaster, configActual, job.StartedAtUtc);
            var serialStage = CoConfigValidationService.Create(
                inspection.Id, coFields, configFields, coActual, configActual, job.StartedAtUtc,
                coStage.Status == InspectionStatus.PASS && configStage.Status == InspectionStatus.PASS,
                fieldComparisonService);
            db.StageResults.AddRange(coStage, configStage, serialStage);

            var now = DateTimeOffset.UtcNow;
            coStage.CompletedAtUtc = now;
            configStage.CompletedAtUtc = now;
            serialStage.CompletedAtUtc = now;
            var finalAssessment = inspectionWorkflowService.EvaluateFinalStatus(
                coStage.Status, configStage.Status, serialStage.Status);
            var finalInspection = await db.InspectionSessions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == inspectionId, cancellationToken)
                ?? throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
            logger.LogInformation(
                "FINAL_STATUS_SAVE inspectionId={InspectionId} currentStatus={CurrentStatus} currentRowVersion={RowVersion} intendedStatus={IntendedStatus}",
                inspectionId, finalInspection.Status, Convert.ToHexString(finalInspection.RowVersion), finalAssessment.Status);
            if (finalInspection.Status != InspectionStatus.PROCESSING)
            {
                throw new InspectionConflictException(
                    $"Processing cannot be completed because the inspection is {finalInspection.Status}.");
            }
            var comparisonPath = await InspectionComparisonArtifactWriter.WriteAsync(
                _storageRoot,
                inspection.Id,
                finalAssessment.Status,
                coStage.FieldComparisons.Concat(configStage.FieldComparisons),
                cancellationToken);
            foreach (var comparison in coStage.FieldComparisons.Concat(configStage.FieldComparisons))
            {
                logger.LogInformation(
                    "Configured field comparison completed; inspection={InspectionId} field={FieldKey} result={Result}",
                    inspectionId, comparison.FieldKey, comparison.Status);
            }
            logger.LogInformation(
                "Inspection comparison completed; inspection={InspectionId} result={Result} fieldCount={FieldCount} artifact={ArtifactPath}",
                inspectionId, finalAssessment.Status,
                coStage.FieldComparisons.Count + configStage.FieldComparisons.Count,
                comparisonPath);

            await using var finalizationTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var finalized = await db.InspectionSessions
                .Where(x => x.Id == inspectionId
                    && x.Status == InspectionStatus.PROCESSING
                    && x.RowVersion == finalInspection.RowVersion)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.COStatus, coStage.Status)
                        .SetProperty(x => x.ConfigStatus, configStage.Status)
                        .SetProperty(x => x.SerialStatus, serialStage.Status)
                        .SetProperty(x => x.Status, finalAssessment.Status)
                        .SetProperty(x => x.CompletedAtUtc, (DateTimeOffset?)now)
                        .SetProperty(x => x.ProcessingDurationMs, (long?)stopwatch.ElapsedMilliseconds)
                        .SetProperty(x => x.FinalRemarks, finalAssessment.Reason),
                    cancellationToken);
            if (finalized == 0)
            {
                var latest = await db.InspectionSessions.AsNoTracking()
                    .Where(x => x.Id == inspectionId)
                    .Select(x => new { x.Status, x.RowVersion })
                    .SingleOrDefaultAsync(cancellationToken);
                logger.LogError(
                    "PROCESS_FINALIZATION_CONFLICT inspectionId={InspectionId} expectedRowVersion={ExpectedRowVersion} currentRowVersion={CurrentRowVersion} currentStatus={CurrentStatus}",
                    inspectionId, Convert.ToHexString(finalInspection.RowVersion),
                    latest is null ? "n/a" : Convert.ToHexString(latest.RowVersion),
                    latest?.Status.ToString() ?? "NOT_FOUND");
                if (latest is null)
                    throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
                throw new InspectionConflictException(
                    latest.Status == InspectionStatus.PROCESSING
                        ? "Inspection changed while processing was being finalized. Refresh and retry."
                        : $"Processing cannot be completed because the inspection is {latest.Status}.");
            }

            job.Status = "COMPLETED";
            job.CompletedAtUtc = now;
            db.InspectionLogs.Add(new InspectionLog
            {
                InspectionSessionId = inspectionId,
                UserAccountId = operatorUserId,
                EventType = "PROCESSING_COMPLETED",
                Details = finalAssessment.Reason ?? "Inspection processing completed.",
                IPAddress = ipAddress
            });
            await SaveProcessChangesAsync(
                "FINAL_STATUS_SAVE", inspectionId, finalAssessment.Status, cancellationToken);
            await finalizationTransaction.CommitAsync(cancellationToken);
            await activityLog.RecordAsync("INSPECTION_COMPLETED", $"Inspection completed with status {finalAssessment.Status}.", finalInspection.BrandId, inspectionId, entityType: "Inspection", ipAddress: ipAddress, cancellationToken: cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            var finalRowVersion = await db.InspectionSessions.AsNoTracking()
                .Where(x => x.Id == inspectionId)
                .Select(x => x.RowVersion)
                .SingleAsync(cancellationToken);
            logger.LogInformation(
                "PROCESS_COMPLETED inspectionId={InspectionId} finalStatus={FinalStatus} rowVersion={RowVersion} durationMs={DurationMs}",
                inspectionId, finalAssessment.Status, Convert.ToHexString(finalRowVersion),
                stopwatch.ElapsedMilliseconds);
            return finalAssessment.Status;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "PROCESS_FAILED inspectionId={InspectionId} stage={Stage} processStartRowVersion={RowVersion} exceptionType={ExceptionType}",
                inspectionId, "OCR_OR_COMPARISON", Convert.ToHexString(inspection.RowVersion),
                exception.GetType().Name);
            try
            {
                await SaveFailedProcessStateAsync(
                    inspectionId, operatorUserId, ipAddress, exception, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception stateSaveException)
            {
                logger.LogError(
                    stateSaveException,
                    "PROCESS_FAILED_STATE_SAVE_FAILED inspectionId={InspectionId} originalExceptionType={OriginalExceptionType}",
                    inspectionId, exception.GetType().Name);
            }

            if ((exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
                || exception is InspectionConflictException
                || exception is InspectionNotFoundException)
                throw;

            throw new InspectionProcessingException("OCR processing failed. The saved inspection remains available for retry.", exception);
        }
    }

    private async Task SaveFailedProcessStateAsync(
        Guid inspectionId,
        Guid operatorUserId,
        string? ipAddress,
        Exception exception,
        long processingDurationMs)
    {
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cleanupToken = cleanupTimeout.Token;
        db.ChangeTracker.Clear();

        var failedInspection = await db.InspectionSessions
            .SingleOrDefaultAsync(x => x.Id == inspectionId, cleanupToken)
            ?? throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
        var job = await db.InspectionProcessingJobs
            .SingleOrDefaultAsync(x => x.InspectionSessionId == inspectionId, cleanupToken)
            ?? throw new InspectionProcessingException("The inspection processing job record is unavailable.");
        var stateChanged = false;
        var now = DateTimeOffset.UtcNow;

        if (job.Status == "PROCESSING")
        {
            job.Status = "FAILED";
            job.LastError = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
            job.CompletedAtUtc = now;
            stateChanged = true;
        }

        if (failedInspection.Status == InspectionStatus.PROCESSING)
        {
            failedInspection.Status = InspectionStatus.ERROR;
            failedInspection.ProcessingDurationMs = processingDurationMs;
            failedInspection.CompletedAtUtc = null;
            failedInspection.FinalRemarks = "Processing could not complete; the inspection requires retry or manual review.";
            stateChanged = true;
        }

        if (!stateChanged)
        {
            logger.LogWarning(
                "PROCESS_FAILED_STATE_ALREADY_TERMINAL inspectionId={InspectionId} inspectionStatus={InspectionStatus} jobStatus={JobStatus}",
                inspectionId, failedInspection.Status, job.Status);
            return;
        }

        db.InspectionLogs.Add(new InspectionLog
        {
            InspectionSessionId = inspectionId,
            UserAccountId = operatorUserId,
            EventType = "PROCESSING_FAILED",
            Details = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message,
            IPAddress = ipAddress
        });
        await db.SaveChangesAsync(cleanupToken);
        logger.LogInformation(
            "PROCESS_FAILED_STATE_SAVE_SUCCESS inspectionId={InspectionId} inspectionStatus={InspectionStatus} jobStatus={JobStatus}",
            inspectionId, failedInspection.Status, job.Status);
    }

    private async Task SaveProcessChangesAsync(
        string stage,
        Guid inspectionId,
        InspectionStatus status,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "{ProcessStage} inspectionId={InspectionId} currentStatus={Status} trackedStates={TrackedStates}",
            stage, inspectionId, status,
            string.Join(",", db.ChangeTracker.Entries()
                .Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}")));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var rowVersion = await db.InspectionSessions.AsNoTracking()
                .Where(x => x.Id == inspectionId)
                .Select(x => x.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);
            logger.LogInformation(
                "{ProcessStage}_SUCCESS inspectionId={InspectionId} status={Status} rowVersion={RowVersion}",
                stage, inspectionId, status,
                rowVersion is null ? "n/a" : Convert.ToHexString(rowVersion));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await LogProcessConcurrencyFailureAsync(exception, stage, inspectionId, cancellationToken);
            throw;
        }
    }

    private async Task LogProcessConcurrencyFailureAsync(
        DbUpdateConcurrencyException exception,
        string stage,
        Guid inspectionId,
        CancellationToken cancellationToken)
    {
        foreach (var entry in exception.Entries)
        {
            var rowVersion = entry.Properties.SingleOrDefault(
                property => property.Metadata.Name == nameof(InspectionSession.RowVersion));
            var expected = rowVersion?.OriginalValue is byte[] original
                ? Convert.ToHexString(original)
                : "n/a";
            var current = rowVersion?.CurrentValue is byte[] currentValue
                ? Convert.ToHexString(currentValue)
                : "n/a";
            var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
            var databaseVersionProperty = databaseValues?.Properties.SingleOrDefault(
                property => property.Name == nameof(InspectionSession.RowVersion));
            var databaseStatusProperty = databaseValues?.Properties.SingleOrDefault(
                property => property.Name == nameof(InspectionSession.Status));
            var databaseVersion = databaseVersionProperty is null
                ? null
                : databaseValues![databaseVersionProperty] as byte[];
            var databaseStatus = databaseStatusProperty is null
                ? null
                : databaseValues![databaseStatusProperty] as InspectionStatus?;
            logger.LogError(
                exception,
                "PROCESS_CONCURRENCY_FAILURE inspectionId={InspectionId} stage={Stage} entityType={EntityType} entityState={EntityState} expectedRowVersion={ExpectedRowVersion} currentRowVersion={CurrentRowVersion} databaseRowVersion={DatabaseRowVersion} databaseStatus={DatabaseStatus}",
                inspectionId, stage, entry.Metadata.ClrType.Name, entry.State, expected, current,
                databaseVersion is null ? "n/a" : Convert.ToHexString(databaseVersion),
                databaseStatus?.ToString() ?? "n/a");
        }
    }

    public async Task CancelAsync(Guid inspectionId, Guid operatorUserId, string? ipAddress, CancellationToken cancellationToken)
    {
        var inspection = await db.InspectionSessions.SingleOrDefaultAsync(x => x.Id == inspectionId, cancellationToken)
            ?? throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
        if (inspection.Status is InspectionStatus.PASS or InspectionStatus.FAIL
            or InspectionStatus.CANCELLED or InspectionStatus.PROCESSING)
            throw new InspectionConflictException("Completed or processing inspections cannot be cancelled.");
        var previousStatus = inspection.Status.ToString();
        inspection.Status = InspectionStatus.CANCELLED;
        inspection.CompletedAtUtc = DateTimeOffset.UtcNow;
        AddLog(inspection, operatorUserId, "CANCELLED", "Inspection cancelled by operator.",
            previousStatus, InspectionStatus.CANCELLED.ToString(), ipAddress);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InspectionLogResponse>> GetLogsAsync(Guid inspectionId, CancellationToken cancellationToken)
    {
        if (!await db.InspectionSessions.AnyAsync(x => x.Id == inspectionId, cancellationToken))
            throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
        return await db.InspectionLogs.AsNoTracking().Where(x => x.InspectionSessionId == inspectionId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new InspectionLogResponse(x.Id, x.UserAccountId, x.EventType, x.Details,
                x.PreviousValue, x.NewValue, x.IPAddress, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private Dictionary<Guid, ConfiguredFieldExtraction> ExtractConfiguredFields(
        IReadOnlyList<FieldDefinition> definitions,
        OcrResult ocrResult,
        OcrServiceResponse response)
    {
        var extracted = new Dictionary<Guid, ConfiguredFieldExtraction>();
        foreach (var definition in definitions)
        {
            var result = ConfiguredFieldExtractionService.Extract(
                definition, response.Lines, response.FieldCandidates);

            var observation = result.Observation;
            db.ExtractedFields.Add(new ExtractedField
            {
                OcrResultId = ocrResult.Id,
                FieldDefinitionId = definition.Id,
                FieldKey = definition.FieldName,
                ExtractedValue = observation?.Value,
                NormalizedValue = observation is null
                    ? null
                    : FieldComparisonService.NormalizeForStorage(definition, observation.Value!),
                Confidence = observation?.Confidence ?? 0,
                BoundingBoxJson = result.BoundingBoxJson
            });
            extracted.Add(definition.Id, result);
        }

        return extracted;
    }

    private void EnsureMasterProfile(
        MasterLabel master,
        LabelProfile profile,
        IReadOnlyList<FieldDefinition> definitions)
    {
        if (master.LabelProfileId != profile.Id)
            throw new InspectionConflictException(
                $"The saved Master {master.LabelType} label uses an older profile. Ask a Supervisor to replace it.");

        var savedKeys = master.Fields.Select(field => field.FieldKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (savedKeys.Count != definitions.Count
            || definitions.Any(definition => !savedKeys.Contains(definition.FieldName)))
            throw new InspectionConflictException(
                $"The saved Master {master.LabelType} fields do not match the active profile. Ask a Supervisor to replace it.");

        if (!File.Exists(GetStoragePath(master.StoragePath)))
            throw new InspectionConflictException(
                $"The saved Master {master.LabelType} image is unavailable. Ask a Supervisor to replace it.");
    }

    private async Task<InspectionImage> CopyMasterToInspectionAsync(
        InspectionSession inspection,
        MasterLabel master,
        IReadOnlyList<FieldDefinition> definitions,
        Guid userId,
        ICollection<string> copiedPaths,
        CancellationToken cancellationToken)
    {
        var storedFileName = master.StoredFileName;
        var relativePath = master.StoragePath;

        var imageType = master.LabelType == LabelType.CO
            ? InspectionImageType.MASTER_CO
            : InspectionImageType.MASTER_CONFIG;
        var uploadedAtUtc = DateTimeOffset.UtcNow;
        var image = new InspectionImage
        {
            InspectionSessionId = inspection.Id,
            ImageType = imageType,
            LabelType = master.LabelType,
            Role = ImageRole.MASTER,
            OriginalFileName = master.OriginalFileName,
            StoredFileName = storedFileName,
            StoragePath = relativePath,
            Sha256Hash = master.Sha256Hash,
            ContentType = master.ContentType,
            SizeBytes = master.SizeBytes,
            Width = master.Width,
            Height = master.Height,
            UploadedByUserId = userId,
            UploadedAtUtc = uploadedAtUtc,
            ImageQualityStatus = master.ImageQualityStatus
        };
        var savedFields = master.Fields.ToDictionary(
            field => field.FieldKey, StringComparer.OrdinalIgnoreCase);
        var ocrResult = new OcrResult
        {
            InspectionImageId = image.Id,
            InspectionSessionId = inspection.Id,
            Engine = master.Engine,
            EngineVersion = master.EngineVersion,
            Status = master.OcrStatus,
            RawExtractedText = master.RawExtractedText,
            ExtractedFieldsJson = master.OcrDataJson,
            OverallConfidence = master.OverallConfidence,
            ProcessingDurationMs = master.ProcessingDurationMs,
            CreatedAtUtc = uploadedAtUtc
        };
        foreach (var definition in definitions)
        {
            var field = savedFields[definition.FieldName];
            ocrResult.ExtractedFields.Add(new ExtractedField
            {
                OcrResultId = ocrResult.Id,
                FieldDefinitionId = definition.Id,
                FieldKey = field.FieldKey,
                ExtractedValue = field.ExtractedValue,
                NormalizedValue = field.NormalizedValue,
                Confidence = field.Confidence,
                BoundingBoxJson = field.BoundingBoxJson,
                CreatedAtUtc = uploadedAtUtc
            });
        }
        image.OcrResults.Add(ocrResult);
        return image;
    }

    private void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Could not remove a failed inspection master image copy");
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception, "Could not remove a failed inspection master image copy");
        }
    }

    private static Dictionary<Guid, ConfiguredFieldExtraction> BuildFieldExtractions(
        InspectionImage image,
        IReadOnlyList<FieldDefinition> definitions)
    {
        var latestOcr = image.OcrResults.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
        return definitions.ToDictionary(definition => definition.Id, definition =>
        {
            var field = latestOcr?.ExtractedFields.FirstOrDefault(x => x.FieldDefinitionId == definition.Id);
            if (field is null || string.IsNullOrWhiteSpace(field.ExtractedValue))
                return ConfiguredFieldExtraction.Unreadable("No extracted value is available for this field.");
            var confidence = field.IsManuallyCorrected ? 1 : field.Confidence;
            return new ConfiguredFieldExtraction(
                new FieldObservation { Value = field.ExtractedValue, Confidence = confidence },
                field.BoundingBoxJson,
                field.IsManuallyCorrected
                    ? $"Manually corrected: {field.CorrectionReason}"
                    : "Value retained from the latest OCR extraction.");
        });
    }

    private StageResult CreateComparisonStage(
        Guid inspectionId,
        LabelType labelType,
        InspectionStageType stageType,
        IReadOnlyList<FieldDefinition> definitions,
        IReadOnlyDictionary<Guid, ConfiguredFieldExtraction> masterFields,
        IReadOnlyDictionary<Guid, ConfiguredFieldExtraction> actualFields,
        DateTimeOffset? startedAtUtc)
    {
        var comparisons = new List<FieldComparison>(definitions.Count);
        foreach (var definition in definitions)
        {
            var master = masterFields[definition.Id];
            var actual = actualFields[definition.Id];
            var assessment = fieldComparisonService.Compare(definition, master.Observation, actual.Observation);
            var reasons = assessment.Status is InspectionStatus.PASS or InspectionStatus.NOT_APPLICABLE
                ? assessment.Reason
                : string.Join(" ", assessment.Reason, master.Reason, actual.Reason);

            comparisons.Add(new FieldComparison
            {
                FieldDefinitionId = definition.Id,
                FieldKey = definition.FieldName,
                MasterValue = master.Observation?.Value,
                ActualValue = actual.Observation?.Value,
                NormalizedMasterValue = master.Observation is null
                    ? null
                    : FieldComparisonService.NormalizeForStorage(definition, master.Observation.Value!),
                NormalizedActualValue = actual.Observation is null
                    ? null
                    : FieldComparisonService.NormalizeForStorage(definition, actual.Observation.Value!),
                ComparisonStrategy = definition.ComparisonStrategy,
                MasterConfidence = master.Observation?.Confidence,
                ActualConfidence = actual.Observation?.Confidence,
                Status = assessment.Status,
                MismatchReason = reasons
            });
        }

        var stageStatus = AggregateFieldStatuses(comparisons, definitions.Count);
        var stage = new StageResult
        {
            InspectionSessionId = inspectionId,
            LabelType = labelType,
            StageType = stageType,
            Status = stageStatus,
            TotalFields = comparisons.Count,
            MatchedFields = comparisons.Count(x => x.Status == InspectionStatus.PASS),
            MismatchedFields = comparisons.Count(x => x.Status == InspectionStatus.FAIL),
            UnreadableFields = comparisons.Count(x => x.Status == InspectionStatus.REVIEW_REQUIRED),
            StartedAtUtc = startedAtUtc,
            Remarks = definitions.Count == 0
                ? "No active fields are configured for this profile; review is required."
                : $"Compared {definitions.Count} active configured field(s)."
        };
        foreach (var comparison in comparisons)
            stage.FieldComparisons.Add(comparison);
        return stage;
    }

    private static InspectionStatus AggregateFieldStatuses(
        IReadOnlyCollection<FieldComparison> comparisons,
        int configuredFieldCount)
    {
        if (configuredFieldCount == 0)
            return InspectionStatus.REVIEW_REQUIRED;
        if (comparisons.Any(x => x.Status == InspectionStatus.FAIL))
            return InspectionStatus.FAIL;
        if (comparisons.Any(x => x.Status == InspectionStatus.REVIEW_REQUIRED))
            return InspectionStatus.REVIEW_REQUIRED;
        return comparisons.Any(x => x.Status == InspectionStatus.PASS)
            && comparisons.All(x => x.Status is InspectionStatus.PASS or InspectionStatus.NOT_APPLICABLE)
            ? InspectionStatus.PASS
            : InspectionStatus.REVIEW_REQUIRED;
    }

    private static bool IsReadable(FieldObservation? observation, double minimumConfidence) =>
        observation is not null
        && !string.IsNullOrWhiteSpace(observation.Value)
        && observation.Confidence is >= 0 and <= 1
        && observation.Confidence >= minimumConfidence;

    private static async Task<string> ReadOcrServiceErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        const int maximumErrorLength = 1000;
        return body.Length > maximumErrorLength
            ? body[..maximumErrorLength]
            : body;
    }

    private void EnsureAllImages(InspectionSession inspection)
    {
        var missing = InspectionImageRequirements.GetMissingTypes(inspection.Images.Select(x => x.ImageType));
        if (missing.Count > 0)
            throw new InspectionConflictException($"Required images are missing: {string.Join(", ", missing)}.");
    }

    private string GetStoragePath(string relativePath) =>
        InspectionStoragePaths.ResolvePath(_storageRoot, relativePath);

    private static string SanitizeOriginalName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var clean = new string(name.Where(x => !char.IsControl(x)).ToArray()).Trim();
        if (clean.Length == 0) return "uploaded-image";
        return clean.Length > 255 ? clean[..255] : clean;
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static (LabelType LabelType, ImageRole Role) GetLabelParts(InspectionImageType type) => type switch
    {
        InspectionImageType.MASTER_CO => (LabelType.CO, ImageRole.MASTER),
        InspectionImageType.ACTUAL_CO => (LabelType.CO, ImageRole.ACTUAL),
        InspectionImageType.MASTER_CONFIG => (LabelType.CONFIG, ImageRole.MASTER),
        InspectionImageType.ACTUAL_CONFIG => (LabelType.CONFIG, ImageRole.ACTUAL),
        _ => throw new InspectionValidationException("Unsupported image type.")
    };

    private static void AddLog(
        InspectionSession inspection,
        Guid userId,
        string action,
        string description,
        string? oldValue,
        string? newValue,
        string? ipAddress) =>
        inspection.Logs.Add(new InspectionLog
        {
            UserAccountId = userId,
            EventType = action,
            Details = description,
            PreviousValue = oldValue,
            NewValue = newValue,
            IPAddress = ipAddress
        });

    private static InspectionSummaryResponse ToSummary(InspectionSession item) =>
        new(item.Id, item.InspectionNumber, item.BrandId, item.Brand.Name, item.ModelId,
            item.Model.Code, item.Model.Name, item.ProductionOrderNumber, item.WorkOrderNumber,
            item.Status, item.COStatus, item.ConfigStatus, item.SerialStatus,
            item.CreatedAtUtc, item.CompletedAtUtc, item.CreatedByUser?.FullName,
            item.CreatedByUser?.EmployeeId);

    private static InspectionImageResponse ToImageResponse(InspectionImage item) =>
        new(item.Id, item.ImageType, item.OriginalFileName, item.ContentType, item.SizeBytes,
            item.Width, item.Height, item.Sha256Hash, item.UploadedAtUtc, item.ImageQualityStatus);

    private static InspectionDetailsResponse ToDetails(InspectionSession item) =>
        new(ToSummary(item), item.LabelProfileCOId, item.LabelProfileCO?.Version,
            item.LabelProfileConfigId, item.LabelProfileConfig?.Version,
            item.Images.OrderBy(x => x.ImageType).Select(ToImageResponse).ToList(),
            item.Images.OrderBy(x => x.ImageType).SelectMany(image => image.OcrResults.Select(result =>
                new InspectionOcrEvidenceResponse(
                    result.Id, image.Id, image.ImageType, result.Engine, result.EngineVersion,
                    result.Status, result.RawExtractedText, result.ExtractedFieldsJson,
                    result.OverallConfidence, result.ProcessingDurationMs,
                    result.ExtractedFields.OrderBy(field => field.FieldKey).Select(field =>
                        new ExtractedFieldResponse(
                            field.FieldDefinitionId, field.FieldKey, field.ExtractedValue, field.NormalizedValue,
                            field.Confidence, field.IsManuallyCorrected, field.CorrectionReason)).ToList()))).ToList(),
            item.StageResults.OrderBy(x => x.CreatedAtUtc).Select(x => new InspectionStageResponse(
                x.Id, x.StageType, x.Status, x.TotalFields, x.MatchedFields, x.MismatchedFields,
                x.UnreadableFields, x.StartedAtUtc, x.CompletedAtUtc, x.Remarks,
                x.FieldComparisons.Select(comparison => new FieldComparisonResponse(
                    comparison.FieldKey, comparison.MasterValue, comparison.ActualValue,
                    comparison.NormalizedMasterValue, comparison.NormalizedActualValue,
                    comparison.ComparisonStrategy, comparison.Status,
                    comparison.MasterConfidence, comparison.ActualConfidence,
                    comparison.MismatchReason)).ToList())).ToList(),
            item.Logs.OrderBy(x => x.CreatedAtUtc).Select(x => new InspectionLogResponse(
                x.Id, x.UserAccountId, x.EventType, x.Details, x.PreviousValue,
                x.NewValue, x.IPAddress, x.CreatedAtUtc)).ToList(),
            item.FinalRemarks);

}
