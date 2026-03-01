# NatifyOverlay

A Discord notification overlay designed for gaming. It provides a modern, Discord-styled notification interface that remains visible over games and supports interaction.

## Project Overview

- **Core Purpose:** Display Discord messages from specific channels or users as an on-screen overlay while gaming.
- **Main Technologies:**
  - **C# / .NET 6.0-windows:** Core application logic.
  - **WPF (Windows Presentation Foundation):** Used for the high-quality transparent overlay window.
  - **Windows Forms:** Employed specifically for the system tray icon and context menu.
  - **Discord.Net:** Integration with the Discord API.
  - **Win32 API:** Utilized via P/Invoke for advanced window management (click-through, layered transparency, hotkeys, and aggressive "topmost" enforcement).

## Architecture

- **`Program.cs`:** The entry point. Handles application lifecycle, configuration loading (`config.json`), system tray icon creation, and the Discord bot client.
- **`OverlayWindow.cs`:** A code-only WPF window (no XAML file). It manages the UI (Discord dark theme), notifications, and user interaction logic.
- **Configuration:** Users must provide a `config.json` file in the same directory as the executable with a valid Discord Bot Token.

## Features

- **Discord Style:** Dark theme, round avatars, and familiar colors (`#5865F2` blurple, `#36393F` dark grey).
- **Interactivity:**
  - Press **Shift + ~ (Tilde)** to toggle "Interactive Mode".
  - **Interactive Mode:** Allows you to click the overlay and type replies. Note: This may minimize exclusive fullscreen games.
  - **Passive Mode:** Default. Click-through enabled (clicks pass to the game).
- **TopMost Enforcement:** Aggressive logic to stay on top of borderless windowed games.

## Building and Running

### Prerequisites
- [.NET 6.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) or higher.

### Key Commands

- **Build:**
  ```powershell
  dotnet build
  ```
- **Run:**
  ```powershell
  dotnet run
  ```
- **Publish Standalone EXE:**
  This generates a single executable that includes all dependencies (no .NET install required for the end-user).
  ```powershell
  dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true
  ```
  The resulting file will be in `bin\Release\net6.0-windows\win-x64\publish\NatifyOverlay.exe`.

## Development Conventions

- **Visual Style:** Discord Dark Theme.
- **Transparency & Interaction:** Uses `WS_EX_TRANSPARENT` for passive mode. Toggles this off for interactive mode.
- **Fullscreen Compatibility:** Designed for **Windowed Fullscreen** or **Borderless** modes. Exclusive Fullscreen games may minimize when Interactive Mode is active.
- **Bot Permissions:** The Discord bot requires the `MESSAGE CONTENT INTENT` to be enabled in the Discord Developer Portal.
