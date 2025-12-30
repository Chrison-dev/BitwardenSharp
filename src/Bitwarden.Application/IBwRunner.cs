namespace Bitwarden.Application
{
    public interface IBwRunner
    {
        bool IsBwAvailable();
        Task<int> LoginAsync(string[] args, Config? config = null);
        Task<int> SyncAsync(string[] args);
        Task<int> ListAsync(string[] args);
        Task<int> StatusAsync(string[] args);
        Task<int> LogoutAsync(string[] args);
        Task<int> ItemsGetAsync(string[] args);
    }
}
