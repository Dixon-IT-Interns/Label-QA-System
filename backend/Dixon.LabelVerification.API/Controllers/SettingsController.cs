using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Roles = "Admin")]
public sealed class SettingsController(LabelVerificationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemSettingResponse>>> Get(CancellationToken cancellationToken)
    {
        var settings = await db.SystemSettings.AsNoTracking().OrderBy(x => x.SettingKey)
            .Select(x => new SystemSettingResponse(x.SettingKey, x.SettingValue, x.Description, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPut("ocr-confidence")]
    public async Task<ActionResult<SystemSettingResponse>> SetOcrConfidence(
        OcrConfidenceSettingRequest request,
        CancellationToken cancellationToken)
    {
        var setting = await db.SystemSettings.SingleOrDefaultAsync(
            x => x.SettingKey == "OcrMinimumConfidence", cancellationToken);
        if (setting is null)
        {
            setting = new SystemSetting
            {
                SettingKey = "OcrMinimumConfidence",
                Description = "Minimum OCR confidence required before configured field values can be compared."
            };
            db.SystemSettings.Add(setting);
        }

        setting.SettingValue = request.Value.ToString("0.####", CultureInfo.InvariantCulture);
        setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new SystemSettingResponse(
            setting.SettingKey, setting.SettingValue, setting.Description, setting.UpdatedAtUtc));
    }
}

public sealed class OcrConfidenceSettingRequest
{
    [Range(0, 1)]
    public double Value { get; init; }
}

public sealed record SystemSettingResponse(
    string Key, string? Value, string? Description, DateTimeOffset UpdatedAtUtc);
