# Dependency review

The November 13, 2026 local production-graph audit reports 0 critical, 0 high,
15 moderate and 3 low findings after compatible lockfile updates and an explicit
@xmldom/xmldom 0.8.15 override. This is a dated observation, not a certification
or a promise that the app is vulnerability-free. CI archives a fresh report and
fails for high/critical findings or an unsuccessful audit request.

The remaining report includes fast-xml-parser, minimatch and deepmerge, plus
React Native CLI/Windows helper packages that inherit those findings. npm's
production graph includes build tooling brought in by react-native-windows;
the classification does not establish exploitability in the installed app.
Review each advisory and upstream upgrade path before production signing. Do
not use npm audit fix --force to silently migrate the native framework.

The xmldom override patches the XML parser used by Windows CLI/telemetry tooling
without changing the React Native Windows version. Validate autolinking,
code generation, native build, Jest and TypeScript whenever changing it.

Dependency licenses are collected conservatively from npm package metadata,
package notices and NuGet nuspec/license files. Legacy license URLs are retained
as evidence, not converted into an assumed SPDX license. Windows SDK build
tools are build inputs, whereas some Microsoft runtime binaries are shipped.
Review their redistribution terms and the Foundation's system-library exception
with SignPath. Their inclusion is not evidence that all bundled code is MIT.

Before signing, resolve unknown ownership, missing notice texts, unsupported
license expressions and material security findings. Record any accepted risk
with the actual advisory, affected build and maintainer rationale; this file
does not waive the remaining advisories.
