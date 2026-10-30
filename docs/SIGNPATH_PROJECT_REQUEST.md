# SignPath project configuration request

Use this checklist when submitting Windows Dynamic Island to SignPath
Foundation or when configuring the project after approval. It is deliberately
specific about what must come from SignPath so that this repository never
pretends that a self-signed certificate is a public-trust solution.

## Project facts

- Repository: `https://github.com/acheeseburgercodes/WindowsDynamicIsland`
- License: MIT
- Product name: `Windows Dynamic Island`
- Publisher before SignPath approval: `acheeseburgercodes`
- Release platform: Windows x64
- Build system: GitHub Actions on a GitHub-hosted Windows runner
- Installer: Inno Setup 6
- Maintainer, reviewer, and signing approver: `@acheeseburgercodes`

## Requested SignPath setup

1. Create the project and return the organization ID, project slug, release
   signing-policy slug, artifact-configuration slug, and the GitHub Actions
   connector settings.
2. Bind signing to this public repository and protected release tags only.
3. Require a manual approval by the signing approver for every production
   signing request.
4. Apply metadata restrictions for the product name **Windows Dynamic Island**
   and the version supplied by the release tag.
5. Configure coverage for the application executable, Control Center
   executable, project-owned native binaries, the installer, and the Inno
   Setup uninstaller.
6. Provide the supported SignTool/KSP integration required to sign the Inno
   Setup installer and generated uninstaller during compilation.

## Repository configuration after approval

Add `SIGNPATH_API_TOKEN` as a GitHub Actions secret and the project-specific
organization, project, policy, and artifact-configuration identifiers as
GitHub Actions variables. Keep the generated signing workflow restricted to
signed release tags; it must build from source, submit only its CI artifact,
wait for approval, verify the returned Authenticode signature and timestamp,
then publish the signed output and SHA-256 checksum.

See [the onboarding guide](SIGNPATH_ONBOARDING.md) and
[code-signing policy](CODE_SIGNING_POLICY.md) for the enforceable release
rules.
