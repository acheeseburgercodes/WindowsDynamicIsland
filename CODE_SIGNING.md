# Code signing policy

## Status

SignPath Foundation approval and production signing have **not been verified**.
The repository is being prepared for application; this policy is not a claim
that existing downloads are Foundation-signed. Verify each release's actual
Authenticode signature. A SHA-256 checksum is not a publisher signature.

If approved and production signing is enabled, the attribution will be:
“Free code signing provided by [SignPath.io](https://signpath.io),
certificate by [SignPath Foundation](https://signpath.org).”

## People and review

| Role | Responsible maintainer |
| --- | --- |
| Author / committer | [acheeseburgercodes](https://github.com/acheeseburgercodes) |
| Reviewer of outside contributions | [acheeseburgercodes](https://github.com/acheeseburgercodes) |
| Proposed release signing approver | [acheeseburgercodes](https://github.com/acheeseburgercodes) |

These are project responsibilities, not a claim that SignPath accounts or roles
are configured. Everyone with repository write access or SignPath access must
enable MFA. Outside contributions require maintainer review, particularly
workflows, dependencies, installer scripts, and signing configurations.
A successful build alone never authorizes production signing. Each production
request needs an explicit approval by the configured SignPath approver.

## Scope and provenance

Only Windows Dynamic Island's own binaries and installer may be submitted.
Do not sign upstream Microsoft, React Native, Hermes, WebView2, or other
third-party DLLs with the project's Foundation certificate. Preserve upstream
signatures. Bundling a dependency does not make us its maintainer.

Build from a reviewed revision of this public repository on GitHub-hosted
Windows runners. Upload the output as a GitHub Actions artifact before
submitting its artifact ID through SignPath's GitHub integration. Never upload
a local binary as if it originated in CI. Restrict production policy to this
repository and reviewed release refs; do not grant signing credentials to pull
request jobs. Product name and version must match the artifact configuration.

The [application checklist](docs/SIGNPATH_APPLICATION.md) records onboarding
and release gates. The [release procedure](docs/RELEASING.md) covers validation,
signing order, and installer limitations.

## Privacy and security

Read the [privacy policy](PRIVACY.md), [security policy](SECURITY.md), and
[third-party notices](THIRD-PARTY-NOTICES.md). Do not disable Windows trust
checks or add certificates to users' trust stores as part of installation.

## References

- [Foundation conditions](https://signpath.org/terms.html)
- [GitHub build origin verification](https://docs.signpath.io/trusted-build-systems/github)
