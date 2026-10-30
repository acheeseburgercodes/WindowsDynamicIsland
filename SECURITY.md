# Security policy

## Supported releases

Only the latest GitHub Release is supported. Signed releases will state the
SignPath Foundation signer and include a SHA-256 checksum for every download.
Do not rely on an executable that is unsigned, self-signed, or signed by an
unexpected publisher.

## Reporting a vulnerability

Please use GitHub's private vulnerability-reporting flow for
`acheeseburgercodes/WindowsDynamicIsland`. Do not publish a proof of concept,
exploit details, private configuration, signing credentials, or user data in a
public issue.

Include the affected release/version, Windows version, clear reproduction
steps, and any relevant logs with personal information removed. The maintainer
will acknowledge a report, assess the impact, and coordinate a fix and release.

## Release-signing incident reports

Report an unexpected signer, a checksum mismatch, or a suspected compromised
release through the same private reporting channel. Include the release URL,
file name, SHA-256 value, and the output of
`Get-AuthenticodeSignature <file>` where possible.
