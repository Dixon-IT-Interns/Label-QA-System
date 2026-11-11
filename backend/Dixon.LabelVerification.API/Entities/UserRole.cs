namespace Dixon.LabelVerification.API.Entities;

public sealed class UserRole
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
