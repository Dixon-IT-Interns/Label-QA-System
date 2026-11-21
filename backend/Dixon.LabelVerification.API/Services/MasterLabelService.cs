using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.DTOs.Configuration;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class MasterLabelService(
    LabelVerificationDbContext db,
    IHttpClientFactory httpClientFactory,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<MasterLabelService> logger,
    IModelConfigurationService modelConfigurationService,
    IFieldComparisonService comparisonService,
    AuthenticatedUserContext currentUser) : IMasterLabelService
{
    private readonly string _storageRoot = InspectionStoragePaths.ResolveRoot(configuration, environment);

    public async Task<IReadOnlyList<MasterLabelResponse>> GetAllAsync(
        Guid? brandId, Guid? modelId, CancellationToken cancellationToken)
    {
        var query = db.MasterLabels.AsNoTracking()
            .Include(x => x.Model).ThenInclude(x => x.Brand)
            .Include(x => x.LabelProfile)
            .Include(x => x.Fields)
            .AsQueryable();
        if (brandId.HasValue)
        {
            currentUser.EnsureCanAccessBrand(brandId.Value);
            query = query.Where(x => x.BrandId == brandId.Value);
        }
        else if (!currentUser.IsAdmin)
        {
            var assignedBrandId = currentUser.BrandId
                ?? throw new UnauthorizedAccessException("The authenticated account has no assigned brand.");
            query = query.Where(x => x.BrandId == assignedBrandId);
        }
        if (modelId.HasValue)
        {
            await EnsureModelAccessAsync(modelId.Value, cancellationToken);
            query = query.Where(x => x.ModelId == modelId.Value);
        }

        var labels = await query
            .OrderBy(x => x.Model.Code)
            .ThenBy(x => x.LabelType)
            .ToListAsync(cancellationToken);
        return labels.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyList<MasterLabelResponse>> GetByModelAsync(
        Guid modelId, CancellationToken cancellationToken) =>
        await GetAllAsync(null, modelId, cancellationToken);

    public async Task<MasterLabelAvailabilityResponse> GetAvailabilityAsync(
        Guid modelId, CancellationToken cancellationToken)
    {
        await EnsureModelAccessAsync(modelId, cancellationToken);

        var configuredTypes = await db.MasterLabels.AsNoTracking()
            .Where(x => x.ModelId == modelId)
            .Select(x => x.LabelType)
            .ToListAsync(cancellationToken);
        var hasCoMaster = configuredTypes.Contains(LabelType.CO);
        var hasConfigMaster = configuredTypes.Contains(LabelType.CONFIG);
        var missing = new List<string>();
        if (!hasCoMaster) missing.Add("Master COO");
        if (!hasConfigMaster) missing.Add("Master Config");
        var target = missing.Count == 1 ? "it" : "them";
        return new MasterLabelAvailabilityResponse(
            modelId,
            hasCoMaster,
            hasConfigMaster,
            missing.Count == 0
                ? null
                : $"{string.Join(" and ", missing)} label{(missing.Count == 1 ? " is" : "s are")} not configured. Ask a Supervisor to configure {target} in Master Labels.");
    }

    public async Task<MasterLabelPairResponse> SavePairAsync(
        SaveMasterLabelPairRequest request, Guid userId, CancellationToken cancellationToken)
    {
        currentUser.EnsureCanManageConfiguration();
        currentUser.EnsureCanAccessBrand(request.BrandId);
        var model = await db.Models.Include(x => x.Brand)
            .SingleOrDefaultAsync(
                x => x.Id == request.ModelId && x.BrandId == request.BrandId,
                cancellationToken)
            ?? throw new InspectionValidationException("The selected model does not belong to the selected brand.");
        if (!model.IsActive || !model.Brand.IsActive)
            throw new InspectionConflictException("Only active brands and models can have master labels.");
        if (!await db.Users.AnyAsync(x => x.Id == userId, cancellationToken))
            throw new InspectionValidationException("The authenticated supervisor account is unavailable.");

        var co = await PrepareMasterLabelAsync(model, LabelType.CO, request.CoFile, cancellationToken);
        var config = await PrepareMasterLabelAsync(model, LabelType.CONFIG, request.ConfigFile, cancellationToken);
        MasterLabelPairValidator.Validate(
            co.Definitions, co.Fields, config.Definitions, config.Fields, comparisonService);

        var saved = await PersistMasterLabelPairAsync(model, [co, config], userId, cancellationToken);
        return new MasterLabelPairResponse(saved[LabelType.CO], saved[LabelType.CONFIG]);
    }

    private async Task<PreparedMasterLabel> PrepareMasterLabelAsync(
        LaptopModel model,
        LabelType labelType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > ImageFileInspector.MaximumFileSizeBytes)
            throw new InspectionValidationException("Images must be non-empty and 20 MB or smaller.");

        await using var uploadStream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(uploadStream, cancellationToken);
        var bytes = uploadStream.ToArray();
        var (contentType, width, height) =
            ImageFileInspector.Inspect(bytes, file.FileName, file.ContentType);
        var profile = await DefaultLabelProfileProvisioner.EnsureActiveProfileAsync(
            db, model, labelType, cancellationToken);
        var definitions = labelType == LabelType.CO
            ? CoComparisonFieldSelector.Select(profile.FieldDefinitions)
            : ConfigComparisonFieldSelector.Select(profile.FieldDefinitions);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var (ocr, barcode) = await ExtractOcrAsync(
            bytes, contentType, file.FileName, labelType, definitions, cancellationToken);
        var confidence = ocr.OcrConfidence
            ?? (ocr.Lines.Count == 0 ? null : ocr.Lines.Average(line => line.Confidence));
        var rawText = ocr.RawText ?? string.Join(Environment.NewLine, ocr.Lines.Select(line => line.Text));
        if (rawText.Length > 20000)
            throw new InspectionProcessingException("Master label OCR text exceeds the supported size.");
        var needsReview = ocr.Status == "REVIEW_REQUIRED"
            || ocr.Lines.Count == 0 && (ocr.FieldCandidates?.Count ?? 0) == 0;
        var processingDurationMs = ocr.ProcessingDurationMs + (barcode?.ProcessingDurationMs ?? 0);
        var ocrData = JsonSerializer.Serialize(new
        {
            rawText,
            ocrConfidence = confidence,
            jobId = ocr.JobId,
            artifacts = ocr.Artifacts,
            lines = ocr.Lines,
            configuredFieldCandidates = (ocr.FieldCandidates ?? []).Select(candidate => new
            {
                fieldName = candidate.FieldName,
                value = candidate.Value,
                confidence = candidate.Confidence,
                box = candidate.Box,
                status = candidate.Status,
                reason = candidate.Reason,
                debug = candidate.Debug
            }).ToArray(),
            barcodes = barcode?.Barcodes ?? [],
            warnings = (ocr.Warnings ?? []).Concat(barcode?.Warnings ?? []).ToArray(),
            imageQuality = ocr.ImageQuality,
            processingDurationMs
        });
        var fields = definitions.Select(definition =>
        {
            var extraction = ConfiguredFieldExtractionService.Extract(
                definition, ocr.Lines, ocr.FieldCandidates);
            var observation = extraction.Observation;
            return new MasterLabelField
            {
                FieldKey = definition.FieldName,
                DisplayName = definition.DisplayName,
                ExtractedValue = observation?.Value,
                NormalizedValue = observation is null
                    ? null
                    : FieldComparisonService.NormalizeForStorage(definition, observation.Value!),
                Confidence = observation?.Confidence ?? 0,
                BoundingBoxJson = extraction.BoundingBoxJson,
                Reason = extraction.Reason
            };
        }).ToList();

        var extension = contentType == "image/png" ? ".png" : ".jpg";
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storagePath = Path.Combine(
            "master-labels", model.Id.ToString("N"), labelType.ToString(), storedFileName);
        return new PreparedMasterLabel(
            labelType,
            profile,
            definitions,
            SanitizeOriginalName(file.FileName),
            bytes,
            contentType,
            width,
            height,
            hash,
            storedFileName,
            storagePath,
            ocr,
            rawText,
            ocrData,
            confidence,
            needsReview,
            processingDurationMs,
            fields);
    }

    private async Task<IReadOnlyDictionary<LabelType, MasterLabelResponse>> PersistMasterLabelPairAsync(
        LaptopModel model,
        IReadOnlyList<PreparedMasterLabel> preparedLabels,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var targets = preparedLabels.ToDictionary(
            prepared => prepared.LabelType,
            prepared => InspectionStoragePaths.ResolvePath(_storageRoot, prepared.StoragePath));
        var previousPaths = new List<(string Path, LabelType LabelType)>();
        var savedMasterIds = new Dictionary<LabelType, Guid>();
        try
        {
            foreach (var prepared in preparedLabels)
            {
                var target = targets[prepared.LabelType];
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllBytesAsync(target, prepared.Bytes, cancellationToken);
            }

            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);
            foreach (var prepared in preparedLabels)
            {
                var currentProfileId = await db.LabelProfiles.AsNoTracking()
                    .Where(x => x.ModelId == model.Id
                        && x.LabelType == prepared.LabelType
                        && x.IsActive)
                    .Select(x => (Guid?)x.Id)
                    .SingleOrDefaultAsync(cancellationToken);
                if (currentProfileId != prepared.Profile.Id)
                    throw new InspectionConflictException(
                        "An active label profile changed during upload. Refresh and upload both master labels again.");
            }

            var labelTypes = preparedLabels.Select(prepared => prepared.LabelType).ToArray();
            var existingMasters = await db.MasterLabels.Include(x => x.Fields)
                .Where(master => master.ModelId == model.Id && labelTypes.Contains(master.LabelType))
                .ToListAsync(cancellationToken);
            foreach (var prepared in preparedLabels)
            {
                var master = existingMasters.SingleOrDefault(x => x.LabelType == prepared.LabelType);
                if (master is null)
                {
                    master = new MasterLabel
                    {
                        BrandId = model.BrandId,
                        ModelId = model.Id,
                        LabelType = prepared.LabelType,
                        OriginalFileName = prepared.OriginalFileName,
                        StoredFileName = prepared.StoredFileName,
                        StoragePath = prepared.StoragePath,
                        Sha256Hash = prepared.Sha256Hash,
                        ContentType = prepared.ContentType,
                        Engine = prepared.Ocr.Engine,
                        EngineVersion = prepared.Ocr.EngineVersion
                    };
                    db.MasterLabels.Add(master);
                }
                else
                {
                    previousPaths.Add((master.StoragePath, prepared.LabelType));
                    db.MasterLabelFields.RemoveRange(master.Fields);
                    master.Fields.Clear();
                }

                master.LabelProfileId = prepared.Profile.Id;
                master.OriginalFileName = prepared.OriginalFileName;
                master.StoredFileName = prepared.StoredFileName;
                master.StoragePath = prepared.StoragePath;
                master.Sha256Hash = prepared.Sha256Hash;
                master.ContentType = prepared.ContentType;
                master.SizeBytes = prepared.Bytes.LongLength;
                master.Width = prepared.Width;
                master.Height = prepared.Height;
                master.Engine = prepared.Ocr.Engine;
                master.EngineVersion = prepared.Ocr.EngineVersion;
                master.OcrStatus = prepared.NeedsReview
                    ? InspectionStatus.REVIEW_REQUIRED
                    : InspectionStatus.PASS;
                master.RawExtractedText = prepared.RawText;
                master.OcrDataJson = prepared.OcrDataJson;
                master.OverallConfidence = prepared.Confidence;
                master.ProcessingDurationMs = prepared.ProcessingDurationMs;
                master.ImageQualityStatus = prepared.NeedsReview ? "REVIEW_REQUIRED" : "ACCEPTED";
                master.UploadedByUserId = userId;
                master.UploadedAtUtc = DateTimeOffset.UtcNow;
                foreach (var field in prepared.Fields)
                {
                    db.MasterLabelFields.Add(field);
                    master.Fields.Add(field);
                }

                savedMasterIds.Add(prepared.LabelType, master.Id);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            foreach (var target in targets.Values)
                DeleteNewFile(target);
            throw;
        }

        foreach (var (path, labelType) in previousPaths)
            DeleteReplacedFile(path, model.Id, labelType);
        logger.LogInformation(
            "Validated master COO and Config labels saved for model {ModelId} by user {UserId}",
            model.Id, userId);
        var ids = savedMasterIds.Values.ToArray();
        var savedMasters = await db.MasterLabels.AsNoTracking()
            .Include(x => x.Model).ThenInclude(x => x.Brand)
            .Include(x => x.LabelProfile)
            .Include(x => x.Fields)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        return savedMasters.ToDictionary(master => master.LabelType, ToResponse);
    }

    public async Task<DetectedModelResponse> DetectModelAsync(
        DetectModelRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureCanManageConfiguration();
        currentUser.EnsureCanAccessBrand(request.BrandId);
        if (request.File.Length is <= 0 or > ImageFileInspector.MaximumFileSizeBytes)
            throw new InspectionValidationException("Images must be non-empty and 20 MB or smaller.");
        var brand = await db.Brands.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.BrandId && x.IsActive, cancellationToken)
            ?? throw new InspectionValidationException("Select an active brand before detecting a model.");
        await using var stream = new MemoryStream((int)request.File.Length);
        await request.File.CopyToAsync(stream, cancellationToken);
        var bytes = stream.ToArray();
        var (contentType, _, _) =
            ImageFileInspector.Inspect(bytes, request.File.FileName, request.File.ContentType);

        var result = await ExtractModelOcrAsync(
            bytes, contentType, request.File.FileName, LabelType.CO, cancellationToken);
        var modelField = FindDetectedModelField(result);
        if (modelField is null)
        {
            result = await ExtractModelOcrAsync(
                bytes, contentType, request.File.FileName, LabelType.CONFIG, cancellationToken);
            modelField = FindDetectedModelField(result);
        }

        var ocrText = string.IsNullOrWhiteSpace(result.RawText)
            ? string.Join(" ", result.Lines.Select(line => line.Text))
            : result.RawText;
        var recognizedText = NormalizeModelText(ocrText);
        var models = await db.Models.AsNoTracking()
            .Where(x => x.BrandId == brand.Id && x.IsActive)
            .Select(x => new { x.Id, x.BrandId, x.Code, x.Name })
            .ToListAsync(cancellationToken);
        var matches = models
            .SelectMany(model => new[] { model.Code, model.Name }
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(value => new
                {
                    Model = model,
                    Token = NormalizeModelText(value)
                }))
            .Where(candidate => candidate.Token.Length >= 4
                && recognizedText.Contains(candidate.Token, StringComparison.Ordinal))
            .GroupBy(candidate => candidate.Model.Id)
            .Select(group => new
            {
                Model = group.First().Model,
                MatchLength = group.Max(candidate => candidate.Token.Length)
            })
            .OrderByDescending(candidate => candidate.MatchLength)
            .ToArray();
        if (matches.Length > 1 && matches[0].MatchLength == matches[1].MatchLength)
            throw new InspectionConflictException(
                "The label matches multiple configured models. Select the model manually.");

        if (matches.Length > 0)
        {
            var detected = matches[0].Model;
            return new DetectedModelResponse(
                detected.Id, detected.BrandId, detected.Code, detected.Name, false);
        }

        var modelCode = ModelCodeExtractor.Extract(modelField?.Value);
        if (modelCode is null)
            throw new InspectionConflictException(
                "COO and Config OCR could not confidently read a model number. Select an existing model or upload a clearer label.");

        var existingModel = await db.Models.AsNoTracking()
            .Where(model => model.BrandId == brand.Id && model.Code == modelCode)
            .Select(model => new { model.Id, model.BrandId, model.Code, model.Name, model.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (existingModel is not null)
        {
            if (!existingModel.IsActive)
                throw new InspectionConflictException(
                    $"Detected model '{modelCode}' already exists but is inactive. Activate it in Model Management.");
            return new DetectedModelResponse(
                existingModel.Id, existingModel.BrandId, existingModel.Code, existingModel.Name, false);
        }

        try
        {
            var created = await modelConfigurationService.CreateAsync(
                new ModelRequest
                {
                    BrandId = brand.Id,
                    Code = modelCode,
                    Name = modelCode,
                    IsActive = true
                },
                cancellationToken);
            return new DetectedModelResponse(created.Id, created.BrandId, created.Code, created.Name, true);
        }
        catch (ConfigurationConflictException)
        {
            var concurrentModel = await db.Models.AsNoTracking()
                .Where(model => model.BrandId == brand.Id && model.Code == modelCode && model.IsActive)
                .Select(model => new { model.Id, model.BrandId, model.Code, model.Name })
                .SingleOrDefaultAsync(cancellationToken);
            if (concurrentModel is null)
                throw;
            return new DetectedModelResponse(
                concurrentModel.Id, concurrentModel.BrandId, concurrentModel.Code, concurrentModel.Name, false);
        }
    }

    private async Task<OcrServiceResponse> ExtractModelOcrAsync(
        byte[] bytes,
        string contentType,
        string fileName,
        LabelType labelType,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(bytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(imageContent, "file", fileName);
        content.Add(new StringContent(JsonSerializer.Serialize(GetModelDetectionFields(labelType))), "field_config");
        content.Add(new StringContent(labelType.ToString()), "label_type");

        using var response = await httpClientFactory.CreateClient("OcrService")
            .PostAsync("ocr/extract", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InspectionProcessingException(
                $"OCR service returned HTTP {(int)response.StatusCode} while detecting the model from {labelType}: {await ReadOcrErrorAsync(response, cancellationToken)}");

        var result = await response.Content.ReadFromJsonAsync<OcrServiceResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InspectionProcessingException("OCR service returned an empty response.");
        if (result.Status is not ("PROCESSING" or "REVIEW_REQUIRED")
            || string.IsNullOrWhiteSpace(result.Engine)
            || result.Lines is null)
            throw new InspectionProcessingException("OCR service returned an invalid response.");
        return result;
    }

    private static object[] GetModelDetectionFields(LabelType labelType) =>
        labelType switch
        {
            LabelType.CO =>
            [
                new
                {
                    field_name = "MODEL_NO",
                    display_name = "Model No.",
                    aliases = new[] { "Model", "Model Number", "Product Model" },
                    confidence_threshold = 0.55
                },
                new
                {
                    field_name = "QR_CODE",
                    display_name = "QR Code",
                    aliases = new[] { "QR", "QRCode" },
                    confidence_threshold = 0.65
                },
                new
                {
                    field_name = "PART_NUMBER",
                    display_name = "P/N",
                    aliases = new[] { "PN", "Part No", "Part Number" },
                    confidence_threshold = 0.65
                }
            ],
            LabelType.CONFIG =>
            [
                new
                {
                    field_name = "MODEL_NO",
                    display_name = "Model No.",
                    aliases = new[] { "Model", "Model Number", "Product Model" },
                    confidence_threshold = 0.55
                },
                new
                {
                    field_name = "QUEUE_NO",
                    display_name = "Queue No.",
                    aliases = new[] { "Queue", "Queue Number", "Q No" },
                    confidence_threshold = 0.55
                }
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(labelType), labelType, null)
        };

    private static OcrFieldCandidate? FindDetectedModelField(OcrServiceResponse result) =>
        result.FieldCandidates?.FirstOrDefault(candidate =>
            string.Equals(candidate.FieldName, "MODEL_NO", StringComparison.OrdinalIgnoreCase)
            && candidate.Status == "DETECTED"
            && candidate.Confidence >= 0.55
            && !string.IsNullOrWhiteSpace(candidate.Value));

    public async Task<MasterLabelImageContent> GetImageAsync(
        Guid modelId, LabelType labelType, CancellationToken cancellationToken)
    {
        await EnsureModelAccessAsync(modelId, cancellationToken);
        var master = await db.MasterLabels.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.ModelId == modelId && x.LabelType == labelType,
                cancellationToken)
            ?? throw new InspectionNotFoundException(
                $"No {labelType} master label is configured for model '{modelId}'.");
        var path = InspectionStoragePaths.ResolvePath(_storageRoot, master.StoragePath);
        if (!File.Exists(path))
            throw new InspectionNotFoundException("The stored master label image is unavailable.");
        return new MasterLabelImageContent(
            File.OpenRead(path), master.ContentType, master.OriginalFileName);
    }

    private async Task EnsureModelAccessAsync(Guid modelId, CancellationToken cancellationToken)
    {
        var brandId = await db.Models.AsNoTracking()
            .Where(x => x.Id == modelId)
            .Select(x => (Guid?)x.BrandId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InspectionNotFoundException($"Model '{modelId}' was not found.");
        currentUser.EnsureCanAccessBrand(brandId);
    }

    private async Task<(OcrServiceResponse Ocr, OcrBarcodeResponse? Barcode)> ExtractOcrAsync(
        byte[] bytes,
        string contentType,
        string fileName,
        LabelType labelType,
        IReadOnlyList<FieldDefinition> definitions,
        CancellationToken cancellationToken)
    {
        var thresholdValue = await db.SystemSettings.AsNoTracking()
            .Where(x => x.SettingKey == "OcrMinimumConfidence")
            .Select(x => x.SettingValue)
            .SingleOrDefaultAsync(cancellationToken);
        if (!double.TryParse(thresholdValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
            || !double.IsFinite(threshold)
            || threshold is < 0 or > 1)
            throw new InspectionProcessingException("The configured global OCR confidence threshold is invalid.");

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(bytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(imageContent, "file", fileName);
        content.Add(new StringContent(JsonSerializer.Serialize(definitions.Select(definition => new
        {
            field_name = definition.FieldName,
            display_name = definition.DisplayName,
            aliases = definition.Aliases.Where(alias => alias.IsActive).Select(alias => alias.Value).ToArray(),
            confidence_threshold = definition.OcrConfidenceThreshold
        }))), "field_config");
        content.Add(new StringContent(labelType.ToString()), "label_type");
        content.Add(new StringContent(threshold.ToString(CultureInfo.InvariantCulture)), "minimum_confidence");

        using var response = await httpClientFactory.CreateClient("OcrService")
            .PostAsync("ocr/extract", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InspectionProcessingException(
                $"OCR service returned HTTP {(int)response.StatusCode} for the {labelType} master label: {await ReadOcrErrorAsync(response, cancellationToken)}");
        var ocr = await response.Content.ReadFromJsonAsync<OcrServiceResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InspectionProcessingException("OCR service returned an empty response.");
        ValidateOcrResponse(ocr);

        if (labelType == LabelType.CO)
        {
            var configuredKeys = definitions.Select(field => field.FieldName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidates = (ocr.FieldCandidates ?? [])
                .Where(candidate => configuredKeys.Contains(candidate.FieldName))
                .Select(candidate => candidate with { Debug = null })
                .ToList();
            if (candidates.Count != configuredKeys.Count
                || candidates.Select(candidate => candidate.FieldName)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuredKeys.Count)
                throw new InspectionProcessingException(
                    "CO OCR must return one candidate for each configured Model No., QR Code, and P/N field.");
            ocr = ocr with
            {
                RawText = string.Empty,
                Lines = [],
                FieldCandidates = candidates,
                OcrConfidence = candidates.Average(candidate => candidate.Confidence)
            };
        }

        OcrBarcodeResponse? barcode = null;
        if (labelType == LabelType.CONFIG)
        {
            using var barcodeContent = new MultipartFormDataContent();
            var barcodeImageContent = new ByteArrayContent(bytes);
            barcodeImageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            barcodeContent.Add(barcodeImageContent, "file", fileName);
            using var barcodeResponse = await httpClientFactory.CreateClient("OcrService")
                .PostAsync("ocr/barcode", barcodeContent, cancellationToken);
            if (!barcodeResponse.IsSuccessStatusCode)
                throw new InspectionProcessingException(
                    $"OCR barcode service returned HTTP {(int)barcodeResponse.StatusCode}: {await ReadOcrErrorAsync(barcodeResponse, cancellationToken)}");
            barcode = await barcodeResponse.Content.ReadFromJsonAsync<OcrBarcodeResponse>(
                cancellationToken: cancellationToken)
                ?? throw new InspectionProcessingException("OCR barcode service returned an empty response.");
        }

        return (ocr, barcode);
    }

    private static void ValidateOcrResponse(OcrServiceResponse result)
    {
        if (result.Status is not ("PROCESSING" or "REVIEW_REQUIRED")
            || result.ImageQuality is null
            || result.ImageQuality.Width <= 0
            || result.ImageQuality.Height <= 0
            || result.Lines is null
            || string.IsNullOrWhiteSpace(result.Engine)
            || string.IsNullOrWhiteSpace(result.EngineVersion)
            || result.RawText is { Length: > 20000 }
            || result.OcrConfidence is { } confidence
                && (!double.IsFinite(confidence) || confidence is < 0 or > 1)
            || result.Lines.Any(line => !double.IsFinite(line.Confidence)
                || line.Confidence is < 0 or > 1)
            || result.FieldCandidates?.Any(candidate =>
                string.IsNullOrWhiteSpace(candidate.FieldName)
                || !double.IsFinite(candidate.Confidence)
                || candidate.Confidence is < 0 or > 1
                || candidate.Status is not ("DETECTED" or "AMBIGUOUS" or "NOT_FOUND")) == true)
            throw new InspectionProcessingException("OCR service returned an invalid response.");
    }

    private async Task<string> ReadOcrErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return body.Length > 2000 ? body[..2000] : body;
    }

    private sealed record PreparedMasterLabel(
        LabelType LabelType,
        LabelProfile Profile,
        IReadOnlyList<FieldDefinition> Definitions,
        string OriginalFileName,
        byte[] Bytes,
        string ContentType,
        int Width,
        int Height,
        string Sha256Hash,
        string StoredFileName,
        string StoragePath,
        OcrServiceResponse Ocr,
        string RawText,
        string OcrDataJson,
        double? Confidence,
        bool NeedsReview,
        long ProcessingDurationMs,
        List<MasterLabelField> Fields);

    private void DeleteReplacedFile(string relativePath, Guid modelId, LabelType labelType)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;
        var path = InspectionStoragePaths.ResolvePath(_storageRoot, relativePath);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException exception)
        {
            logger.LogError(exception,
                "Could not delete the replaced master label image for model {ModelId} and type {LabelType}",
                modelId, labelType);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception,
                "Could not delete the replaced master label image for model {ModelId} and type {LabelType}",
                modelId, labelType);
        }
    }

    private void DeleteNewFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Could not remove a failed master label upload file");
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception, "Could not remove a failed master label upload file");
        }
    }

    private static string NormalizeModelText(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string SanitizeOriginalName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var clean = new string(name.Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (clean.Length == 0) return "uploaded-image";
        return clean.Length > 255 ? clean[..255] : clean;
    }

    private static MasterLabelResponse ToResponse(MasterLabel master) =>
        new(master.Id, master.BrandId, master.ModelId, master.Model.Brand.Name,
            master.Model.Code, master.Model.Name, master.LabelType, master.LabelProfileId,
            master.LabelProfile.Version, master.OriginalFileName, master.ContentType,
            master.SizeBytes, master.Sha256Hash, master.Engine, master.EngineVersion,
            master.OcrStatus, master.RawExtractedText, master.OverallConfidence,
            master.ImageQualityStatus, master.UploadedAtUtc, master.UpdatedAtUtc,
            master.Fields.OrderBy(field => field.FieldKey).Select(field =>
                new MasterLabelFieldResponse(field.FieldKey, field.DisplayName,
                    field.ExtractedValue, field.NormalizedValue, field.Confidence, field.Reason)).ToArray());
}
