using Avalonia.Media;
using Avalonia.Media.Imaging;
using BitwardenSharp.Desktop.Services;
using BitwardenSharp.Domain.Vault;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitwardenSharp.Desktop.ViewModels;

/// <summary>One vault entry as the list and detail pane see it.</summary>
public sealed partial class ItemViewModel(VaultItem item) : ViewModelBase
{
    /// <summary>
    /// Placeholder tints. Muted enough to sit behind a letter without competing with real icons,
    /// and picked deterministically from the name so an item keeps the same colour between runs.
    /// </summary>
    private static readonly Color[] PlaceholderColours =
    [
        Color.FromRgb(0x4C, 0x6E, 0xF5), Color.FromRgb(0x7C, 0x4D, 0xC4),
        Color.FromRgb(0x0C, 0x8C, 0x8C), Color.FromRgb(0xC2, 0x6B, 0x2B),
        Color.FromRgb(0xB0, 0x3A, 0x6B), Color.FromRgb(0x3F, 0x7D, 0x3F),
        Color.FromRgb(0x8A, 0x6D, 0x1F), Color.FromRgb(0x5A, 0x5A, 0x7A),
    ];

    public VaultItem Item { get; } = item;

    public string Id => Item.Id;
    public string Name => Item.Name;
    public string Username => Item.Login?.Username ?? string.Empty;
    public ItemType Type => Item.Type;
    public bool Favorite => Item.Favorite;
    public string? Notes => Item.Notes;
    public IReadOnlyList<LoginUri> Uris => Item.Uris;
    public IReadOnlyList<CustomField> Fields => Item.Fields;
    public IReadOnlyList<ItemAttachment> Attachments => Item.Attachments;
    public IReadOnlyList<PasswordHistoryEntry> PasswordHistory => Item.PasswordHistory;
    public CardDetails? Card => Item.Card;
    public IdentityDetails? Identity => Item.Identity;
    public SshKeyDetails? SshKey => Item.SshKey;
    public bool HasTotp => !string.IsNullOrWhiteSpace(Item.Login?.Totp);
    public DateTimeOffset? Revised => Item.RevisionDate;
    public bool RequiresReprompt => Item.RequiresReprompt;

    public bool IsLogin => Item.Type == ItemType.Login;
    public bool HasCard => Item.Card is not null;
    public bool HasIdentity => Item.Identity is not null;
    public bool HasSshKey => Item.SshKey is not null;
    public bool HasUris => Item.Uris.Count > 0;
    public bool HasFields => Item.Fields.Count > 0;
    public bool HasAttachments => Item.Attachments.Count > 0;
    public bool HasNotes => !string.IsNullOrWhiteSpace(Item.Notes);
    public bool HasUsername => Username.Length > 0;
    public bool HasPasswordHistory => Item.PasswordHistory.Count > 0;

    public string TypeGlyph => Item.Type switch
    {
        ItemType.Login => "🔑",
        ItemType.SecureNote => "📝",
        ItemType.Card => "💳",
        ItemType.Identity => "🪪",
        ItemType.SshKey => "🖧",
        _ => "•",
    };

    /// <summary>Primary URI or username, for the list's secondary line.</summary>
    public string Subtitle => Username.Length > 0
        ? Username
        : Item.Uris.FirstOrDefault()?.Uri ?? string.Empty;

    // ── icon ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>The domain its icon comes from, or null when it has no web URI.</summary>
    public string? IconDomain => IconLoader.IconDomainFor(Item);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    private Bitmap? _icon;

    /// <summary>True until a real icon arrives — and forever, for anything without one.</summary>
    public bool ShowPlaceholder => Icon is null;

    /// <summary>The letter drawn in the placeholder.</summary>
    public string Initial => Name.TrimStart() is { Length: > 0 } trimmed
        ? trimmed[..1].ToUpperInvariant()
        : "?";

    /// <summary>
    /// Deterministic tint from the name, so the same item is the same colour every launch and the
    /// list stays visually stable rather than reshuffling colours on each load.
    /// </summary>
    public IBrush PlaceholderBrush
    {
        get
        {
            var hash = 0;
            foreach (var c in Name) hash = unchecked(hash * 31 + char.ToLowerInvariant(c));
            return new SolidColorBrush(PlaceholderColours[Math.Abs(hash) % PlaceholderColours.Length]);
        }
    }

    public async Task LoadIconAsync(IconLoader loader, CancellationToken cancellationToken = default)
    {
        if (IconDomain is null || !loader.IsEnabled) return;
        Icon = await loader.GetAsync(IconDomain, cancellationToken);
    }

    // ── reveal ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether secrets on this item are currently on screen. Defaults to hidden, applies to the
    /// password, the card number and CVV and the SSH private key alike, and is deliberately not
    /// persisted — revealing is a per-view action, never a stored preference.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordDisplay), nameof(CardNumberDisplay),
        nameof(CardCodeDisplay), nameof(SshPrivateKeyDisplay))]
    private bool _isSecretVisible;

    private static string Mask(string? value, int cap = 24) =>
        new('•', Math.Min(value?.Length ?? 0, cap));

    public string PasswordDisplay =>
        IsSecretVisible ? Item.Login?.Password ?? string.Empty : Mask(Item.Login?.Password);

    public string CardNumberDisplay => IsSecretVisible
        ? Item.Card?.Number ?? string.Empty
        : Item.Card?.LastFour is { } last ? $"•••• •••• •••• {last}" : string.Empty;

    public string CardCodeDisplay =>
        IsSecretVisible ? Item.Card?.Code ?? string.Empty : Mask(Item.Card?.Code, 4);

    public string SshPrivateKeyDisplay =>
        IsSecretVisible ? Item.SshKey?.PrivateKey ?? string.Empty : "•••• private key hidden ••••";

    [RelayCommand]
    private void ToggleSecrets() => IsSecretVisible = !IsSecretVisible;
}
