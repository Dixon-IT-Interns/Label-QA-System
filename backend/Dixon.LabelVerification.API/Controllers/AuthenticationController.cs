using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
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
    JwtTokenService tokenService,
    PasswordResetEmailSender passwordResetEmailSender,
    IConfiguration configuration,
    ILogger<AuthenticationController> logger) : ControllerBase
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
        return Ok(CreateLoginResponse(user));
    }

    [AllowAnonymous]
    [HttpPost("signup")]
    public async Task<ActionResult<AuthenticatedUserResponse>> SignUp(
        SignUpRequest request,
        CancellationToken cancellationToken)
    {
        var fullName = request.FullName.Trim();
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(
            x => x.Username == username || x.Email == email, cancellationToken))
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "That username or email address is already in use."
            });

        var user = new User
        {
            FullName = fullName,
            Username = username,
            Email = email,
            Role = "Operator",
            PasswordHash = string.Empty,
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            new AuthenticatedUserResponse(user.Id, user.FullName, user.Username, user.Email, user.Role));
    }

    [AllowAnonymous]
    [HttpPost("google")]
    public async Task<ActionResult<LoginResponse>> GoogleSignIn(
        GoogleSignInRequest request,
        CancellationToken cancellationToken)
    {
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Google sign-in is not configured."
            });

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.Credential,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Google sign-in could not be verified."
            });
        }

        if (payload.EmailVerified != true || string.IsNullOrWhiteSpace(payload.Email))
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Google sign-in requires a verified email address."
            });

        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Email == payload.Email && x.IsActive, cancellationToken);
        if (user is null)
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Google sign-in is available only for an existing active account with the same email."
            });

        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(CreateLoginResponse(user));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!passwordResetEmailSender.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Password reset email is not configured on the server."
            });

        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Email == request.Email.Trim() && x.IsActive, cancellationToken);
        const string confirmation = "If an active account matches that email, a password reset link will be sent.";
        if (user is null)
            return Accepted(new { message = confirmation });

        var token = CreatePasswordResetToken();
        user.PasswordResetTokenHash = HashPasswordResetToken(token);
        user.PasswordResetExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(15);
        await db.SaveChangesAsync(cancellationToken);

        var frontendBaseUrl = (configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var resetUrl = $"{frontendBaseUrl}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";
        try
        {
            await passwordResetEmailSender.SendPasswordResetAsync(user.Email, resetUrl, cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            logger.LogError(exception, "Password reset email delivery failed.");
            user.PasswordResetTokenHash = null;
            user.PasswordResetExpiresAtUtc = null;
            await db.SaveChangesAsync(cancellationToken);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Password reset email could not be sent. Please try again later."
            });
        }

        return Accepted(new { message = confirmation });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Email == request.Email.Trim() && x.IsActive, cancellationToken);
        if (user?.PasswordResetTokenHash is null
            || user.PasswordResetExpiresAtUtc is null
            || user.PasswordResetExpiresAtUtc.Value <= DateTimeOffset.UtcNow
            || !PasswordResetTokenMatches(request.Token, user.PasswordResetTokenHash))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The password reset link is invalid or has expired."
            });

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.PasswordResetTokenHash = null;
        user.PasswordResetExpiresAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { message = "Your password has been reset. You can now sign in." });
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

    private LoginResponse CreateLoginResponse(User user)
    {
        var (token, expiresAtUtc) = tokenService.CreateToken(user);
        return new LoginResponse(
            token,
            expiresAtUtc,
            new AuthenticatedUserResponse(user.Id, user.FullName, user.Username, user.Email, user.Role));
    }

    private static string CreatePasswordResetToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string HashPasswordResetToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool PasswordResetTokenMatches(string token, string storedHash)
    {
        var expectedHash = Convert.FromHexString(storedHash);
        var providedHash = Convert.FromHexString(HashPasswordResetToken(token));
        return expectedHash.Length == providedHash.Length
            && CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
    }
}

public sealed class LoginRequest
{
    [Required, StringLength(256, MinimumLength = 1)]
    public string Identifier { get; init; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 1)]
    public string Password { get; init; } = string.Empty;
}

public sealed class SignUpRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9._-]+$")]
    public string Username { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;
}

public sealed class GoogleSignInRequest
{
    [Required]
    public string Credential { get; init; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 20)]
    public string Token { get; init; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;
}

public sealed record AuthenticatedUserResponse(Guid Id, string FullName, string Username, string Email, string Role);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, AuthenticatedUserResponse User);
