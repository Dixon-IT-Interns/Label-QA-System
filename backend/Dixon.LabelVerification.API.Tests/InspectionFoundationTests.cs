using System.Text.Json;
using Dixon.LabelVerification.API.Controllers;
using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Middleware;
using Dixon.LabelVerification.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dixon.LabelVerification.API.Tests;

public sealed class InspectionFoundationTests
{
    [Fact]
    public void ModelNumberIsUniqueWithinBrand()
    {
        using var db = CreateDbContext();
        var index = db.Model.FindEntityType(typeof(LaptopModel))!.GetIndexes()
            .Single(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["BrandId", "Code"]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void ConfidenceColumnsUseSqlServerDoublePrecision()
    {
        using var db = CreateDbContext();
        var ocrConfidence = db.Model.FindEntityType(typeof(OcrResult))!
            .FindProperty(nameof(OcrResult.OverallConfidence))!;
        var extractedConfidence = db.Model.FindEntityType(typeof(ExtractedField))!
            .FindProperty(nameof(ExtractedField.Confidence))!;

        Assert.Equal("float", ocrConfidence.GetColumnType());
        Assert.Equal("float", extractedConfidence.GetColumnType());
    }

    [Fact]
    public void InspectionPinsProfilesAndRestrictsHistoricalDeletion()
    {
        using var db = CreateDbContext();
        var inspection = db.Model.FindEntityType(typeof(InspectionSession))!;
        var profileForeignKeys = inspection.GetForeignKeys()
            .Where(x => x.PrincipalEntityType.ClrType == typeof(LabelProfile))
            .ToList();

        Assert.Equal(2, profileForeignKeys.Count);
        Assert.All(profileForeignKeys, foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.Contains(inspection.GetProperties(), property => property.Name == nameof(InspectionSession.LabelProfileCOId));
        Assert.Contains(inspection.GetProperties(), property => property.Name == nameof(InspectionSession.LabelProfileConfigId));
    }

    [Fact]
    public void RequiredImageValidationReportsEveryMissingSlot()
    {
        var uploaded = new[] { InspectionImageType.MASTER_CO, InspectionImageType.ACTUAL_CONFIG };
        var missing = InspectionImageRequirements.GetMissingTypes(uploaded);

        Assert.Equal(
            [InspectionImageType.ACTUAL_CO, InspectionImageType.MASTER_CONFIG],
            missing);
        Assert.Equal(["MASTER_CO", "ACTUAL_CO", "MASTER_CONFIG", "ACTUAL_CONFIG"], Enum.GetNames<InspectionImageType>());
    }

    [Fact]
    public void OperatorsCanUploadOnlyActualLabelsAndMasterLabelsAreUniquePerModelAndType()
    {
        Assert.Equal(
            [InspectionImageType.ACTUAL_CO, InspectionImageType.ACTUAL_CONFIG],
            InspectionImageRequirements.OperatorUploadTypes);

        using var db = CreateDbContext();
        var masterLabel = db.Model.FindEntityType(typeof(MasterLabel))!;
        var uniqueIndex = masterLabel.GetIndexes().Single(index => index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(MasterLabel.ModelId), nameof(MasterLabel.LabelType)]));
        var modelForeignKey = masterLabel.GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(LaptopModel));

        Assert.True(uniqueIndex.IsUnique);
        Assert.Equal(
            [nameof(MasterLabel.BrandId), nameof(MasterLabel.ModelId)],
            modelForeignKey.Properties.Select(property => property.Name));
    }

