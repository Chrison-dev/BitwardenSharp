using System.Collections.ObjectModel;
using BitwardenSharp.Application.Merging;
using BitwardenSharp.Domain.Vault;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitwardenSharp.Desktop.ViewModels;

/// <summary>One scalar property, compared across the group and resolved in the middle.</summary>
public sealed partial class MergePropertyRow : ObservableObject
{
    private readonly Func<VaultItem, string?> _read;

    public MergePropertyRow(
        string label,
        Func<VaultItem, string?> read,
        IReadOnlyList<VaultItem> members,
        string? resolved,
        bool isSecret = false)
    {
        Label = label;
        _read = read;
        IsSecret = isSecret;
        Members = members;
        _resultValue = resolved;

        var distinct = members.Select(read)
            .Select(v => v ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        IsIdentical = distinct.Count <= 1;
    }

    public string Label { get; }
    public bool IsSecret { get; }
    public IReadOnlyList<VaultItem> Members { get; }

    /// <summary>Every member agrees, so there is nothing here to decide.</summary>
    public bool IsIdentical { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue), nameof(IsEdited))]
    private string? _resultValue;

    /// <summary>The value carried by whichever member is currently in the compare pane.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompareDisplay), nameof(DiffersFromCompare))]
    private VaultItem? _comparedMember;

    public string? CompareValue => ComparedMember is null ? null : _read(ComparedMember);

    public bool DiffersFromCompare =>
        !string.Equals(CompareValue ?? string.Empty, ResultValue ?? string.Empty, StringComparison.Ordinal);

    /// <summary>True once the value matches no member — i.e. it was typed.</summary>
    public bool IsEdited =>
        !string.IsNullOrEmpty(ResultValue)
        && !Members.Any(m => string.Equals(_read(m), ResultValue, StringComparison.Ordinal));

    // Secrets are masked in both panes until the editor is set to reveal. A merge decision is
    // about which value wins, and you can make it from "these differ" without reading either.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue), nameof(CompareDisplay))]
    private bool _reveal;

    public string? DisplayValue => Render(ResultValue);
    public string? CompareDisplay => Render(CompareValue);

    private string? Render(string? value) =>
        IsSecret && !Reveal && !string.IsNullOrEmpty(value)
            ? new string('•', Math.Min(value.Length, 16))
            : value;

    /// <summary>Pull the compared member's value into the result.</summary>
    [RelayCommand]
    private void TakeFromCompare() => ResultValue = CompareValue;
}

/// <summary>A URI or custom field, included in the result or not.</summary>
public sealed partial class MergeElementRow(string text, string detail, bool included = true)
    : ObservableObject
{
    public string Text { get; } = text;

    /// <summary>Which members carry this element.</summary>
    public string Detail { get; } = detail;

    [ObservableProperty] private bool _isIncluded = included;
}

/// <summary>One option on the "what does the result become" radio.</summary>
public sealed partial class MergeTargetOption(
    string label, string detail, MergeTarget target, bool enabled = true, string? blockedReason = null)
    : ObservableObject
{
    public string Label { get; } = label;
    public string Detail { get; } = detail;
    public MergeTarget Target { get; } = target;
    public bool IsEnabled { get; } = enabled;
    public string? BlockedReason { get; } = blockedReason;
    public bool IsBlocked => !IsEnabled;

    [ObservableProperty] private bool _isChosen;
}

/// <summary>
/// The three-pane merge editor: members on the left, one of them compared in the middle, and the
/// resolved result on the right.
/// </summary>
/// <remarks>
/// The left is a rail rather than a single pane because 15 of the mergeable groups on a real vault
/// have three to five members; a strict two-pane layout has nowhere to put the rest. For a
/// two-member group — the large majority — the rail holds one other item and it reads exactly like
/// a plain side-by-side.
/// </remarks>
public sealed partial class MergeEditorViewModel : ViewModelBase
{
    private readonly DuplicateGroupViewModel _row;

