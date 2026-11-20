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
[Authorize(Roles = ApplicationRoles.Supervisor)]
public sealed class UsersController(
    LabelVerificationDbContext db,
    IPasswordHasher<User> passwordHasher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await db.Users.AsNoTracking().OrderBy(x => x.FullName)
            .Select(x => new UserResponse(
                x.Id, x.FullName, x.Username, x.Email, x.Role, x.IsActive, x.CreatedAtUtc, x.LastLoginAtUtc))
            .ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(UserRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueAsync(request.Username, request.Email, null, cancellationToken);
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            Role = ApplicationRoles.Operator,
            PasswordHash = string.Empty,
            IsActive = request.IsActive
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ToResponse(user));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"User '{id}' was not found.");
        if (user.Role != ApplicationRoles.Operator)
            throw new ConfigurationConflictException("Only Operator accounts can be edited here.");
        await EnsureUniqueAsync(request.Username, request.Email, id, cancellationToken);
        user.FullName = request.FullName.Trim();
        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim();
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(user));
    }

    private async Task EnsureUniqueAsync(
        string username, string email, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(x => x.Id != excludedId
            && (x.Username == username.Trim() || x.Email == email.Trim()), cancellationToken))
            throw new ConfigurationConflictException("Username or email is already in use.");
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.FullName, user.Username, user.Email, user.Role, user.IsActive,
            user.CreatedAtUtc, user.LastLoginAtUtc);
}

public sealed class UserRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9._-]+$")]
    public string Username { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;
    [Required, StringLength(256, MinimumLength = 12)]
    public string? Password { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateUserRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9._-]+$")]
    public string Username { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;
    [StringLength(256, MinimumLength = 12)]
    public string? Password { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record UserResponse(
    Guid Id,
    string FullName,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);
