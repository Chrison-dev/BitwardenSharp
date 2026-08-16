using BitwardenSharp.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace BitwardenSharp.Desktop.ViewModels;

/// <summary>Owns the top-level swap between the unlock screen and the vault browser.</summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly IVaultSession _session;

    [ObservableProperty]
    private ViewModelBase _current;

    public MainWindowViewModel(IServiceProvider services, IVaultSession session)
    {
        _services = services;
        _session = session;

        var unlock = services.GetRequiredService<UnlockViewModel>();
        unlock.Unlocked += OnUnlocked;
        _current = unlock;
    }

    private void OnUnlocked()
    {
        var vault = _services.GetRequiredService<VaultViewModel>();
        vault.Locked += OnLocked;
        Current = vault;
        _ = vault.LoadAsync();
    }

    private void OnLocked()
    {
        var unlock = _services.GetRequiredService<UnlockViewModel>();
        unlock.Unlocked += OnUnlocked;
        Current = unlock;
    }

    /// <summary>Locks the vault on the way out rather than leaving a live session behind.</summary>
    public void OnShutdown()
    {
        try { _session.LockAsync().GetAwaiter().GetResult(); }
        catch { /* shutting down anyway; a failure to lock must not block exit */ }
    }
}
