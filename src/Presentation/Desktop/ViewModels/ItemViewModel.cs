using BitwardenSharp.Domain.Vault;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitwardenSharp.Desktop.ViewModels;

/// <summary>One vault entry as the list and detail pane see it.</summary>
public sealed partial class ItemViewModel(VaultItem item) : ViewModelBase
{
    public VaultItem Item { get; } = item;

    public string Name => Item.Name;
    public string Username => Item.Login?.Username ?? string.Empty;
    public ItemType Type => Item.Type;
    public bool Favorite => Item.Favorite;
    public string? Notes => Item.Notes;
    public IReadOnlyList<LoginUri> Uris => Item.Uris;
    public IReadOnlyList<CustomField> Fields => Item.Fields;
    public IReadOnlyList<ItemAttachment> Attachments => Item.Attachments;
    public bool HasTotp => !string.IsNullOrWhiteSpace(Item.Login?.Totp);

    // Bound directly to IsVisible. Binding `Uris.Count` there instead would hand an int to a
    // bool property, which Avalonia reports as a binding error at runtime and silently leaves
    // the section in whatever state it started in.
    public bool HasUris => Item.Uris.Count > 0;
    public bool HasFields => Item.Fields.Count > 0;
    public bool HasAttachments => Item.Attachments.Count > 0;
    public bool HasNotes => !string.IsNullOrWhiteSpace(Item.Notes);
    public bool HasUsername => Username.Length > 0;
    public DateTimeOffset? Revised => Item.RevisionDate;

    public string TypeGlyph => Item.Type switch
    {
        ItemType.Login => "🔑",
        ItemType.SecureNote => "📝",
        ItemType.Card => "💳",
        ItemType.Identity => "🪪",
        ItemType.SshKey => "🖧",
        _ => "•",
    };

    /// <summary>Primary URI, for the list's secondary line.</summary>
    public string Subtitle => Username.Length > 0
        ? Username
        : Item.Uris.FirstOrDefault()?.Uri ?? string.Empty;

    /// <summary>
    /// Whether the password is currently on screen. Defaults to hidden and is deliberately not
    /// persisted anywhere — revealing is a per-view action, never a stored preference.
    /// </summary>
    [ObservableProperty] private bool _isPasswordVisible;

    public string PasswordDisplay => IsPasswordVisible
        ? Item.Login?.Password ?? string.Empty
        : new string('•', Math.Min(Item.Login?.Password?.Length ?? 0, 24));

    partial void OnIsPasswordVisibleChanged(bool value) => OnPropertyChanged(nameof(PasswordDisplay));

    [RelayCommand]
    private void TogglePassword() => IsPasswordVisible = !IsPasswordVisible;
}
