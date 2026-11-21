using Dixon.LabelVerification.API.DTOs.Configuration;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dixon.LabelVerification.API.Controllers;

[ApiController]
[Route("api/models")]
[Authorize]
public sealed class ModelsController(IModelConfigurationService modelService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ModelResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ModelResponse>>> GetAll(
        [FromQuery] Guid? brandId,
        CancellationToken cancellationToken) =>
        Ok(await modelService.GetAllAsync(brandId, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ModelResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ModelResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await modelService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(typeof(ModelResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ModelResponse>> Create(
        ModelRequest request,
        CancellationToken cancellationToken)
    {
        var model = await modelService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(typeof(ModelResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ModelResponse>> Update(
        Guid id,
        ModelRequest request,
        CancellationToken cancellationToken) =>
        Ok(await modelService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await modelService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
