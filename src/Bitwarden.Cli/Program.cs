using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;
using Bitwarden.Application;
using Bitwarden.Infrastructure;

namespace Bitwarden.Cli
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            using var host = Host.CreateDefaultBuilder()
                .ConfigureBitwardenHost()
                .Build();

            // Resolve services
            var config = host.Services.GetRequiredService<Config>();
            var bwRunner = host.Services.GetRequiredService<Bitwarden.Application.IBwRunner>();

            if (args.Length == 0)
            {
                PrintUsage();
                return (int)ProcessExitCodes.InvalidCommand;
            }

            var cmd = args[0].ToLowerInvariant();

            try
            {
                if (cmd == "config")
                    return HandleConfig(args.Skip(1).ToArray(), config);

                if (!bwRunner.IsBwAvailable())
                {
                    Console.Error.WriteLine("Error: 'bw' (Bitwarden CLI) not found in PATH. Please install it and ensure it's on PATH.");
                    return (int)ProcessExitCodes.Failure;
                }

                return cmd switch
                {
                    "login" => await bwRunner.LoginAsync(args.Skip(1).ToArray(), config),
                    "sync" => await bwRunner.SyncAsync(args.Skip(1).ToArray()),
                    "list" => await bwRunner.ListAsync(args.Skip(1).ToArray()),
                    "status" => await bwRunner.StatusAsync(args.Skip(1).ToArray()),
                    "logout" => await bwRunner.LogoutAsync(args.Skip(1).ToArray()),
                    "items-get" => await bwRunner.ItemsGetAsync(args.Skip(1).ToArray()),
                    _ => (int)ProcessExitCodes.InvalidCommand,
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return (int)ProcessExitCodes.Failure;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: bitwarden-cli <command> [options]");
            Console.WriteLine("Commands: login, sync, list, status, logout, items-get, config");
            Console.WriteLine("Config: set <key> <value> | get <key> | clear");
        }

        private static int HandleConfig(string[] args, Config config)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("config set <key> <value> | config get <key> | config clear");
                return (int)ProcessExitCodes.InvalidCommand;
            }

            var sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "set":
                    return HandleConfigSet(config, args);
                case "get":
                    return HandleConfigGet(config, args);
                case "clear":
                    config.Clear();
                    Console.WriteLine("Config cleared.");
                    return (int)ProcessExitCodes.Success;
                default:
                    Console.WriteLine("Unknown config subcommand");
                    return (int)ProcessExitCodes.InvalidCommand;
            }
        }

        private static int HandleConfigSet(Config config, string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: config set <key> <value>\nKeys: email, password, clientid, clientsecret");
                return (int)ProcessExitCodes.InvalidCommand;
            }

            var key = args[1].ToLowerInvariant();
            var value = string.Join(' ', args.Skip(2));

            switch (key)
            {
                case "email":
                    config.Email = value;
                    config.Save();
                    Console.WriteLine("Email set.");
                    return (int)ProcessExitCodes.Success;
                case "password":
                    if (value == "-")
                    {
                        Console.Write("Password: ");
                        var pw = ReadPassword();
                        config.SetPassword(pw);
                    }
                    else
                    {
                        config.SetPassword(value);
                    }
                    config.Save();
                    Console.WriteLine("Password set.");
                    return (int)ProcessExitCodes.Success;
                case "clientid":
                    config.ClientId = value;
                    config.Save();
                    Console.WriteLine("ClientId set.");
                    return (int)ProcessExitCodes.Success;
                case "clientsecret":
                    if (value == "-")
                    {
                        Console.Write("Client Secret: ");
                        var sec = ReadPassword();
                        config.EncryptedClientSecret = Config.Protect(sec);
                    }
                    else
                    {
                        config.EncryptedClientSecret = Config.Protect(value);
                    }
                    config.Save();
                    Console.WriteLine("ClientSecret set.");
                    return (int)ProcessExitCodes.Success;
                default:
                    Console.WriteLine("Unknown config key");
                    return (int)ProcessExitCodes.InvalidCommand;
            }
        }

        private static int HandleConfigGet(Config config, string[] args)
        {
            if (args.Length < 2) return (int)ProcessExitCodes.InvalidCommand;
            var key = args[1].ToLowerInvariant();
            switch (key)
            {
                case "email":
                    Console.WriteLine(config.Email ?? "");
                    return (int)ProcessExitCodes.Success;
                case "password":
                    Console.WriteLine(config.GetPassword() ?? "");
                    return (int)ProcessExitCodes.Success;
                case "clientid":
                    Console.WriteLine(config.ClientId ?? "");
                    return (int)ProcessExitCodes.Success;
                case "clientsecret":
                    try
                    {
                        Console.WriteLine(config.EncryptedClientSecret != null ? Config.Unprotect(config.EncryptedClientSecret) : "");
                    }
                    catch
                    {
                        Console.WriteLine("");
                    }
                    return (int)ProcessExitCodes.Success;
                default:
                    Console.WriteLine("Unknown config key");
                    return (int)ProcessExitCodes.InvalidCommand;
            }
        }

        private static string ReadPassword()
        {
            var sb = new StringBuilder();
            ConsoleKeyInfo key;
            while (true)
            {
                key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
                {
                    sb.Length--;
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    sb.Append(key.KeyChar);
                    Console.Write('*');
                }
            }
            Console.WriteLine();
            return sb.ToString();
        }

        private enum ProcessExitCodes : int
        {
            Success = 0,
            Failure = 2,
            InvalidCommand = 3
        }
    }
}
