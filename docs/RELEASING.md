# Release procedure

## Unsigned build evidence

Use a clean checkout on Windows 11 with .NET 10 SDK, Node 22, Visual Studio
2022 C++/.NET desktop workloads and Windows SDK, and Inno Setup 6.7.1.

    cd IslandSettings
    npm ci
    cd ..
    ./tools/Build-Installer.ps1 -Version 0.3.0

Use the intended release version, keeping the project version in sync.
The script generates matching Control Center version metadata, application
binaries, dependency notices/inventory, build.json, Setup and SHA-256.

Use -StageOnly to stop before packaging. After signing that exact stage,
-PackageOnly with the same version packages it without recompiling.
Metadata checks reject a missing/mismatched stage. Foundation requests must
use GitHub-hosted artifacts, not local files.

CI publishes unsigned candidate artifacts, not GitHub releases. Traceability
is provided, but byte-for-byte reproducibility is not claimed: hosted runner
images and toolchain patch versions can change.

## Foundation signing order

1. Review source, dependencies, build scripts and version.
2. Build on GitHub-hosted runners and upload installer-stage.
3. Sign its artifact ID using applications.xml, restricted to
   app/DynamicIsland.exe and control-center/IslandSettings.exe.
4. Verify returned signatures and metadata. Preserve upstream DLL signatures.
5. Generate and sign Inno Setup's **uninstaller stub** in the same hosted
   pipeline. Use SignedUninstaller=yes and SignedUninstallerDir: an initial
   compile creates the uniquely named stub; a subsequent compile embeds its
   signature. Agree on stub ownership, metadata and artifact configuration
   with SignPath. Never use an unrestricted *.exe or *.dll signing rule.
6. Package those signed binaries without recompiling the applications.
7. Upload Setup separately and sign it with installer.xml.
8. Verify signatures and calculate final checksums after signing.
9. Test install, upgrade, launch, default-off weather, weather opt-in,
   Control Center and uninstall on clean Windows 11.
10. Publish reviewed artifacts with source commit, build-run link, signing
    status, checksums, license, privacy policy and Code signing policy.

Remote uninstaller signing remains an onboarding gate, not an implemented
feature of the unsigned-candidate workflow. Signing only the outer Setup
does not sign nested executables or its temporary setup/uninstall copies.

-CertificateThumbprint supports local signing with your own certificate and
Inno's uninstaller callback. It is not Foundation signing. Self-signed
certificates do not establish public trust.

## Release note template

    Source: <reviewed commit>
    Build: <successful GitHub Actions run>
    Windows signing: <unsigned / test-signed / verified Foundation-signed>
    Code signing policy: https://github.com/acheeseburgercodes/WindowsDynamicIsland/blob/main/CODE_SIGNING.md
    Privacy policy: https://github.com/acheeseburgercodes/WindowsDynamicIsland/blob/main/PRIVACY.md

Describe functionality, requirements and uninstall instructions on download
pages. Never present test-signed builds as publicly trusted releases.

Reference: https://jrsoftware.org/ishelp/topic_setup_signeduninstaller.htm
