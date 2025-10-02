# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

- CI: Force in-memory secret store during tests
  - Set `BITWARDEN_INMEMORY_SECRETS=1` in the `build-and-test` job of `.github/workflows/dotnet-test.yml` so GitHub Actions runners use the `InMemorySecretStore` and avoid OS keyring dependencies.
  - This prevents CI jobs from failing on runners that don't provide libsecret/keychain access.

---
