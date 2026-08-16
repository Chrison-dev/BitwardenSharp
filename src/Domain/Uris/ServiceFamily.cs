namespace BitwardenSharp.Domain.Uris;

/// <summary>
/// Groups brands that are one account behind different front doors — gmail.com and youtube.com
/// are one Google login, live.com and outlook.com are one Microsoft login.
/// </summary>
/// <remarks>
/// Membership here is a claim that a single set of credentials genuinely signs into all of them.
/// It is deliberately conservative: a wrong entry causes two real, separate accounts to be
/// proposed for merge, and the loser is deleted. When in doubt, leave a brand out — the cost of
/// omission is one duplicate that survives, the cost of a false entry is a lost account.
/// </remarks>
public static class ServiceFamily
{
    private static readonly Dictionary<string, string> BrandToFamily = BuildIndex(new()
    {
        ["google"] = ["google", "gmail", "googlemail", "youtube", "googleapis", "blogger", "firebase"],
        ["microsoft"] = ["microsoft", "live", "outlook", "hotmail", "office", "office365", "msn",
                         "azure", "xbox", "skype", "sharepoint", "microsoftonline", "onedrive"],
        ["amazon"] = ["amazon", "audible", "primevideo", "kindle"],
        ["apple"] = ["apple", "icloud", "itunes"],
        ["meta"] = ["facebook", "instagram", "whatsapp", "messenger", "meta", "oculus"],
        ["atlassian"] = ["atlassian", "jira", "confluence", "bitbucket", "trello"],
        ["valve"] = ["steampowered", "steamcommunity", "valvesoftware", "steam"],
        ["ubisoft"] = ["ubisoft", "ubi", "uplay"],
        ["ea"] = ["origin", "eaplay"],
        ["adobe"] = ["adobe", "behance"],
        ["sony"] = ["sony", "playstation", "sonyentertainmentnetwork"],
        ["proton"] = ["proton", "protonvpn", "protonmail"],
        ["x"] = ["twitter"],
    });

    private static Dictionary<string, string> BuildIndex(Dictionary<string, string[]> families)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (family, brands) in families)
            foreach (var brand in brands)
                index[brand] = family;
        return index;
    }

    /// <summary>The family a brand belongs to, or null when it is not part of a known one.</summary>
    public static string? ForBrand(string? brand) =>
        brand is not null && BrandToFamily.TryGetValue(brand, out var family) ? family : null;

    /// <summary>The family a target belongs to, or null for IPs, hosts, apps and unknown brands.</summary>
    public static string? ForTarget(UriTarget target) => ForBrand(target.Brand);
}
