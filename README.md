# NatifyOverlay

A modern Discord notification overlay specifically designed for Minecraft players (Badlion, Lunar Client, etc.). It mimics the Discord UI and includes a custom "Glue" logic to stay visible even over fullscreen games.

## 🚀 Features
- **Discord Visuals:** Dark theme, round avatars, and Blurple accents.
- **Customizable Themes:** Switch between "Discord" (Dark) and "Pink" (Light/Pink) via the config file.
- **Sound Notifications:** Toggleable notification "ping" sound when a new message arrives.
- **Auto-Fix for Fullscreen:** Automatically detects Minecraft/Lunar/Badlion and forces them into a borderless mode that allows the overlay to sit on top.
- **Interactive Mode:** Press a hotkey to focus the overlay and type replies without alt-tabbing.
- **Run at Startup:** Toggleable option to launch with Windows.
- **Lightweight:** Uses low-level Win32 APIs to ensure zero impact on game FPS.

## ⌨️ Controls
- **`Shift` + `~` (Tilde):** Toggle Interactive Mode (Unlock mouse to click/reply).
- **`Ctrl` + `Shift` + `B`:** Manually force the active game into Borderless/Overlay-friendly mode.

## 🛠️ Setup
1. Download the latest `NatifyOverlay.exe`.
2. Place your bot token in the `config.json` file that is generated on the first run.
3. **Customize your experience:** Update `config.json` to change themes or toggle sound:
   ```json
   {
     "BotToken": "...",
     "Theme": "Pink",
     "PlaySound": true
   }
   ```
4. Ensure the `MESSAGE CONTENT INTENT` is enabled in your Discord Developer Portal.

**Note:** The application icon is now embedded inside the executable, so you only need the `.exe` file!

## 📦 How to Build
To generate a standalone, single-file executable:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true
```
