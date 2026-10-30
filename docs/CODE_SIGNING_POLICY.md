# Code signing policy

## Status

Windows Dynamic Island is applying to the SignPath Foundation open-source code-signing program. Until that application is approved and a release is signed, Windows builds are unsigned and may be blocked by Windows Smart App Control. A self-signed development certificate is never used for public releases.

Free code signing provided by SignPath.io, certificate by SignPath Foundation.

## What may be signed

Only Windows Dynamic Island release artifacts built from this public repository may be signed. The signed release consists of the Windows installer, its generated uninstaller, `DynamicIsland.exe`, `IslandSettings.exe`, and project-owned native binaries explicitly approved in the SignPath artifact configuration. The published product identity is **Windows Dynamic Island**; signed release artifacts must use the tag's release version and the metadata restrictions configured in SignPath.

Release artifacts must be built from an immutable Git tag in this repository by the approved GitHub Actions workflow. We do not sign arbitrary uploads, locally modified binaries, forks, or artifacts produced from an unreviewed commit. Third-party binaries retain their original signatures where available and are not represented as authored by this project. Signing credentials and SignPath API tokens are stored only in GitHub Actions secrets; no certificate private key is committed to this repository.

## Team roles and review

- Committer and reviewer: [@acheeseburgercodes](https://github.com/acheeseburgercodes)
- Signing approver: [@acheeseburgercodes](https://github.com/acheeseburgercodes)

Every person with commit, review, SignPath, or release approval access must use multi-factor authentication. Changes from anyone without commit access require pull-request review by a reviewer before merge. A signing request requires a manual approval by the listed approver after the tag, workflow run, artifact version, and release notes have been checked.

## Build and release controls

1. Release branches and tags are protected from direct, unreviewed edits. Repository protection and required reviews are configured in GitHub, not bypassed by a workflow.
2. The build uses the repository's `tools/Build-Installer.ps1` and `installer/WindowsDynamicIsland.iss` scripts.
3. The signed result must be verified with `Get-AuthenticodeSignature` and the SHA-256 checksum published alongside the installer.
4. No executable, DLL, installer, uninstaller, or script is modified after signing. The unsigned input and signed output are retained as CI artifacts for the signing request audit trail.
5. A release must be tested through installation, normal launch, Control Center launch, and uninstall before publication.

## Privacy

See the project's [privacy policy](PRIVACY.md). The policy is part of the signing scope because a signing identity vouches for the software users receive.

## Security reporting

Report a suspected compromised release, unexpected signer, or signing-policy violation through the repository's security contact or a private GitHub report. See [the security policy](../SECURITY.md). Do not run an artifact whose signer is not the expected SignPath Foundation signer once signing is active.
