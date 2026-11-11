namespace Dixon.LabelVerification.API.Entities;

public sealed class FieldAlias : AuditedEntity
{
    public Guid FieldDefinitionId { get; set; }
    public required string Value { get; set; }
    public string Language { get; set; } = "en";
    public bool IsActive { get; set; } = true;
    public FieldDefinition FieldDefinition { get; set; } = null!;
}
