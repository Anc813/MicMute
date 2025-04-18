# Mute mic

Mute default capture device (typically microphone) using tray icon click or custom shortcut.

![Image](img.png?raw=true "Image")

### Download binary

[Direct link to latest version](https://github.com/Anc813/MicMute/releases/latest/download/MicMute.exe)

[All versions](https://github.com/Anc813/MicMute/releases)

### Features

[How to add sound on mute / unmute](https://github.com/Anc813/MicMute/releases/tag/0.0.5)

### Troubleshooting

**Hotkey Issues in Full-Screen Applications or applications running with administrator privileges**

*   **Problem:** MicMute hotkeys might stop working when certain applications, particularly full-screen games (like Oxygen Not Included) or applications running with administrator privileges, are in the foreground. This can lead to the microphone staying on unexpectedly.
*   **Cause:** Windows security mechanisms can prevent hotkey signals from reaching applications running with standard user privileges when an application with administrator privileges is active.
*   **Solution:** Run `MicMute.exe` with administrator rights. This allows MicMute to receive hotkey signals regardless of which application is currently active or its privilege level. To do this, right-click the `MicMute.exe` file and select "Run as administrator". For persistent use, you can configure the executable or a shortcut to always run as administrator.
*   **See this comment for auto starting as administrator:**  https://github.com/Anc813/MicMute/issues/48#issuecomment-2815416640

### Libraries that used in this project

Shortcut library https://github.com/AlexArchive/Shortcut

AudioSwitcher library https://github.com/xenolightning/AudioSwitcher

Costura.Fody https://github.com/Fody/Costura

Icons from  flaticon.com [one](https://www.flaticon.com/free-icon/microphone-black-shape_25682#term=mic&page=1&position=1 "one") [two](https://www.flaticon.com/free-icon/microphone-off_25632#term=mic&page=1&position=3 "two")

