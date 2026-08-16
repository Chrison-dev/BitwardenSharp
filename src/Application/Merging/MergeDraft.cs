using BitwardenSharp.Domain.Duplicates;
using BitwardenSharp.Domain.Vault;

namespace BitwardenSharp.Application.Merging;

/// <summary>Where a resolved merge is written.</summary>
public sealed record MergeTarget
{
    /// <summary>The id of the member being kept, or null to create a brand-new item.</summary>
    public string? ItemId { get; init; }

    public bool IsNewItem => ItemId is null;

    public static MergeTarget Existing(string itemId) => new() { ItemId = itemId };

    public static readonly MergeTarget NewItem = new();
}

/// <summary>Where a resolved value came from, so the UI can show provenance.</summary>
public enum ValueOrigin
{
    /// <summary>Every member agreed; there was nothing to decide.</summary>
    Unanimous,

    /// <summary>Taken from one member.</summary>
    Member,

    /// <summary>Typed by hand — a value that exists in no member.</summary>
    Edited,

    /// <summary>Built from several members, e.g. unioned URIs or concatenated notes.</summary>
    Combined,
}

/// <summary>A resolved scalar, with where it came from.</summary>
public sealed record Resolved<T>(T Value, ValueOrigin Origin, string? SourceItemId = null)
{
    public static Resolved<T> Unanimous(T value) => new(value, ValueOrigin.Unanimous);

    public static Resolved<T> From(T value, string itemId) => new(value, ValueOrigin.Member, itemId);

    public static Resolved<T> Edited(T value) => new(value, ValueOrigin.Edited);

    public static Resolved<T> Combined(T value) => new(value, ValueOrigin.Combined);
}

/// <summary>
/// The editable result of a merge: what the surviving item will contain, and where it is written.
/// </summary>
/// <remarks>
/// <para>
/// This is the model behind the three-pane editor, and it lives in Application rather than the
/// view because it is the thing that decides what gets overwritten. It must be testable without a
/// UI — this is the operation that can destroy data.
/// </para>
/// <para>
/// Unlike <see cref="MergeBuilder"/>'s additive default, a draft may replace the survivor's own
/// values, including its password. See <see cref="Overwrites"/> for how that is surfaced, and
/// <see cref="MergeBuilder.Build(MergeDraft)"/> for how a displaced password is preserved.
/// </para>
/// </remarks>
public sealed record MergeDraft
{
    public required DuplicateGroup Group { get; init; }

    public required MergeTarget Target { get; init; }

    public required Resolved<string> Name { get; init; }
    public Resolved<string?> Username { get; init; } = Resolved<string?>.Unanimous(null);
    public Resolved<string?> Password { get; init; } = Resolved<string?>.Unanimous(null);
    public Resolved<string?> Totp { get; init; } = Resolved<string?>.Unanimous(null);
    public Resolved<string?> FolderId { get; init; } = Resolved<string?>.Unanimous(null);
    public Resolved<string?> Notes { get; init; } = Resolved<string?>.Unanimous(null);
    public Resolved<bool> Favorite { get; init; } = Resolved<bool>.Unanimous(false);

    public IReadOnlyList<LoginUri> Uris { get; init; } = [];
    public IReadOnlyList<CustomField> Fields { get; init; } = [];

    /// <summary>The member being kept, or null when the target is a new item.</summary>
    public VaultItem? TargetItem =>
        Target.IsNewItem ? null : Group.Members.FirstOrDefault(m => m.Id == Target.ItemId);

    /// <summary>Members that will be deleted once the merge is applied.</summary>
    public IEnumerable<VaultItem> Doomed =>
        Group.Members.Where(m => Target.IsNewItem || m.Id != Target.ItemId);

    /// <summary>
    /// Whether the merge may be resolved into a brand-new item.
    /// </summary>
    /// <remarks>
    /// It may not when any member holds an attachment: the CLI cannot move an attachment between
    /// items, so creating a third item and deleting the sources would destroy the file. Merging
    /// into the member that holds it is still fine.
    /// </remarks>
    public bool CanTargetNewItem => Group.Members.All(m => m.Attachments.Count == 0);

