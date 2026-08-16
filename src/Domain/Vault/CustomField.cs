namespace BitwardenSharp.Domain.Vault;

/// <summary>A user-defined field on an item.</summary>
public sealed record CustomField
{
    public string? Name { get; init; }

    public string? Value { get; init; }

    public FieldType Type { get; init; }

    public string? LinkedId { get; init; }

    /// <summary>
    /// Redacts <see cref="Value"/>: a field may be <see cref="FieldType.Hidden"/>, and this is
    /// exactly where API keys and recovery codes end up.
    /// </summary>
    public override string ToString() => $"CustomField {{ Name = {Name}, Type = {Type} }}";
}
