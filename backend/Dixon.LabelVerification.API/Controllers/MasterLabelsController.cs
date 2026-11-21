using System.Security.Claims;
using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Dixon.LabelVerification.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dixon.LabelVerification.API.Controllers;

[ApiController]
[Route("api/master-labels")]
[Authorize]
public sealed class MasterLabelsController(IMasterLabelService masterLabelService) : ControllerBase
{
    [HttpGet("availability/{modelId:guid}")]
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supervisor + "," + ApplicationRoles.Operator)]
    [ProducesResponseType(typeof(MasterLabelAvailabilityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MasterLabelAvailabilityResponse>> GetAvailability(
        Guid modelId, CancellationToken cancellationToken) =>
        Ok(await masterLabelService.GetAvailabilityAsync(modelId, cancellationToken));

    [HttpGet]
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supervisor)]
    [ProducesResponseType(typeof(IReadOnlyList<MasterLabelResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterLabelResponse>>> GetAll(
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? modelId,
        CancellationToken cancellationToken) =>
        Ok(await masterLabelService.GetAllAsync(brandId, modelId, cancellationToken));

    [HttpGet("models/{modelId:guid}")]
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supervisor)]
    [ProducesResponseType(typeof(IReadOnlyList<MasterLabelResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterLabelResponse>>> GetByModel(
        Guid modelId, CancellationToken cancellationToken) =>
        Ok(await masterLabelService.GetByModelAsync(modelId, cancellationToken));

    [HttpPut("{modelId:guid}/pair")]
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supervisor)]
    [RequestSizeLimit(2 * ImageFileInspector.MaximumFileSizeBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(MasterLabelPairResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MasterLabelPairResponse>> SavePair(
        Guid modelId,
        [FromForm] Guid brandId,
        [FromForm] IFormFile? coFile,
        [FromForm] IFormFile? configFile,
        CancellationToken cancellationToken)
    {
        if (coFile is null || configFile is null)
            throw new InspectionValidationException("Both COO and Config master images are required.");
        var result = await masterLabelService.SavePairAsync(
            new SaveMasterLabelPairRequest
            {
                BrandId = brandId,
                ModelId = modelId,
                CoFile = coFile,
                ConfigFile = configFile
            },
            GetAuthenticatedUserId(),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("detect-model")]
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supervisor)]
    [RequestSizeLimit(ImageFileInspector.MaximumFileSizeBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(DetectedModelResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DetectedModelResponse>> DetectModel(
        [FromForm] DetectModelRequest request, CancellationToken cancellationToken) =>
        Ok(await masterLabelService.DetectModelAsync(request, cancellationToken));

    [HttpGet("{modelId:guid}/{labelType}/image")]
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supervisor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImage(
        Guid modelId, LabelType labelType, CancellationToken cancellationToken)
    {
        var image = await masterLabelService.GetImageAsync(modelId, labelType, cancellationToken);
        return File(image.Content, image.ContentType, image.DownloadFileName);
    }

    private Guid GetAuthenticatedUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user ID claim is missing.");
    }
}
