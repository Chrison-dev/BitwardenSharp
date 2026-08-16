namespace BitwardenSharp.Domain.Uris;

/// <summary>
/// The multi-label public suffixes needed to find the registrable domain (eTLD+1).
/// </summary>
/// <remarks>
/// <para>
/// This is a curated subset, not the full Public Suffix List. The full list is ~10k entries that
/// change monthly and would need a fetched, versioned data file; the failure mode of a missing
/// entry here is narrow and safe — <c>foo.co.zz</c> would reduce to <c>co.zz</c> instead of
/// <c>foo.co.zz</c>, which can only make two items look MORE alike, and every merge is reviewed
/// before it is applied.
/// </para>
/// <para>
/// Weighted towards where the vault actually lives: New Zealand, the UK, Germany and Australia.
/// Add entries as they come up rather than importing the world.
/// </para>
/// </remarks>
public static class PublicSuffix
{
    private static readonly HashSet<string> MultiLabel = new(StringComparer.OrdinalIgnoreCase)
    {
        // New Zealand
        "co.nz", "net.nz", "org.nz", "govt.nz", "ac.nz", "school.nz", "geek.nz", "kiwi.nz",
        // United Kingdom
        "co.uk", "org.uk", "ac.uk", "gov.uk", "me.uk", "net.uk", "plc.uk", "ltd.uk",
        // Australia
        "com.au", "net.au", "org.au", "edu.au", "gov.au", "asn.au", "id.au",
        // Japan
        "co.jp", "or.jp", "ne.jp", "ac.jp", "go.jp",
        // Rest of the world, as encountered
        "com.br", "com.cn", "net.cn", "org.cn", "gov.cn", "edu.cn",
        "co.za", "org.za", "co.in", "net.in", "org.in", "com.mx", "com.ar", "com.co",
        "co.kr", "or.kr", "com.sg", "com.hk", "com.tw", "co.il", "com.tr", "co.id",
        "com.my", "co.th", "com.ph", "com.vn", "com.pk", "com.eg", "com.sa", "com.ua",
        "com.pl", "com.ru", "com.es", "co.ke", "com.ng", "com.pe", "com.ve", "com.uy",
        "co.at", "co.hu", "com.de", "com.ee", "com.hr", "com.gr", "com.cy", "com.mt",
    };

    /// <summary>True when the final two labels of a host form a known multi-label suffix.</summary>
    public static bool IsMultiLabelSuffix(string lastTwoLabels) => MultiLabel.Contains(lastTwoLabels);
}
