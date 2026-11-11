using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthenticationController(
    LabelVerificationDbContext db,
    IPasswordHasher<User> passwordHasher,
    JwtTokenService tokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Username == identifier || x.Email == identifier, cancellationToken);
        if (user is null || !user.IsActive
            || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "The username/email or password is incorrect."
            });
        }

        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var (token, expiresAtUtc) = tokenService.CreateToken(user);
        return Ok(new LoginResponse(
            token,
            expiresAtUtc,
            new AuthenticatedUserResponse(user.Id, user.FullName, user.Username, user.Email, user.Role)));
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out var userId))
            return Unauthorized();
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (user is null)
            return Unauthorized();
        return Ok(new AuthenticatedUserResponse(user.Id, user.FullName, user.Username, user.Email, user.Role));
    }
}

public sealed class LoginRequest
{
    [Required, StringLength(256, MinimumLength = 1)]
    public string Identifier { get; init; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 1)]
    public string Password { get; init; } = string.Empty;
}

public sealed record AuthenticatedUserResponse(Guid Id, string FullName, string Username, string Email, string Role);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, AuthenticatedUserResponse User);
