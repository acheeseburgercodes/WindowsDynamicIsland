# Security policy

## Supported versions

Security fixes target the latest release and current main branch. Older releases
may need an upgrade; this project does not promise a response SLA.

## Reporting

Do not post exploit details, private files, tokens, or personal data in public
issues. Use GitHub's **Report a vulnerability** action in the repository's
Security tab if private vulnerability reporting has been enabled. If that
action is unavailable, open an issue asking the maintainer for a private
reporting channel without disclosing the vulnerability. The maintainer must
enable private reporting before the SignPath application is submitted.

Include affected versions, Windows version, reproduction steps, and impact.
Reports about signing, release provenance, or the installer are security reports.

## Maintainer requirements

Use MFA for GitHub and SignPath, least-privilege access, reviewed pull requests,
and manual production-signing approval. Do not put certificates, private keys,
tokens, personal test data, or local security-configuration scripts in the
repository or release. No workflow should disable Windows security or trust an
arbitrary local certificate.

The local Control Center bridge is not a security boundary against another
process running as the user. External-window hosting is user initiated and
must respect Windows privilege boundaries.
