Bitwarden CLI Helper
====================

This is a tiny wrapper CLI to run common Bitwarden CLI (`bw`) commands from a .NET executable.

Prerequisites
-------------

- Install the official Bitwarden CLI (`bw`) and ensure it's on your PATH. See: [Bitwarden CLI docs](https://developer.bitwarden.com/docs/cli/)
- .NET SDK (recommended: `net10.0`)

Usage
-----

Build and run from the project folder:

dotnet run --project src/Bitwarden.Cli -- login `email` `password`
dotnet run --project src/Bitwarden.Cli -- sync
dotnet run --project src/Bitwarden.Cli -- list [`items`|`organizations`|`folders`]

Configuration
-------------

This tool can store credentials locally for non-interactive login. Use the `config` command:

dotnet run --project src/Bitwarden.Cli -- config set email `you@example.com`
dotnet run --project src/Bitwarden.Cli -- config set password -    # '-' will prompt you to enter the password securely
dotnet run --project src/Bitwarden.Cli -- config get email
dotnet run --project src/Bitwarden.Cli -- config clear

Security notes


Security notes
--------------

- On Windows the CLI stores secrets encrypted with DPAPI (current user scope) in `%APPDATA%/bitwarden-cli-helper/config.json`.
- On macOS the CLI uses the system Keychain (`security` CLI) to store secrets.
- On Linux the CLI uses the libsecret keyring via the `secret-tool` command.
- The config JSON will not contain plaintext secrets on macOS/Linux; secrets are stored in the OS keyring and retrieved at runtime.



Notes
-----

- This project intentionally keeps dependencies minimal. See the repository-level copilot instructions for pinned NuGet package versions and licensing guidance.
- The wrapper assumes `bw` is installed. For non-interactive CI use, prefer calling `bw` directly or storing session keys securely.
