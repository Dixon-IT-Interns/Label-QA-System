namespace Dixon.LabelVerification.API.Entities;

public static class ApplicationRoles
{
    public const string Supervisor = "Supervisor";
    public const string Operator = "Operator";

    public static bool IsValid(string? role) =>
        role is Supervisor or Operator;
}