    public event Action? Cancelled;
    public event Action<MergeDraft>? Committed;

    public MergeEditorViewModel(DuplicateGroupViewModel row)
    {
        _row = row;
        var group = row.Group;
        var draft = row.Draft;

        Members = [.. group.Members];

        Rows =
        [
            new MergePropertyRow("Name", m => m.Name, Members, draft.Name.Value),
            new MergePropertyRow("Username", m => m.Login?.Username, Members, draft.Username.Value),
            new MergePropertyRow("Password", m => m.Login?.Password, Members, draft.Password.Value, isSecret: true),
            new MergePropertyRow("TOTP", m => m.Login?.Totp, Members, draft.Totp.Value, isSecret: true),
            new MergePropertyRow("Notes", m => m.Notes, Members, draft.Notes.Value),
        ];

        // URIs and custom fields are unioned by default and individually removable: "additive"
        // only means anything for collections — you cannot have two usernames.
        Uris =
        [
            .. group.Members
                .SelectMany(m => m.Uris.Select(u => (Member: m, u.Uri)))
                .GroupBy(x => x.Uri.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new MergeElementRow(
                    g.Key,
                    $"from {string.Join(", ", g.Select(x => x.Member.Name).Distinct())}",
                    draft.Uris.Any(u => string.Equals(u.Uri.Trim(), g.Key, StringComparison.OrdinalIgnoreCase)))),
        ];

        Fields =
        [
            .. group.Members
                .SelectMany(m => m.Fields.Select(f => (Member: m, Field: f)))
                .GroupBy(x => x.Field.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g => new MergeElementRow(
                    g.Key,
                    $"from {string.Join(", ", g.Select(x => x.Member.Name).Distinct())}",
                    draft.Fields.Any(f => string.Equals(f.Name, g.Key, StringComparison.OrdinalIgnoreCase)))),
        ];

        Targets =
        [
            .. group.Members.Select(m => new MergeTargetOption(
                $"Keep \"{m.Name}\"",
                m.Attachments.Count > 0
                    ? $"{m.Uris.Count} uri(s) · {m.Attachments.Count} attachment(s)"
                    : $"{m.Uris.Count} uri(s)",
                MergeTarget.Existing(m.Id))),
            new MergeTargetOption(
                "Create a new item",
                draft.CanTargetNewItem
                    ? $"all {group.Members.Count} originals are deleted"
                    : draft.NewItemBlockedReason!,
                MergeTarget.NewItem,
                enabled: draft.CanTargetNewItem,
                blockedReason: draft.NewItemBlockedReason),
        ];

        ChosenTarget = Targets.FirstOrDefault(t => t.Target.ItemId == draft.Target.ItemId) ?? Targets[0];
        SelectedMember = Members.FirstOrDefault(m => m.Id != draft.Target.ItemId) ?? Members[0];
    }

    public IReadOnlyList<VaultItem> Members { get; }
    public ObservableCollection<MergePropertyRow> Rows { get; }
    public ObservableCollection<MergeElementRow> Uris { get; }
    public ObservableCollection<MergeElementRow> Fields { get; }
    public ObservableCollection<MergeTargetOption> Targets { get; }

    public string GroupId => _row.Id;
    public string GroupKey => _row.Key;
    public bool HasFields => Fields.Count > 0;

