using System.ComponentModel.DataAnnotations;
using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(
    LabelVerificationDbContext db,
    IPasswordHasher<User> passwordHasher,
    AuthenticatedUserContext currentUser) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supervisor)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking()
            .Where(x => x.Role != ApplicationRoles.Admin)
            .AsQueryable();
        if (!currentUser.IsAdmin)
            users = users.Where(x => x.BrandId == currentUser.BrandId && x.Role == ApplicationRoles.Operator);
        return Ok(await users.OrderBy(x => x.FullName)
            .Select(x => new UserResponse(
                x.Id, x.FullName, x.Username, x.Email, x.Role, x.BrandId,
                x.CreatedAtUtc, x.LastLoginAtUtc))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supervisor)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == id && x.Role != ApplicationRoles.Admin,
                cancellationToken)
            ?? throw new ConfigurationNotFoundException($"User '{id}' was not found.");
        if (!currentUser.IsAdmin)
        {
            currentUser.EnsureCanAccessBrand(user.BrandId ?? Guid.Empty);
            if (user.Role != ApplicationRoles.Operator)
                throw new ForbiddenAccessException("Supervisors can view only Operators in their assigned brand.");
        }
        return Ok(ToResponse(user));
    }

    [HttpPost]
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supervisor)]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var (role, brandId) = await ResolveAssignmentAsync(request.Role, request.BrandId, cancellationToken);
        await EnsureUniqueAsync(request.Username, request.Email, null, cancellationToken);
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            Role = role,
            BrandId = brandId,
            PasswordHash = string.Empty
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToResponse(user));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supervisor)]
    public async Task<ActionResult<UserResponse>> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == id && x.Role != ApplicationRoles.Admin,
            cancellationToken)
            ?? throw new ConfigurationNotFoundException($"User '{id}' was not found.");
        if (!currentUser.IsAdmin)
        {
            currentUser.EnsureCanAccessBrand(user.BrandId ?? Guid.Empty);
            if (user.Role != ApplicationRoles.Operator)
                throw new ForbiddenAccessException("Supervisors can manage only Operators in their assigned brand.");
            if (request.Role is not null && request.Role != ApplicationRoles.Operator)
                throw new ForbiddenAccessException("Supervisors cannot change user roles.");
            if (request.BrandId.HasValue)
                currentUser.EnsureCanAccessBrand(request.BrandId.Value);
        }

        await EnsureUniqueAsync(request.Username, request.Email, id, cancellationToken);
        var updatedRole = request.Role ?? user.Role;
        var updatedBrandId = currentUser.IsAdmin
            ? request.BrandId ?? user.BrandId
            : currentUser.BrandId;
        var assignment = await ResolveAssignmentAsync(
            updatedRole,
            updatedBrandId,
            cancellationToken,
            allowUnchangedSupervisorBrand: !currentUser.IsAdmin);

        user.FullName = request.FullName.Trim();
        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim();
        user.Role = assignment.Role;
        user.BrandId = assignment.BrandId;
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(user));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == id && x.Role != ApplicationRoles.Admin,
            cancellationToken)
            ?? throw new ConfigurationNotFoundException($"User '{id}' was not found.");
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<(string Role, Guid? BrandId)> ResolveAssignmentAsync(
        string? requestedRole,
        Guid? requestedBrandId,
        CancellationToken cancellationToken,
        bool allowUnchangedSupervisorBrand = false)
    {
        if (currentUser.IsAdmin)
        {
            var role = requestedRole
                ?? throw new InspectionValidationException("Role must be Supervisor or Operator.");
            if (!ApplicationRoles.IsValid(role))
                throw new InspectionValidationException("Role must be Supervisor or Operator.");
            if (role == ApplicationRoles.Admin)
                throw new ForbiddenAccessException("Admin accounts cannot be created or managed through User Management.");
            if (!requestedBrandId.HasValue)
                throw new InspectionValidationException("Supervisor and Operator accounts require a brand.");
            await EnsureBrandExistsAsync(requestedBrandId.Value, cancellationToken);
            return (role, requestedBrandId);
        }

        if (currentUser.IsSupervisor)
        {
            var assignedBrandId = currentUser.BrandId
                ?? throw new UnauthorizedAccessException("The authenticated Supervisor has no assigned brand.");
            if (requestedRole is not null && requestedRole != ApplicationRoles.Operator)
                throw new ForbiddenAccessException("Supervisors can create and manage only Operators.");
            if (requestedBrandId.HasValue && requestedBrandId.Value != assignedBrandId)
                throw new ForbiddenAccessException("Supervisors cannot assign users to another brand.");
            if (allowUnchangedSupervisorBrand && requestedBrandId != assignedBrandId)
                throw new ForbiddenAccessException("Supervisors cannot change a user's brand.");
            return (ApplicationRoles.Operator, assignedBrandId);
        }

        throw new ForbiddenAccessException("You cannot manage users.");
    }

    private async Task EnsureBrandExistsAsync(Guid brandId, CancellationToken cancellationToken)
    {
        if (!await db.Brands.AnyAsync(x => x.Id == brandId, cancellationToken))
            throw new ConfigurationNotFoundException($"Brand '{brandId}' was not found.");
    }

    private async Task EnsureUniqueAsync(
        string username, string email, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(x => x.Id != excludedId
            && (x.Username == username.Trim() || x.Email == email.Trim()), cancellationToken))
            throw new ConfigurationConflictException("Username or email is already in use.");
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.FullName, user.Username, user.Email, user.Role, user.BrandId,
            user.CreatedAtUtc, user.LastLoginAtUtc);
}

/// <summary>Payload for creating a user account.</summary>
public sealed record CreateUserRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9._-]+$")]
    public string Username { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;

    [StringLength(20)]
    public string? Role { get; init; }

    public Guid? BrandId { get; init; }
}

/// <summary>Payload for updating a user account.</summary>
public sealed record UpdateUserRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9._-]+$")]
    public string Username { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [StringLength(256, MinimumLength = 12)]
    public string? Password { get; init; }

    [StringLength(20)]
    public string? Role { get; init; }

    public Guid? BrandId { get; init; }
}

/// <summary>User account information returned by the API.</summary>
public sealed record UserResponse(
    Guid Id,
    string FullName,
    string Username,
    string Email,
    string Role,
    Guid? BrandId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);
