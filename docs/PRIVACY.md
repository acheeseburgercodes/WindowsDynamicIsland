# Privacy policy

Windows Dynamic Island is a local Windows desktop application. It does not include advertising, analytics, telemetry, or an account system.

## Data stored locally

The island stores its configuration, appearance settings, saved application entries, focus/timer state, optional mini-logo image, and durable File Dock copies under the current Windows user's local application-data folder. It can read and write the Windows clipboard only when the user uses the clipboard features. Docked files remain on the device until the user removes them.

If the optional notification feature is enabled in a packaged build, notification previews are displayed in memory and are not persisted by the notification service.

## Network and local integrations

- When the user requests weather, the island sends the selected place name to `wttr.in`. If no place is supplied, that service may derive an approximate location from the network request. Weather is not fetched automatically in the background without a user action.
- The Control Center communicates with the running island through a loopback-only local connection on the same device.
- TempVault support copies files only to the local TempVault `Inbox` folder selected by the user. It does not upload them to a service operated by this project.
- Media, hardware, running-window, clipboard, camera, and notification features use Windows APIs locally. Camera and notification access require the applicable Windows permission.

The application does not sell, share, or transmit user data to the project maintainer. Third-party services selected by the user, including `wttr.in`, TempVault, media apps, and Windows itself, have their own policies.

## Your choices

You can decline Windows permissions, leave the weather location blank, disable TempVault auto-copy, clear clipboard data, remove docked files, and uninstall the application. Uninstalling removes installed program files; user-created configuration and File Dock data can be removed separately from the local application-data folder.
