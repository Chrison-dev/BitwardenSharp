# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Duplicate detection across five categories, with only same-site and same-brand/family groups
  treated as mergeable.
- Merge engine: survivor-first, verified before any deletion, soft deletes to trash.
- `bwsharp scan` and `bwsharp merge` on Spectre.Console; dry run by default.
- `bwsharp scan --from <file>` for read-only analysis of a saved `bw list items` dump.
- Architecture tests enforcing the hexagon and keeping process execution inside Infrastructure.

### Changed
- Rewritten from the pre-1.0 prototype, which returned process exit codes rather than data and had
  no domain model. That work is preserved on the `archive/v0-copilot` tag.

### Security
- Secrets are never passed as process arguments: the session key travels in the child environment
  and the item payload for `bw edit` is piped to stdin. The prototype passed the master password
  on the command line, where `ps` exposes it to every local user.
- Records holding credentials redact them in `ToString`.
