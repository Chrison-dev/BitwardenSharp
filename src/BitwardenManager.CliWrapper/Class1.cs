using System.Diagnostics;
using System.Text.Json;
using BitwardenManager.Core.Interfaces;
using BitwardenManager.Core.Models;

namespace BitwardenManager.CliWrapper;

public class BitwardenCliService : IBitwardenService
{
    private readonly string _cliPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public BitwardenCliService(string? cliPath = null)
    {
        _cliPath = cliPath ?? "bw"; // Default to 'bw' in PATH
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var result = await ExecuteCliCommandAsync("status");
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
            {
                var status = JsonSerializer.Deserialize<BitwardenStatus>(result.Output, _jsonOptions);
                return status?.IsAuthenticated == true;
            }
        }
        catch (Exception)
        {
            // CLI not available or other error
        }
        
        return false;
    }

    public async Task<bool> AuthenticateAsync(string email, string password, string? twoFactorCode = null)
    {
        try
        {
            var args = $"login \"{email}\" \"{password}\"";
            if (!string.IsNullOrEmpty(twoFactorCode))
            {
                args += $" --code \"{twoFactorCode}\"";
            }
            
            var result = await ExecuteCliCommandAsync(args);
            return result.IsSuccess;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> UnlockAsync(string masterPassword)
    {
        try
        {
            var args = $"unlock \"{masterPassword}\"";
            var result = await ExecuteCliCommandAsync(args);
            return result.IsSuccess;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> LogoutAsync()
    {
        try
        {
            var result = await ExecuteCliCommandAsync("logout");
            return result.IsSuccess;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<IEnumerable<VaultItem>> GetVaultItemsAsync()
    {
        try
        {
            var result = await ExecuteCliCommandAsync("list items");
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
            {
                var items = JsonSerializer.Deserialize<VaultItem[]>(result.Output, _jsonOptions);
                return items ?? Array.Empty<VaultItem>();
            }
        }
        catch (Exception)
        {
            // Handle parsing errors
        }
        
        return Array.Empty<VaultItem>();
    }

    public async Task<VaultItem?> GetVaultItemAsync(string id)
    {
        try
        {
            var result = await ExecuteCliCommandAsync($"get item \"{id}\"");
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
            {
                return JsonSerializer.Deserialize<VaultItem>(result.Output, _jsonOptions);
            }
        }
        catch (Exception)
        {
            // Handle parsing errors
        }
        
        return null;
    }

    public async Task<VaultItem> CreateVaultItemAsync(VaultItem item)
    {
        try
        {
            var itemJson = JsonSerializer.Serialize(item, _jsonOptions);
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, itemJson);
            
            try
            {
                var result = await ExecuteCliCommandAsync($"create item \"{tempFile}\"");
                if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
                {
                    return JsonSerializer.Deserialize<VaultItem>(result.Output, _jsonOptions) ?? item;
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        catch (Exception)
        {
            // Handle errors
        }
        
        return item;
    }

    public async Task<VaultItem> UpdateVaultItemAsync(VaultItem item)
    {
        try
        {
            var itemJson = JsonSerializer.Serialize(item, _jsonOptions);
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, itemJson);
            
            try
            {
                var result = await ExecuteCliCommandAsync($"edit item \"{item.Id}\" \"{tempFile}\"");
                if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
                {
                    return JsonSerializer.Deserialize<VaultItem>(result.Output, _jsonOptions) ?? item;
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        catch (Exception)
        {
            // Handle errors
        }
        
        return item;
    }

    public async Task<bool> DeleteVaultItemAsync(string id)
    {
        try
        {
            var result = await ExecuteCliCommandAsync($"delete item \"{id}\"");
            return result.IsSuccess;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<IEnumerable<Folder>> GetFoldersAsync()
    {
        try
        {
            var result = await ExecuteCliCommandAsync("list folders");
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
            {
                var folders = JsonSerializer.Deserialize<Folder[]>(result.Output, _jsonOptions);
                return folders ?? Array.Empty<Folder>();
            }
        }
        catch (Exception)
        {
            // Handle parsing errors
        }
        
        return Array.Empty<Folder>();
    }

    public async Task<Folder?> GetFolderAsync(string id)
    {
        try
        {
            var result = await ExecuteCliCommandAsync($"get folder \"{id}\"");
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
            {
                return JsonSerializer.Deserialize<Folder>(result.Output, _jsonOptions);
            }
        }
        catch (Exception)
        {
            // Handle parsing errors
        }
        
        return null;
    }

    public async Task<Folder> CreateFolderAsync(Folder folder)
    {
        try
        {
            var folderJson = JsonSerializer.Serialize(folder, _jsonOptions);
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, folderJson);
            
            try
            {
                var result = await ExecuteCliCommandAsync($"create folder \"{tempFile}\"");
                if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
                {
                    return JsonSerializer.Deserialize<Folder>(result.Output, _jsonOptions) ?? folder;
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        catch (Exception)
        {
            // Handle errors
        }
        
        return folder;
    }

    public async Task<Folder> UpdateFolderAsync(Folder folder)
    {
        try
        {
            var folderJson = JsonSerializer.Serialize(folder, _jsonOptions);
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, folderJson);
            
            try
            {
                var result = await ExecuteCliCommandAsync($"edit folder \"{folder.Id}\" \"{tempFile}\"");
                if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
                {
                    return JsonSerializer.Deserialize<Folder>(result.Output, _jsonOptions) ?? folder;
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        catch (Exception)
        {
            // Handle errors
        }
        
        return folder;
    }

    public async Task<bool> DeleteFolderAsync(string id)
    {
        try
        {
            var result = await ExecuteCliCommandAsync($"delete folder \"{id}\"");
            return result.IsSuccess;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<IEnumerable<Collection>> GetCollectionsAsync()
    {
        try
        {
            var result = await ExecuteCliCommandAsync("list collections");
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
            {
                var collections = JsonSerializer.Deserialize<Collection[]>(result.Output, _jsonOptions);
                return collections ?? Array.Empty<Collection>();
            }
        }
        catch (Exception)
        {
            // Handle parsing errors
        }
        
        return Array.Empty<Collection>();
    }

    public async Task<Collection?> GetCollectionAsync(string id)
    {
        try
        {
            var result = await ExecuteCliCommandAsync($"get collection \"{id}\"");
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
            {
                return JsonSerializer.Deserialize<Collection>(result.Output, _jsonOptions);
            }
        }
        catch (Exception)
        {
            // Handle parsing errors
        }
        
        return null;
    }

    public async Task<IEnumerable<VaultItem>> SearchVaultItemsAsync(string query)
    {
        try
        {
            var result = await ExecuteCliCommandAsync($"list items --search \"{query}\"");
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
            {
                var items = JsonSerializer.Deserialize<VaultItem[]>(result.Output, _jsonOptions);
                return items ?? Array.Empty<VaultItem>();
            }
        }
        catch (Exception)
        {
            // Handle parsing errors
        }
        
        return Array.Empty<VaultItem>();
    }

    private async Task<CliResult> ExecuteCliCommandAsync(string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _cliPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            return new CliResult
            {
                IsSuccess = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new CliResult
            {
                IsSuccess = false,
                Error = ex.Message,
                ExitCode = -1
            };
        }
    }

    private record CliResult
    {
        public bool IsSuccess { get; init; }
        public string? Output { get; init; }
        public string? Error { get; init; }
        public int ExitCode { get; init; }
    }
}
