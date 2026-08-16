namespace BitwardenSharp.Domain.Vault;

/// <summary>Custom-field kind, using the CLI's own numeric values.</summary>
public enum FieldType
{
    Text = 0,
    Hidden = 1,
    Boolean = 2,
    Linked = 3,
}