    /// <summary>The member currently shown in the compare pane.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompareTitle))]
    private VaultItem? _selectedMember;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultTitle), nameof(TargetIsNewItem))]
    private MergeTargetOption? _chosenTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleRows))]
    private bool _showIdentical;

    [ObservableProperty] private bool _revealSecrets;

    public string CompareTitle => SelectedMember?.Name ?? "—";

    public string ResultTitle => ChosenTarget?.Target.IsNewItem == true
        ? "Result — a new item"
        : $"Result — {ChosenTarget?.Label}";

    public bool TargetIsNewItem => ChosenTarget?.Target.IsNewItem == true;

    /// <summary>
    /// Identical rows are hidden by default. Most properties agree — that is what made these
    /// duplicates — and showing them all buries the one or two that actually need a decision.
    /// </summary>
    public IEnumerable<MergePropertyRow> VisibleRows => Rows.Where(r => ShowIdentical || !r.IsIdentical);

    partial void OnSelectedMemberChanged(VaultItem? value)
    {
        foreach (var row in Rows) row.ComparedMember = value;
    }

    partial void OnChosenTargetChanged(MergeTargetOption? value)
    {
        foreach (var option in Targets) option.IsChosen = ReferenceEquals(option, value);
    }

    partial void OnRevealSecretsChanged(bool value)
    {
        foreach (var row in Rows) row.Reveal = value;
    }

    /// <summary>What this draft would replace on the item being kept.</summary>
    public IReadOnlyList<(string Field, string? Before, string? After)> Overwrites => Build().Overwrites;

    /// <summary>Assembles the draft the panes currently describe.</summary>
    public MergeDraft Build()
    {
        var byLabel = Rows.ToDictionary(r => r.Label, r => r.ResultValue);
        var group = _row.Group;

        var keptUris = Uris.Where(u => u.IsIncluded).Select(u => u.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keptFields = Fields.Where(f => f.IsIncluded).Select(f => f.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _row.Draft with
        {
            Target = ChosenTarget?.Target ?? _row.Draft.Target,
            Name = Resolve(byLabel["Name"] ?? string.Empty, m => m.Name),
            Username = Resolve(byLabel["Username"], m => m.Login?.Username),
            Password = Resolve(byLabel["Password"], m => m.Login?.Password),
            Totp = Resolve(byLabel["TOTP"], m => m.Login?.Totp),
            Notes = Resolve(byLabel["Notes"], m => m.Notes),
            Uris = group.Members
                .SelectMany(m => m.Uris)
                .GroupBy(u => u.Uri.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => keptUris.Contains(g.Key))
                .Select(g => g.First())
                .ToList(),
            Fields = group.Members
                .SelectMany(m => m.Fields)
                .GroupBy(f => f.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Where(g => keptFields.Contains(g.Key))
                .Select(g => g.First())
                .ToList(),
        };

        Resolved<T> Resolve<T>(T value, Func<VaultItem, T> read)
        {
            var source = group.Members.FirstOrDefault(m => EqualityComparer<T>.Default.Equals(read(m), value));
            if (source is null) return Resolved<T>.Edited(value);
            return group.Members.All(m => EqualityComparer<T>.Default.Equals(read(m), value))
                ? Resolved<T>.Unanimous(value)
                : Resolved<T>.From(value, source.Id);
        }
    }

    [RelayCommand]
    private void SelectMember(VaultItem member) => SelectedMember = member;

    [RelayCommand]
    private void ChooseTarget(MergeTargetOption option)
    {
        if (option is { IsEnabled: true }) ChosenTarget = option;
    }

    /// <summary>Pull every differing value from the compared member in one go.</summary>
    [RelayCommand]
    private void TakeAllFromCompare()
    {
        foreach (var row in Rows.Where(r => r.DiffersFromCompare)) row.ResultValue = row.CompareValue;
    }

    [RelayCommand]
    private void Reset()
    {
        var draft = MergeDraft.Default(_row.Group);
        Rows[0].ResultValue = draft.Name.Value;
        Rows[1].ResultValue = draft.Username.Value;
        Rows[2].ResultValue = draft.Password.Value;
        Rows[3].ResultValue = draft.Totp.Value;
        Rows[4].ResultValue = draft.Notes.Value;
        foreach (var uri in Uris) uri.IsIncluded = true;
        foreach (var f in Fields) f.IsIncluded = true;
        ChosenTarget = Targets.FirstOrDefault(t => t.Target.ItemId == draft.Target.ItemId) ?? Targets[0];
    }

    [RelayCommand]
    private void Commit() => Committed?.Invoke(Build());

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}
