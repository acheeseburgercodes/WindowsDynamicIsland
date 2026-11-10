# SignPath configuration candidates

These are onboarding inputs, not an activated signing service. Validate them
in SignPath against an actual GitHub Actions artifact sample.

applications.xml expects installer-stage contents at the ZIP root and signs
exactly two project-owned executables. installer.xml expects Setup at the ZIP
root. Both require a version parameter, product name Windows Dynamic Island,
and matching product version. Neither signs upstream DLLs or the uninstaller
stub; see docs/RELEASING.md for that required step.

## Integration after onboarding

Use signpath/github-action-submit-signing-request pinned to commit
f6d04783b4569d051e0c80105fe66e82819d0092 (v3). Configure:

- api-token: protected-environment secret SIGNPATH_API_TOKEN (submitter only).
- organization-id and project-slug: the values assigned during onboarding.
- signing-policy-slug: test-signing for initial verification.
- artifact-configuration-slug: applications or installer.
- github-artifact-id: artifact-id output of the upload-artifact step in the
  same workflow; do not accept IDs or slugs from untrusted PR inputs.
- wait-for-completion: true.
- output-artifact-directory: a fresh directory for returned signed files.
- parameters: version as a JSON-quoted string matching the build metadata.

All preceding jobs must run on GitHub-hosted runners. Grant contents: read and
actions: read; keep secrets out of pull_request jobs. Restrict the production
workflow to reviewed release refs in this repository and a protected environment.
Every production request must additionally require manual approval in SignPath.

Do not publish test-signed artifacts. Credentials and production integration
are intentionally not enabled before acceptance and validation.
