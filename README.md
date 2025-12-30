# Bitwarden

Tools around bitwarden

## Coding Style

I have limited time to develop hobby projects on the side, so most of this will be me playing around with AI and doing the so called Vibe Coding pattern.
If anything useful comes out of it, fine. If not then cest la vive :-)

## CLI (`Bitwarden.Cli`)

This repository contains a small CLI wrapper around the official `bw` (Bitwarden CLI) executable located in `src/Bitwarden.Cli`.

Key points:

- The CLI uses Generic Host/DI (`HostBuilder`) to register services.

- `ISecretStore` is registered per-platform:
	- Windows: `DpapiSecretStore` (ProtectedData + file storage under AppData).
	- macOS/Linux: `OsKeyringSecretStore` (delegates to OS keyring via `security` / `secret-tool`).
	- Tests and CI use `InMemorySecretStore` (tests set `Config.SecretStore` in setup).

Running tests locally:

```powershell
dotnet test tests/Bitwarden.Cli.Tests/Bitwarden.Cli.Tests.csproj
```

If you'd like to force a specific secret store for debugging you can set `Config.SecretStore` early in your application before interacting with `Config`.