    [Fact]
    public void MasterLabelManagementWritesAllowAdminsAndSupervisors()
    {
        var controllerAuthorization = typeof(MasterLabelsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        var save = typeof(MasterLabelsController).GetMethod(nameof(MasterLabelsController.SavePair))!;
        var saveAuthorization = save.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        var availability = typeof(MasterLabelsController)
            .GetMethod(nameof(MasterLabelsController.GetAvailability))!;
        var availabilityAuthorization = availability.GetCustomAttributes(
                typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();

        Assert.NotNull(controllerAuthorization);
        Assert.Equal("Admin,Supervisor", saveAuthorization.Roles);
        Assert.Equal("Admin,Supervisor,Operator", availabilityAuthorization.Roles);
    }

    [Fact]
    public void UserManagementRestrictsSupervisorScopeAndPermanentDeletionToAdmins()
    {
        var getAll = typeof(UsersController).GetMethod(nameof(UsersController.GetAll))!;
        var create = typeof(UsersController).GetMethod(nameof(UsersController.Create))!;
        var update = typeof(UsersController).GetMethod(nameof(UsersController.Update))!;
        var delete = typeof(UsersController).GetMethod(nameof(UsersController.Delete))!;

        Assert.Equal("Admin,Supervisor", GetRoles(getAll));
        Assert.Equal("Admin,Supervisor", GetRoles(create));
        Assert.Equal("Admin,Supervisor", GetRoles(update));
        Assert.Equal(ApplicationRoles.Admin, GetRoles(delete));
    }

    [Fact]
    public void GlobalSystemSettingsAreAdminOnly()
    {
        var authorization = typeof(SettingsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();

        Assert.Equal(ApplicationRoles.Admin, authorization.Roles);
    }

    [Fact]
    public void MasterConfigWritesAreAdminOnly()
    {
        var controllerAuthorization = typeof(LabelProfilesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        var writeMethods = new[]
        {
            nameof(LabelProfilesController.Create),
            nameof(LabelProfilesController.Update),
            nameof(LabelProfilesController.Delete),
            nameof(LabelProfilesController.CreateField),
            nameof(LabelProfilesController.UpdateField),
            nameof(LabelProfilesController.DeleteField)
        };

        Assert.Null(controllerAuthorization.Roles);
        Assert.All(writeMethods, method => Assert.Equal(
            ApplicationRoles.Admin,
            GetRoles(typeof(LabelProfilesController).GetMethod(method)!)));
    }

    [Fact]
    public void UserBrandAndAttributionRelationshipsSupportSafeUserDeletion()
    {
        using var db = CreateDbContext();
        var user = db.Model.FindEntityType(typeof(User))!;
        var brandForeignKey = user.GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(Brand));
        var brandIndex = user.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(User.BrandId)]));

        Assert.False(brandForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, brandForeignKey.DeleteBehavior);
        Assert.Contains(user.GetProperties(), property => property.Name == nameof(User.BrandId));
        Assert.NotNull(brandIndex);

        var attributionRelationships = new[]
        {
            (typeof(InspectionSession), nameof(InspectionSession.CreatedByUserId)),
            (typeof(InspectionImage), nameof(InspectionImage.UploadedByUserId)),
            (typeof(InspectionReport), nameof(InspectionReport.GeneratedByUserId)),
            (typeof(MasterLabel), nameof(MasterLabel.UploadedByUserId)),
            (typeof(InspectionLog), nameof(InspectionLog.UserAccountId)),
            (typeof(ExtractedField), nameof(ExtractedField.CorrectedByUserId))
        };
        foreach (var (entityType, propertyName) in attributionRelationships)
        {
            var foreignKey = db.Model.FindEntityType(entityType)!.GetForeignKeys()
                .Single(key => key.Properties.Any(property => property.Name == propertyName));
            Assert.False(foreignKey.IsRequired);
            Assert.Equal(DeleteBehavior.SetNull, foreignKey.DeleteBehavior);
        }
    }

    [Fact]
    public async Task ComparisonArtifactContainsFieldValuesAndFinalResult()
    {
        using var temporary = new TemporaryDirectory();
        var storageRoot = Path.Combine(temporary.Path, "storage", "inspections");
        var inspectionId = Guid.NewGuid();
        var comparison = new FieldComparison
        {
            FieldKey = "MODEL",
            MasterValue = "15-fd1654TU",
            ActualValue = "15-fd1652TU",
            NormalizedMasterValue = "15-FD1654TU",
            NormalizedActualValue = "15-FD1652TU",
            Status = InspectionStatus.FAIL
        };

        var path = await InspectionComparisonArtifactWriter.WriteAsync(
            storageRoot, inspectionId, InspectionStatus.FAIL, [comparison], CancellationToken.None);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = document.RootElement;
        var field = root.GetProperty("fields")[0];

        Assert.Equal("completed", root.GetProperty("status").GetString());
        Assert.Equal("FAIL", root.GetProperty("overallResult").GetString());
        Assert.Equal(inspectionId, root.GetProperty("inspectionId").GetGuid());
        Assert.Equal("MODEL", field.GetProperty("field").GetString());
        Assert.Equal("15-fd1654TU", field.GetProperty("master").GetString());
        Assert.Equal("15-fd1652TU", field.GetProperty("actual").GetString());
        Assert.Equal("FAIL", field.GetProperty("result").GetString());
    }