    public string? NewItemBlockedReason => CanTargetNewItem
        ? null
        : "One of these items has an attachment, and the Bitwarden CLI cannot move attachments "
          + "between items. Merge into the item that holds it instead.";

    /// <summary>
    /// Values on the target that this draft would replace, as (field, before, after).
    /// </summary>
    /// <remarks>
    /// Empty for a new item — nothing exists to overwrite. This is what the confirmation step
    /// shows: additions are cheap and reversible, replacements are the part worth reading.
    /// </remarks>
    public IReadOnlyList<(string Field, string? Before, string? After)> Overwrites
    {
        get
        {
            var target = TargetItem;
            if (target is null) return [];

            var changes = new List<(string, string?, string?)>();

            // Named 'label' rather than 'field': inside a property accessor, C# 14 treats
            // 'field' as a contextual keyword for the backing field.
            //
            // The comparison is always on the real values and the masking is applied only to what
            // is reported. Masking first would compare bullet strings, and two different secrets
            // of the same length would then look identical — a replaced password reported as no
            // change at all.
            void Compare(string label, string? before, string? after, bool secret = false)
            {
                if (string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal))
                    return;
                changes.Add(secret
                    ? (label, Mask(before), Mask(after))
                    : (label, before, after));
            }

            Compare("Name", target.Name, Name.Value);
            Compare("Username", target.Login?.Username, Username.Value);
            Compare("Password", target.Login?.Password, Password.Value, secret: true);
            Compare("TOTP", target.Login?.Totp, Totp.Value, secret: true);
            Compare("Notes", target.Notes, Notes.Value);

            return changes;
        }
    }

    /// <summary>Whether this draft changes the target's password, which is the risky case.</summary>
    public bool ReplacesPassword =>
        TargetItem is { Login: not null } target
        && !string.IsNullOrEmpty(target.Login.Password)
        && !string.Equals(target.Login.Password, Password.Value, StringComparison.Ordinal);

    private static string? Mask(string? secret) =>
        string.IsNullOrEmpty(secret) ? null : new string('•', Math.Min(secret.Length, 12));

    /// <summary>
    /// The draft the wizard opens with: today's additive merge, expressed as explicit decisions.
    /// </summary>
    /// <remarks>
    /// Deliberately identical in outcome to <see cref="MergeBuilder.Build(VaultItem, IReadOnlyList{VaultItem})"/>
    /// so that "approve the default" in the fast path and "open the editor and change nothing"
    /// produce the same item. Scalars come from the survivor; collections are unioned.
    /// </remarks>
    public static MergeDraft Default(DuplicateGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var survivor = group.Survivor;
        var others = group.Losers.ToList();

        var (merged, _) = MergeBuilder.Build(survivor, others);

        return new MergeDraft
        {
            Group = group,
            Target = MergeTarget.Existing(survivor.Id),
            Name = Agreed(group, m => m.Name) ?? Resolved<string>.From(merged.Name, survivor.Id),
            Username = Agreed(group, m => m.Login?.Username)
                       ?? Resolved<string?>.From(merged.Login?.Username, survivor.Id),
            Password = Agreed(group, m => m.Login?.Password)
                       ?? Resolved<string?>.From(merged.Login?.Password, survivor.Id),
            Totp = Agreed(group, m => m.Login?.Totp)
                   ?? Resolved<string?>.Combined(merged.Login?.Totp),
            FolderId = Agreed(group, m => m.FolderId)
                       ?? Resolved<string?>.From(merged.FolderId, survivor.Id),
            Notes = Agreed(group, m => m.Notes) ?? Resolved<string?>.Combined(merged.Notes),
            Favorite = Agreed(group, m => m.Favorite) ?? Resolved<bool>.From(merged.Favorite, survivor.Id),
            Uris = merged.Uris,
            Fields = merged.Fields,
        };
    }

    /// <summary>A resolved value when every member already agrees, otherwise null.</summary>
    private static Resolved<T>? Agreed<T>(DuplicateGroup group, Func<VaultItem, T> select)
    {
        var values = group.Members.Select(select).Distinct().ToList();
        return values.Count == 1 ? Resolved<T>.Unanimous(values[0]) : null;
    }
}
