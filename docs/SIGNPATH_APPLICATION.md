# SignPath Foundation application preparation

Target: https://github.com/acheeseburgercodes/WindowsDynamicIsland
Project: Windows Dynamic Island
License: MIT
Maintainer: https://github.com/acheeseburgercodes
Downloads: https://github.com/acheeseburgercodes/WindowsDynamicIsland/releases

## Project description for the application

Windows Dynamic Island is a Windows 11 desktop utility written in C#/.NET/WPF
with a React Native Windows Control Center. It provides media controls, a
focus timer, local task checklists and notes, file and clipboard tools,
system metrics, optional weather, and user-selected app/window hosting.
It has no advertising, account requirement, or project-operated telemetry.
Installation and data handling are documented in PRIVACY.md.

The project requests Windows Authenticode signing for its own application,
Control Center, and installer built from the public repository. Third-party
libraries retain their upstream identity and are not submitted for re-signing.

## Repository evidence

- MIT LICENSE and THIRD-PARTY-NOTICES.md.
- CODE_SIGNING.md: named roles, review/approval rules, honest signing status.
- PRIVACY.md: network endpoints, location, loopback IPC, data storage and removal.
- Installer privacy disclosure and weather networking opt-in.
- .github/workflows/windows-candidate.yml: GitHub-hosted unsigned installer
  build, source revision, product/version checks, npm lockfile restore,
  dependency inventory and notices.
- signpath/applications.xml and installer.xml: exact own-binary allowlists
  and product/version restrictions; validate these with SignPath.
- SECURITY.md, CONTRIBUTING.md and CODEOWNERS.

## Owner checklist before submitting

Committing files does not complete these account, policy or release tasks.

- [ ] Merge the reviewed readiness PR into public main.
- [ ] Obtain a successful Windows candidate workflow run; record its URL.
- [ ] Test the generated installer, offline/default weather, explicit opt-in,
      Control Center, upgrade and uninstall on Windows 11.
- [ ] Publish the tested CI artifact as a new release with honest signing status
      and a **Code signing policy** link. Do not silently replace existing assets.
- [ ] Confirm GitHub and SignPath MFA for every maintainer.
- [ ] Enable private vulnerability reporting and branch/tag protection.
      Require review of outside contributions. A sole maintainer must not
      claim independent two-person review.
- [ ] Review generated dependency inventory and notices; resolve REVIEW REQUIRED
      entries and verify redistribution rights. Ask SignPath about Windows
      system-library exceptions, especially WebView2, Visual C++ runtime and
      Windows App SDK binary packages; do not relabel them all as MIT.
- [ ] Review the current dependency-audit artifact and the remaining advisories
      in [DEPENDENCY_REVIEW.md](DEPENDENCY_REVIEW.md) before production signing.
- [ ] Confirm artwork/icon ownership and upstream-derived code licensing.
- [ ] Supply actual contact details and accept the form's terms personally.
- [ ] Supply truthful adoption/reputation evidence. Do not invent users,
      download counts, contributors or endorsements.

## After acceptance

Install the SignPath GitHub App for this repository and link its trusted GitHub
build system. Configure the organization, project and artifact slugs. Require
manual approval for every production request. Store a submitter-only token in
a protected GitHub environment, never in PR jobs.

Validate the artifact XML against uploaded samples, starting with a test
certificate. See signpath/README.md. Complete the signed-uninstaller integration
in RELEASING.md before declaring production installers ready. Only after
acceptance and verified signing should CODE_SIGNING.md claim active support.

Approval is discretionary, including the Foundation's reputation assessment.
This preparation cannot guarantee acceptance.

References: [terms](https://signpath.org/terms.html),
[application](https://signpath.org/apply.html),
[GitHub integration](https://docs.signpath.io/trusted-build-systems/github).