    [Fact]
    public void InspectionImageSlotsAreUniqueAndInspectionUsesRowVersionConcurrency()
    {
        using var db = CreateDbContext();
        var imageTypeIndex = db.Model.FindEntityType(typeof(InspectionImage))!.GetIndexes()
            .Single(index => index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(InspectionImage.InspectionSessionId), nameof(InspectionImage.ImageType)]));
        var rowVersion = db.Model.FindEntityType(typeof(InspectionSession))!
            .FindProperty(nameof(InspectionSession.RowVersion));

        Assert.True(imageTypeIndex.IsUnique);
        Assert.NotNull(rowVersion);
        Assert.True(rowVersion!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        Assert.Equal("rowversion", rowVersion.GetColumnType());

        var expectedVersion = new byte[8];
        var conditionalUpdateQuery = db.InspectionSessions
            .Where(inspection => inspection.Status == InspectionStatus.READY
                && inspection.RowVersion == expectedVersion)
            .ToQueryString();
        Assert.Contains("[RowVersion] =", conditionalUpdateQuery);
    }

    [Fact]
    public async Task InspectionHasOnlyOneProcessingJobAndConcurrentProcessConflictIsClear()
    {
        using var db = CreateDbContext();
        var processingJobIndex = db.Model.FindEntityType(typeof(InspectionProcessingJob))!
            .GetIndexes()
            .Single(index => index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(InspectionProcessingJob.InspectionSessionId)]));
        Assert.True(processingJobIndex.IsUnique);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            _ => Task.FromException(new InspectionConflictException("Inspection is already being processed.")),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = JsonDocument.Parse(context.Response.Body);
        Assert.Equal("Inspection is already being processed.", response.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task DatabaseConcurrencyConflictUsesRequestAgnosticMessage()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/master-labels/123/pair";
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            _ => Task.FromException(new DbUpdateConcurrencyException("Concurrency conflict.")),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = JsonDocument.Parse(context.Response.Body);
        Assert.Equal(
            "The data changed while this request was in progress. Refresh and retry.",
            response.RootElement.GetProperty("title").GetString());
        Assert.True(response.RootElement.TryGetProperty("conflictingEntities", out _));
    }

    [Fact]
    public void InspectionModelRelationshipIncludesBrandToPreventCrossBrandModels()
    {
        using var db = CreateDbContext();
        var foreignKey = db.Model.FindEntityType(typeof(InspectionSession))!.GetForeignKeys()
            .Single(x => x.PrincipalEntityType.ClrType == typeof(LaptopModel));

        Assert.Equal(
            [nameof(InspectionSession.BrandId), nameof(InspectionSession.ModelId)],
            foreignKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void ImageHeaderValidationRejectsMimeAndContentMismatch()
    {
        var bytes = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), 320);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), 240);

        var result = ImageFileInspector.Inspect(bytes, "label.png", "image/png");

        Assert.Equal(("image/png", 320, 240), result);
        Assert.Throws<InspectionValidationException>(
            () => ImageFileInspector.Inspect(bytes, "label.png", "image/jpeg"));
    }

    [Fact]
    public void InspectionImageRoutesRequireAuthenticatedRoles()
    {
        var controllerAuthorization = typeof(InspectionsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        var upload = typeof(InspectionsController).GetMethod(nameof(InspectionsController.UploadImage))!;
        var authorization = upload.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        var imageDownload = typeof(InspectionsController).GetMethod(nameof(InspectionsController.GetImage))!;

        Assert.NotNull(controllerAuthorization);
        Assert.Equal("Admin,Supervisor,Operator", authorization.Roles);
        Assert.Empty(imageDownload.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    [Fact]
    public void MultipartUploadDoesNotExplicitlyBindIFormFileFromForm()
    {
        var upload = typeof(InspectionsController).GetMethod(nameof(InspectionsController.UploadImage))!;
        var imageType = upload.GetParameters().Single(parameter => parameter.Name == "imageType");
        var file = upload.GetParameters().Single(parameter => parameter.Name == "file");

        Assert.NotEmpty(imageType.GetCustomAttributes(typeof(FromFormAttribute), true));
        Assert.Empty(file.GetCustomAttributes(typeof(FromFormAttribute), true));
        Assert.Equal(typeof(IFormFile), file.ParameterType);
    }

    [Fact]
    public void PasswordHasherStoresOnlyAOneWayPasswordHash()
    {
        var user = new User
        {
            FullName = "Operator",
            EmployeeId = "operator",
            Email = "operator@example.invalid",
            PasswordHash = string.Empty,
            Role = "Operator"
        };
        var hasher = new PasswordHasher<User>();
        var hash = hasher.HashPassword(user, "correct horse battery staple");

        Assert.NotEqual("correct horse battery staple", hash);
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(user, hash, "correct horse battery staple"));
    }

    [Fact]
    public void OcrResponseContractRetainsRawTextAndArtifactReferences()
    {
        const string json = """
            {
              "status": "PROCESSING",
              "engine": "Tesseract",
              "engine_version": "5.4.0",
              "image_quality": { "width": 640, "height": 480, "blur_score": 42 },
              "lines": [],
              "message": "OCR completed",
              "raw_text": "Model: 15-fd1654TU\nS/N: ABC123",
              "ocr_confidence": 0.94,
              "job_id": "job-001",
              "artifacts": {
                "original_image": "job-001/original.png",
                "cleaned_image": "job-001/cleaned.png",
                "raw_ocr_text": "job-001/ocr.txt",
                "parsed_json": "job-001/parsed.json"
              }
            }
            """;

        var response = JsonSerializer.Deserialize<OcrServiceResponse>(json);

        Assert.NotNull(response);
        Assert.Equal("Model: 15-fd1654TU\nS/N: ABC123", response.RawText);
        Assert.Equal(0.94, response.OcrConfidence);
        Assert.Equal("job-001/parsed.json", response.Artifacts?.ParsedJson);
    }

    private static LabelVerificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LabelVerificationDbContext>()
            .UseSqlServer("Server=(local);Database=SchemaOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new LabelVerificationDbContext(options);
    }

    private static string? GetRoles(System.Reflection.MemberInfo member) =>
        member.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Roles;

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"label-verification-tests-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
}
