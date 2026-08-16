namespace BitwardenSharp.Domain.Duplicates;

/// <summary>Why a set of items was grouped, and therefore what may safely be done with it.</summary>
public enum DuplicateCategory
{
    /// <summary>
    /// Same registrable target, same username, same password. One account recorded more than
    /// once — almost always a browser importing each subdomain it saw as its own entry.
    /// </summary>
    ExactDuplicate,

    /// <summary>
    /// Same username and password across different but genuinely related domains — the same
    /// brand under two TLDs, or two front doors onto one <see cref="Uris.ServiceFamily"/>.
    /// Merging keeps every URI so autofill still fires on all of them.
    /// </summary>
    RelatedDomain,

    /// <summary>
    /// Same target and username but the passwords differ. One is stale; which one cannot be
    /// determined from the vault alone. Never merged automatically.
    /// </summary>
    CredentialConflict,

    /// <summary>
    /// One credential reused across several distinct hosts or IPs. These are separate machines
    /// that happen to share a login, not duplicates — merging them would delete the inventory.
    /// Reported so the sprawl is visible, never merged.
    /// </summary>
    InfrastructureSharedCredential,

    /// <summary>
    /// Identical item name but the credentials differ. Too weak to act on alone; surfaced for a
    /// human to look at.
    /// </summary>
    SameName,
}

/// <summary>What the tool is willing to do with a group without being told twice.</summary>
public enum MergeDisposition
{
    /// <summary>Safe to merge once the operator has approved the group.</summary>
    Mergeable,

    /// <summary>Reported only. Requires a human decision that the vault data cannot supply.</summary>
    ReviewOnly,
}

public static class DuplicateCategoryExtensions
{
    /// <summary>
    /// Whether a category may ever be merged. Kept beside the enum rather than decided at the
    /// call site so no future code path can quietly treat a conflict as mergeable.
    /// </summary>
    public static MergeDisposition Disposition(this DuplicateCategory category) => category switch
    {
        DuplicateCategory.ExactDuplicate => MergeDisposition.Mergeable,
        DuplicateCategory.RelatedDomain => MergeDisposition.Mergeable,
        _ => MergeDisposition.ReviewOnly,
    };
}
