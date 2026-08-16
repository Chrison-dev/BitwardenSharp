namespace BitwardenSharp.Domain.Vault;

/// <summary>
/// How Bitwarden decides a URI matches the current page. Null on an item means "inherit the
/// vault default", which is why <see cref="LoginUri.Match"/> is nullable rather than defaulted.
/// </summary>
public enum UriMatchType
{
    Domain = 0,
    Host = 1,
    StartsWith = 2,
    Exact = 3,
    RegularExpression = 4,
    Never = 5,
}
