namespace Dixon.LabelVerification.API.Entities;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string Supervisor = "Supervisor";
    public const string Operator = "Operator";

    public static bool IsValid(string? role) =>
        role is Admin or Supervisor or Operator;

    public static bool RequiresBrand(string role) =>
        role is Supervisor or Operator;

    public static bool HasValidBrandAssignment(string? role, Guid? brandId) =>
        role == Admin ? brandId is null : RequiresBrand(role ?? string.Empty) && brandId is not null;
}
