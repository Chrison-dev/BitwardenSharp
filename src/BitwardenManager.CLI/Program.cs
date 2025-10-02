using BitwardenManager.CliWrapper;
using BitwardenManager.Core.Interfaces;
using BitwardenManager.Core.Models;

namespace BitwardenManager.CLI;

class Program
{
    private static IBitwardenService? _bitwardenService;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Bitwarden Manager CLI");
        Console.WriteLine("====================");

        _bitwardenService = new BitwardenCliService();

        if (args.Length == 0)
        {
            await ShowInteractiveMenu();
        }
        else
        {
            await ProcessCommand(args);
        }
    }

    static async Task ShowInteractiveMenu()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine("1. status    - Check authentication status");
            Console.WriteLine("2. login     - Login to Bitwarden");
            Console.WriteLine("3. unlock    - Unlock vault");
            Console.WriteLine("4. logout    - Logout from Bitwarden");
            Console.WriteLine("5. list      - List vault items");
            Console.WriteLine("6. folders   - List folders");
            Console.WriteLine("7. search    - Search vault items");
            Console.WriteLine("8. exit      - Exit application");
            Console.WriteLine();
            Console.Write("Enter command number or name: ");

            var input = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrEmpty(input))
                continue;

            switch (input)
            {
                case "1":
                case "status":
                    await CheckStatus();
                    break;
                case "2":
                case "login":
                    await Login();
                    break;
                case "3":
                case "unlock":
                    await Unlock();
                    break;
                case "4":
                case "logout":
                    await Logout();
                    break;
                case "5":
                case "list":
                    await ListItems();
                    break;
                case "6":
                case "folders":
                    await ListFolders();
                    break;
                case "7":
                case "search":
                    await SearchItems();
                    break;
                case "8":
                case "exit":
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Unknown command. Please try again.");
                    break;
            }
        }
    }

    static async Task ProcessCommand(string[] args)
    {
        var command = args[0].ToLower();
        
        switch (command)
        {
            case "status":
                await CheckStatus();
                break;
            case "login":
                if (args.Length >= 3)
                {
                    await Login(args[1], args[2], args.Length > 3 ? args[3] : null);
                }
                else
                {
                    Console.WriteLine("Usage: login <email> <password> [two-factor-code]");
                }
                break;
            case "unlock":
                if (args.Length >= 2)
                {
                    await Unlock(args[1]);
                }
                else
                {
                    Console.WriteLine("Usage: unlock <master-password>");
                }
                break;
            case "logout":
                await Logout();
                break;
            case "list":
                await ListItems();
                break;
            case "folders":
                await ListFolders();
                break;
            case "search":
                if (args.Length >= 2)
                {
                    await SearchItems(args[1]);
                }
                else
                {
                    Console.WriteLine("Usage: search <query>");
                }
                break;
            default:
                Console.WriteLine($"Unknown command: {command}");
                break;
        }
    }

    static async Task CheckStatus()
    {
        try
        {
            Console.WriteLine("Checking authentication status...");
            var isAuthenticated = await _bitwardenService!.IsAuthenticatedAsync();
            Console.WriteLine($"Authentication status: {(isAuthenticated ? "Authenticated" : "Not authenticated")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking status: {ex.Message}");
        }
    }

    static async Task Login(string? email = null, string? password = null, string? twoFactorCode = null)
    {
        try
        {
            if (string.IsNullOrEmpty(email))
            {
                Console.Write("Email: ");
                email = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(password))
            {
                Console.Write("Password: ");
                password = ReadPassword();
            }

            if (string.IsNullOrEmpty(twoFactorCode))
            {
                Console.Write("Two-factor code (optional, press Enter to skip): ");
                twoFactorCode = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(twoFactorCode))
                    twoFactorCode = null;
            }

            Console.WriteLine("Logging in...");
            var success = await _bitwardenService!.AuthenticateAsync(email!, password!, twoFactorCode);
            Console.WriteLine(success ? "Login successful!" : "Login failed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during login: {ex.Message}");
        }
    }

    static async Task Unlock(string? masterPassword = null)
    {
        try
        {
            if (string.IsNullOrEmpty(masterPassword))
            {
                Console.Write("Master password: ");
                masterPassword = ReadPassword();
            }

            Console.WriteLine("Unlocking vault...");
            var success = await _bitwardenService!.UnlockAsync(masterPassword!);
            Console.WriteLine(success ? "Vault unlocked!" : "Failed to unlock vault!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during unlock: {ex.Message}");
        }
    }

    static async Task Logout()
    {
        try
        {
            Console.WriteLine("Logging out...");
            var success = await _bitwardenService!.LogoutAsync();
            Console.WriteLine(success ? "Logout successful!" : "Logout failed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during logout: {ex.Message}");
        }
    }

    static async Task ListItems()
    {
        try
        {
            Console.WriteLine("Retrieving vault items...");
            var items = await _bitwardenService!.GetVaultItemsAsync();
            
            if (!items.Any())
            {
                Console.WriteLine("No items found.");
                return;
            }

            Console.WriteLine($"\nFound {items.Count()} items:");
            Console.WriteLine("".PadRight(80, '-'));
            
            foreach (var item in items)
            {
                Console.WriteLine($"ID: {item.Id}");
                Console.WriteLine($"Name: {item.Name}");
                Console.WriteLine($"Type: {item.Type}");
                if (!string.IsNullOrEmpty(item.Notes))
                    Console.WriteLine($"Notes: {item.Notes[..Math.Min(50, item.Notes.Length)]}...");
                Console.WriteLine("".PadRight(80, '-'));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving items: {ex.Message}");
        }
    }

    static async Task ListFolders()
    {
        try
        {
            Console.WriteLine("Retrieving folders...");
            var folders = await _bitwardenService!.GetFoldersAsync();
            
            if (!folders.Any())
            {
                Console.WriteLine("No folders found.");
                return;
            }

            Console.WriteLine($"\nFound {folders.Count()} folders:");
            Console.WriteLine("".PadRight(50, '-'));
            
            foreach (var folder in folders)
            {
                Console.WriteLine($"ID: {folder.Id}");
                Console.WriteLine($"Name: {folder.Name}");
                Console.WriteLine("".PadRight(50, '-'));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving folders: {ex.Message}");
        }
    }

    static async Task SearchItems(string? query = null)
    {
        try
        {
            if (string.IsNullOrEmpty(query))
            {
                Console.Write("Enter search query: ");
                query = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(query))
            {
                Console.WriteLine("Search query cannot be empty.");
                return;
            }

            Console.WriteLine($"Searching for: {query}");
            var items = await _bitwardenService!.SearchVaultItemsAsync(query);
            
            if (!items.Any())
            {
                Console.WriteLine("No items found matching the search query.");
                return;
            }

            Console.WriteLine($"\nFound {items.Count()} matching items:");
            Console.WriteLine("".PadRight(80, '-'));
            
            foreach (var item in items)
            {
                Console.WriteLine($"ID: {item.Id}");
                Console.WriteLine($"Name: {item.Name}");
                Console.WriteLine($"Type: {item.Type}");
                Console.WriteLine("".PadRight(80, '-'));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during search: {ex.Message}");
        }
    }

    static string ReadPassword()
    {
        var password = "";
        ConsoleKeyInfo keyInfo;
        
        do
        {
            keyInfo = Console.ReadKey(true);
            
            if (keyInfo.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[..^1];
                Console.Write("\b \b");
            }
            else if (keyInfo.Key != ConsoleKey.Enter && keyInfo.Key != ConsoleKey.Backspace)
            {
                password += keyInfo.KeyChar;
                Console.Write("*");
            }
        } while (keyInfo.Key != ConsoleKey.Enter);
        
        Console.WriteLine();
        return password;
    }
}
