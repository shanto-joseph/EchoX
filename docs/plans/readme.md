Product Requirements Document (PRD): EchoX
1. Product Overview
Objective: To build a super-lightweight, lightning-fast Windows desktop utility that allows users to seamlessly manage, customize, and switch between different audio input (microphone) and output (speaker/headphone) configurations.
Value Proposition: Unlike bulky audio suites, this application will consume minimal system resources, making it ideal for low-end devices, gamers, and streamers who need instant audio routing without performance drops.

2. System Requirements
Operating System: Windows 10 and Windows 11.

Performance Target: Near-zero idle CPU usage; minimal RAM footprint.

Tech Stack: C# and .NET with Windows Presentation Foundation (WPF) for a highly responsive, low-resource native UI. Audio manipulation will be handled via the AudioSwitcher core API.

3. Core Features (Functional Requirements)
Device Discovery: Automatically detect and list all active Windows playback and recording devices.

Profile Management:

Create, rename, edit, and delete custom audio profiles.

Assign a specific playback device and recording device to each profile.

Export/Import: Allow users to export their profile configurations as a file (e.g., JSON) and import them, making it easy to backup or share setups.

Global Hotkeys:

Assign a unique, system-wide keyboard shortcut to activate each specific profile.

Dedicated shortcut to cycle/toggle through available profiles.

Dedicated shortcut to instantly mute/unmute the active microphone.

Dedicated shortcut to bring the application window to the foreground.

System Tray Integration:

App runs quietly in the system tray.

Hovering over or clicking the tray icon displays the Current Profile Name.

Right-click Context Menu includes: Open App, Current Profile Info, Check for Updates, GitHub Repo Link, Exit.

4. Advanced "Pro" Features
Auto-Switching by App (Process Detection): The app monitors running processes. If a user-defined application (e.g., Spotify.exe or Valorant.exe) is launched or brought to focus, the software automatically switches to the linked audio profile.

Per-Profile Volume Memory: Saves the master volume level within the profile. Switching to a profile automatically adjusts the Windows volume to that profile's saved state.

The "Unplugged" Fallback (Device State Monitoring): If an active device in the current profile disconnects (e.g., wireless headset dies, USB mic is unplugged), the app automatically falls back to a user-designated "Default" profile to prevent audio loss.

5. User Interface & Experience (UI/UX)
Design Philosophy: Clean, modern, and uncluttered.

Theming: * Support for Light Mode and Dark Mode.

Custom UI Themes (allowing users to pick accent colors).

Notification System (Bottom-Right OSD): When a profile changes, a sleek popup appears in the bottom right of the screen. Users can choose their notification style in Settings:

Popup Screen Notify (App's custom UI - Default)

Windows Native Notification (Using Windows Action Center)

Sound Only (A subtle chime with no visual popup)

None (Completely silent switching)

Multilingual Support: UI text mapped to resource files to allow for community-driven translations (English default, with architecture to add more).

6. App Settings & Behaviors
Boot Behavior: Checkbox to "Start with Windows" (Launch on startup minimized to tray).

Update Engine: Automated update checking via GitHub Releases. Options include:

Install Automatically

Notify when an update is available

Do not check for updates