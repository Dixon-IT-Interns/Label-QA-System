using Dixon.LabelVerification.API.DTOs.Configuration;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dixon.LabelVerification.API.Controllers;

[ApiController]
[Route("api/brands")]
[Authorize]
public sealed class BrandsController(IBrandConfigurationService brandService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BrandResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BrandResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await brandService.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BrandResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BrandResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await brandService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(typeof(BrandResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<BrandResponse>> Create(
        BrandRequest request,
        CancellationToken cancellationToken)
    {
        var brand = await brandService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = brand.Id }, brand);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(typeof(BrandResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BrandResponse>> Update(
        Guid id,
        BrandRequest request,
        CancellationToken cancellationToken) =>
        Ok(await brandService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await brandService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
