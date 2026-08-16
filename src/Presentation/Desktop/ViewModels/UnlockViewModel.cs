using BitwardenSharp.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitwardenSharp.Desktop.ViewModels;

/// <summary>The unlock screen.</summary>
public sealed partial class UnlockViewModel(IVaultSession session) : ViewModelBase
{
    public event Action? Unlocked;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private string _masterPassword = string.Empty;

    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _accountEmail;
    [ObservableProperty] private string? _serverUrl;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isReady;

    /// <summary>Reads who we would be unlocking as, so the screen can name the account.</summary>
    public async Task InitialiseAsync()
    {
        try
        {
            var status = await session.GetStatusAsync();
            AccountEmail = status.UserEmail;
            ServerUrl = status.ServerUrl;

            if (status.IsUnlocked)
            {
                // A session already exists — inherited from BW_SESSION, or left by an earlier run.
                Unlocked?.Invoke();
                return;
            }

            IsReady = string.Equals(status.Status, "locked", StringComparison.OrdinalIgnoreCase);
            if (!IsReady)
                Error = $"The bw client reports '{status.Status}'. Run `bw login` first.";
        }
        catch (Exception ex)
        {
            Error = $"Could not reach the bw client: {ex.Message}";
        }
    }

    private bool CanUnlock => !IsBusy && MasterPassword.Length > 0;

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            var result = await session.UnlockAsync(MasterPassword);

            // Drop the password either way — it is not needed again, and a failed attempt has no
            // reason to leave it sitting in a bound property.
            MasterPassword = string.Empty;

            if (result.Succeeded) Unlocked?.Invoke();
            else Error = result.Error;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
