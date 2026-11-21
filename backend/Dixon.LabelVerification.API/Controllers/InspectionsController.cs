using System.Security.Claims;
using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Dixon.LabelVerification.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dixon.LabelVerification.API.Controllers;

[ApiController]
[Route("api/inspections")]
[Authorize]
public sealed class InspectionsController(
    IInspectionManagementService inspectionService,
    IInspectionReportService reportService,
    IInspectionAccessService inspectionAccess,
    ILogger<InspectionsController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = ApplicationRoles.Supervisor + "," + ApplicationRoles.Operator)]
    [ProducesResponseType(typeof(InspectionDetailsResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<InspectionDetailsResponse>> Create(
        CreateInspectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await inspectionService.CreateAsync(
            request, GetAuthenticatedUserId(), HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Inspection.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedInspectionResponse>> GetAll(
        [FromQuery] InspectionListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await inspectionService.GetAllAsync(
            query, GetAuthenticatedUserId(), IsSupervisor, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InspectionDetailsResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        return Ok(await inspectionService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Supervisor + "," + ApplicationRoles.Operator)]
    public async Task<ActionResult<InspectionDetailsResponse>> Update(
        Guid id,
        CreateInspectionRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        return Ok(await inspectionService.UpdateAsync(
            id, request, GetAuthenticatedUserId(), HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken));
    }

    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = ApplicationRoles.Supervisor + "," + ApplicationRoles.Operator)]
    [RequestSizeLimit(ImageFileInspector.MaximumFileSizeBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(InspectionImageResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<InspectionImageResponse>> UploadImage(
        Guid id,
        [FromForm] InspectionImageType imageType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        var requestId = HttpContext.TraceIdentifier;
        logger.LogInformation(
            "[UPLOAD] request started; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} timeUtc={TimeUtc}",
            requestId, id, imageType, DateTimeOffset.UtcNow);
        try
        {
            var result = await inspectionService.UploadImageAsync(
                id, imageType, file, GetAuthenticatedUserId(),
                HttpContext.Connection.RemoteIpAddress?.ToString(), requestId, cancellationToken);
            logger.LogInformation(
                "[UPLOAD] request completed; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} imageId={ImageId} timeUtc={TimeUtc}",
                requestId, id, imageType, result.Id, DateTimeOffset.UtcNow);
            return CreatedAtAction(nameof(GetImages), new { id }, result);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "[UPLOAD] request failed; requestId={RequestId} inspectionId={InspectionId} imageType={ImageType} exceptionType={ExceptionType} timeUtc={TimeUtc}",
                requestId, id, imageType, exception.GetType().Name, DateTimeOffset.UtcNow);
            throw;
        }
    }

    [HttpGet("{id:guid}/images")]
    public async Task<ActionResult<IReadOnlyList<InspectionImageResponse>>> GetImages(
        Guid id, CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        return Ok(await inspectionService.GetImagesAsync(id, cancellationToken));
    }

    [HttpGet("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> GetImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        var image = await inspectionService.GetImageAsync(id, imageId, cancellationToken);
        return File(image.Content, image.ContentType, image.DownloadFileName);
    }

    [HttpPost("{id:guid}/images/{imageId:guid}/fields/{fieldDefinitionId:guid}/correct")]
    [Authorize(Roles = ApplicationRoles.Supervisor + "," + ApplicationRoles.Operator)]
    public async Task<ActionResult<InspectionDetailsResponse>> CorrectField(
        Guid id,
        Guid imageId,
        Guid fieldDefinitionId,
        ManualFieldCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        return Ok(await inspectionService.CorrectFieldAsync(
            id, imageId, fieldDefinitionId, request, GetAuthenticatedUserId(),
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken));
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [Authorize(Roles = ApplicationRoles.Supervisor + "," + ApplicationRoles.Operator)]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        await inspectionService.DeleteImageAsync(
            id, imageId, GetAuthenticatedUserId(), HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/validate")]
    [Authorize(Roles = ApplicationRoles.Supervisor + "," + ApplicationRoles.Operator)]
    public async Task<ActionResult<object>> Validate(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        return Ok(new { status = await inspectionService.ValidateAsync(
            id, GetAuthenticatedUserId(), HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken) });
    }

    [HttpPost("{id:guid}/process")]
    [Authorize(Roles = ApplicationRoles.Supervisor + "," + ApplicationRoles.Operator)]
    public async Task<ActionResult<object>> Process(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        var requestId = HttpContext.TraceIdentifier;
        logger.LogInformation(
            "PROCESS_REQUEST_START requestId={RequestId} inspectionId={InspectionId} timeUtc={TimeUtc}",
            requestId, id, DateTimeOffset.UtcNow);
        try
        {
            var status = await inspectionService.ProcessAsync(
                id, GetAuthenticatedUserId(), HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            logger.LogInformation(
                "PROCESS_REQUEST_COMPLETED requestId={RequestId} inspectionId={InspectionId} status={Status} timeUtc={TimeUtc}",
                requestId, id, status, DateTimeOffset.UtcNow);
            return Ok(new { status });
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "PROCESS_REQUEST_FAILED requestId={RequestId} inspectionId={InspectionId} exceptionType={ExceptionType} timeUtc={TimeUtc}",
                requestId, id, exception.GetType().Name, DateTimeOffset.UtcNow);
            throw;
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = ApplicationRoles.Supervisor + "," + ApplicationRoles.Operator)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        await inspectionService.CancelAsync(id, GetAuthenticatedUserId(), HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<ActionResult<IReadOnlyList<InspectionLogResponse>>> GetLogs(
        Guid id, CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        return Ok(await inspectionService.GetLogsAsync(id, cancellationToken));
    }

    [HttpGet("{id:guid}/report")]
    public async Task<IActionResult> GetReport(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInspectionAccessAsync(id, cancellationToken);
        var report = await reportService.GenerateOrGetAsync(
            id, GetAuthenticatedUserId(), IsSupervisor, cancellationToken);
        return File(report.Content, "application/pdf", report.FileName);
    }

    private bool IsSupervisor => User.IsInRole(ApplicationRoles.Supervisor);

    private Guid GetAuthenticatedUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("An authenticated user account is required.");
        return userId;
    }

    private Task EnsureInspectionAccessAsync(Guid id, CancellationToken cancellationToken) =>
        inspectionAccess.EnsureCanAccessAsync(
            id, GetAuthenticatedUserId(), IsSupervisor, cancellationToken);
}
