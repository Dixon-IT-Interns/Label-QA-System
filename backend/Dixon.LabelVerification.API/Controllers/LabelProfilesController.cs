using Dixon.LabelVerification.API.DTOs.Configuration;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dixon.LabelVerification.API.Controllers;

[ApiController]
[Route("api/label-profiles")]
[Authorize]
public sealed class LabelProfilesController(
    ILabelProfileConfigurationService profileService,
    IFieldDefinitionConfigurationService fieldService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LabelProfileResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LabelProfileResponse>>> GetAll(
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? modelId,
        [FromQuery] LabelType? labelType,
        CancellationToken cancellationToken) =>
        Ok(await profileService.GetAllAsync(brandId, modelId, labelType, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LabelProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LabelProfileResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await profileService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(typeof(LabelProfileResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<LabelProfileResponse>> Create(
        LabelProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await profileService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, profile);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(typeof(LabelProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LabelProfileResponse>> Update(
        Guid id,
        LabelProfileRequest request,
        CancellationToken cancellationToken) =>
        Ok(await profileService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await profileService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{labelProfileId:guid}/fields")]
    [ProducesResponseType(typeof(IReadOnlyList<FieldDefinitionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FieldDefinitionResponse>>> GetFields(
        Guid labelProfileId,
        CancellationToken cancellationToken) =>
        Ok(await fieldService.GetAllAsync(labelProfileId, cancellationToken));

    [HttpGet("{labelProfileId:guid}/fields/match")]
    [ProducesResponseType(typeof(IReadOnlyList<FieldDefinitionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FieldDefinitionResponse>>> FindFieldsByOcrName(
        Guid labelProfileId,
        [FromQuery, System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(200)]
        string label,
        CancellationToken cancellationToken) =>
        Ok(await fieldService.FindByOcrNameAsync(labelProfileId, label, cancellationToken));

    [HttpPost("{labelProfileId:guid}/fields")]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(typeof(FieldDefinitionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<FieldDefinitionResponse>> CreateField(
        Guid labelProfileId,
        FieldDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var field = await fieldService.CreateAsync(labelProfileId, request, cancellationToken);
        return CreatedAtAction(nameof(GetFieldById), new { id = field.Id }, field);
    }

    [HttpGet("/api/field-definitions")]
    [ProducesResponseType(typeof(IReadOnlyList<FieldDefinitionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FieldDefinitionResponse>>> GetAllFields(
        [FromQuery] Guid? labelProfileId,
        CancellationToken cancellationToken) =>
        Ok(await fieldService.GetAllAsync(labelProfileId, cancellationToken));

    [HttpGet("/api/field-definitions/{id:guid}")]
    [ProducesResponseType(typeof(FieldDefinitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FieldDefinitionResponse>> GetFieldById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await fieldService.GetByIdAsync(id, cancellationToken));

    [HttpPut("/api/field-definitions/{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(typeof(FieldDefinitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FieldDefinitionResponse>> UpdateField(
        Guid id,
        FieldDefinitionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await fieldService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("/api/field-definitions/{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteField(Guid id, CancellationToken cancellationToken)
    {
        await fieldService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
