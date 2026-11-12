# Privacy policy

Windows Dynamic Island is a local Windows desktop utility. The project does not
operate an analytics, advertising, account, or cloud-sync service. This statement
describes the source in this repository, not the policies of apps you launch.

## Local data

Settings, task checklists, notes, application entries, and file-shelf metadata
are stored in %LOCALAPPDATA%\DynamicIsland\configuration.json. Files added to
the shelf are copied to %LOCALAPPDATA%\DynamicIsland\FileDock. Custom logo
images are stored locally. These files are not encrypted by the application;
protect them using your Windows account and disk security.

Media titles, artists, artwork, playback state, system metrics, and running
window information come from Windows APIs. Clipboard text/files are read when
you use the clipboard or paste features. Optional notification access requires
Windows permission and a capable packaged build; notification content is not
saved in configuration. Camera preview requires the relevant Windows access.
The project does not upload these contents.

TempVault export copies files to the directory you configure. A network drive
or cloud-synced directory can transmit files through its own provider.
Auto-copy is optional. Apps launched or hosted in the island retain their own
network behavior and privacy policies.

## Weather and location

Weather uses HTTPS requests to https://wttr.in. That service sees your public
IP address, request time, HTTP headers, and the location query. Network-based
weather uses an approximate location inferred by the provider. Selecting
**Use my location** asks Windows for a location fix and sends latitude and
longitude (rounded to four decimal places) to wttr.in; if unavailable, the app
falls back to network-based weather. Coordinates are not saved in configuration.

Weather requests occur when Weather is opened/refreshed or configured as
preview/startup content. To avoid those requests, do not select Weather as
content or open it. The installer offers an option to disable online weather
for the installation, including all users; it is off by default. Other panels
work without weather networking.

wttr.in is independently operated. Consult its
[service and documentation](https://github.com/chubin/wttr.in) for provider
information. The project cannot promise the provider's log-retention practices.
Windows location services are governed separately by
[Microsoft's privacy statement](https://privacy.microsoft.com/privacystatement).
The app does not control OS-level diagnostics or third-party app traffic.

## Control Center

The companion communicates with the island over a loopback-only HTTP endpoint
on port 38691. This is local traffic, not a public listening service. The
bridge checks loopback origin and a custom request header, but that header
is not authentication against other programs running on your PC.

## Installation and removal

Setup displays this policy, installs program files and Start menu shortcuts,
registers the dynamicisland-settings URL protocol and uninstaller, and offers
optional desktop and sign-in shortcuts. It does not change security policies.
Uninstall through Windows Settings > Apps > Installed apps. The current
uninstaller closes the island and Control Center; save work in hosted apps first.

Uninstall preserves local data and exported files. After closing the app,
remove %LOCALAPPDATA%\DynamicIsland manually if you want to erase saved
settings, notes, tasks, images and shelf copies. Exported TempVault files and
Windows clipboard contents must be managed separately.

## Contact

For privacy questions, open an issue in
[WindowsDynamicIsland](https://github.com/acheeseburgercodes/WindowsDynamicIsland/issues).
Do not post private notes, clipboard contents, coordinates, or credentials.
Use the [security reporting process](SECURITY.md) for vulnerabilities.
