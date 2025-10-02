using BitwardenManager.Core.Models;

namespace BitwardenManager.Core.Interfaces;

public interface IBitwardenService
{
    Task<bool> IsAuthenticatedAsync();
    Task<bool> AuthenticateAsync(string email, string password, string? twoFactorCode = null);
    Task<bool> UnlockAsync(string masterPassword);
    Task<bool> LogoutAsync();
    
    // Vault Items
    Task<IEnumerable<VaultItem>> GetVaultItemsAsync();
    Task<VaultItem?> GetVaultItemAsync(string id);
    Task<VaultItem> CreateVaultItemAsync(VaultItem item);
    Task<VaultItem> UpdateVaultItemAsync(VaultItem item);
    Task<bool> DeleteVaultItemAsync(string id);
    
    // Folders
    Task<IEnumerable<Folder>> GetFoldersAsync();
    Task<Folder?> GetFolderAsync(string id);
    Task<Folder> CreateFolderAsync(Folder folder);
    Task<Folder> UpdateFolderAsync(Folder folder);
    Task<bool> DeleteFolderAsync(string id);
    
    // Collections
    Task<IEnumerable<Collection>> GetCollectionsAsync();
    Task<Collection?> GetCollectionAsync(string id);
    
    // Search
    Task<IEnumerable<VaultItem>> SearchVaultItemsAsync(string query);
}
