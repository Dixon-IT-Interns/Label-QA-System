using System.Security.Claims;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Services;

public sealed class AuthenticatedUserContext(IHttpContextAccessor httpContextAccessor)
{
    private ClaimsPrincipal Principal =>
        httpContextAccessor.HttpContext?.User
        ?? throw new UnauthorizedAccessException("An authenticated user account is required.");

    public Guid UserId =>
        Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user ID claim is invalid.");

    public string Role =>
        Principal.FindFirstValue(ClaimTypes.Role)
        ?? throw new UnauthorizedAccessException("The authenticated user role claim is missing.");

    public Guid? BrandId
    {
        get
        {
            var value = Principal.FindFirstValue("brand_id");
            if (value is null)
                return null;
            return Guid.TryParse(value, out var brandId)
                ? brandId
                : throw new UnauthorizedAccessException("The authenticated user brand claim is invalid.");
        }
    }

    public bool IsAdmin => Role == ApplicationRoles.Admin;
    public bool IsSupervisor => Role == ApplicationRoles.Supervisor;
    public bool IsOperator => Role == ApplicationRoles.Operator;

    public void EnsureCanAccessBrand(Guid brandId)
    {
        if (!IsAdmin && BrandId != brandId)
            throw new ForbiddenAccessException("You do not have access to this brand.");
    }

    public void EnsureCanManageConfiguration()
    {
        if (!IsAdmin && !IsSupervisor)
            throw new ForbiddenAccessException("Only Admins and Supervisors can manage configuration.");
    }

    public void EnsureAdmin()
    {
        if (!IsAdmin)
            throw new ForbiddenAccessException("Only Admins can perform this operation.");
    }
}

public sealed class ForbiddenAccessException(string message) : Exception(message);
