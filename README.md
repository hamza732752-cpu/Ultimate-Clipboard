# Ultimate Clipboard

A premium, fast, private, and native Windows clipboard manager built with **C#**, **WPF**, and **SQLite** for Windows 10 and Windows 11.

---

## ✨ Features

- **Blazing Fast & Lightweight**: Native Windows desktop app with hardware-accelerated WPF UI. No Electron, no web wrapper, minimal memory footprint.
- **Multiformat Clipboard Support**: Automatically captures and restores:
  - Plain and formatted text (Unicode, multiline, code snippets)
  - Rich Text Format (RTF) and HTML
  - High-resolution images and screenshots (with lazy thumbnail previews)
  - Copied files and folders (preserves paths without duplicating file contents)
- **AI Visual Content Search (On-Device)**:
  - Takes visual search to the next level: screenshots and copied images are automatically analyzed using an embedded MobileNetV2 neural network.
  - Recognizes objects inside images (cats, dogs, cars, screens, laptops, pets, vehicles, etc.).
  - Search for **`cat`** or **`car`** or **`computer`** in the search bar and it instantly finds matching images even if the image file has no text!
  - 100% on-device CPU inference via ONNX Runtime: zero cloud calls, zero external APIs, complete privacy.
- **Software Update Service**:
  - Checks for newer releases with release notes.
  - Dedicated Updates section in Settings and quick access in the system tray menu.
- **Keyboard-First Workflow**:
  - `Win + Shift + V` (or `Ctrl + Shift + V`): Toggle floating panel
  - `↑` / `↓` Arrow Keys: Navigate history
  - `Enter`: Copy selected item to clipboard and close panel
  - `Ctrl + F`: Focus search box
  - `Delete`: Remove selected item
  - `Escape`: Instantly dismiss panel
- **Instant Search**: Real-time debounced search across all copied text, formatted data, and file names.
- **Content Filtering**: Dedicated filter tabs for **All**, **Text**, **Images**, **Files**, **Pinned**, and **Favorites**.
- **Pinning & Favorites**: Keep frequently used links, templates, and reference materials permanent. Pinned entries are never deleted by retention limits or history clears.
- **System Tray Integration**: Quietly runs in the background. Right-click the tray icon to pause/resume monitoring, access Settings, clear history, or exit.
- **Themes**: Elegant Fluent-inspired **Dark** and **Light** themes.
- **Privacy & Security by Design**:
  - 100% local SQLite storage (`%LOCALAPPDATA%\UltimateClipboard\clipboard.db`).
  - Zero cloud sync, zero telemetry, zero analytics, zero external network requests.
  - Exclude sensitive applications (e.g., password managers like KeePass, 1Password, Bitwarden).
  - Configurable retention periods and maximum history limits.

---

## 🚀 How to Run the Application

You can run Ultimate Clipboard using either of the two methods below:

### Method 1: Portable Executable (Recommended)

A fully compiled, self-contained Windows executable is ready in the `dist` folder:

1. Open the project folder in Windows File Explorer:
   ```
   C:\Users\Hamza\Documents\AI-Projects\UltimateClipboard\dist\UltimateClipboard\
   ```
2. Double-click **`UltimateClipboard.exe`**.
3. Ultimate Clipboard will start minimized in your Windows System Tray (near the clock in the bottom right corner of your screen).
4. Press **`Win + Shift + V`** (or **`Ctrl + Shift + V`**) at any time to open your clipboard history panel!

### Method 2: From Source via Terminal

If you wish to run directly from the source code using the .NET SDK:

```powershell
cd C:\Users\Hamza\Documents\AI-Projects\UltimateClipboard
dotnet run --project src/UltimateClipboard
```

---

## ⌨️ Keyboard Shortcuts Reference

| Shortcut | Action |
|---|---|
| `Win + Shift + V` | Open or hide the Clipboard Panel (falls back to `Ctrl + Shift + V` if reserved) |
| `Enter` | Restore selected item back to Windows clipboard and close panel |
| `↑` / `↓` | Navigate through clipboard entries |
| `Ctrl + F` | Jump to search input |
| `Delete` | Delete the currently selected clipboard entry |
| `Esc` | Dismiss / close the clipboard panel |

---

## ⚙️ Configuration & Settings

Click the **Settings gear icon (⚙️)** in the top-right of the clipboard panel or right-click the system tray icon and select **Settings**:

- **Appearance**: Toggle between **Dark** and **Light** modes.
- **Max History Entries**: Configure maximum stored clipboard entries (default: 5,000). Oldest non-pinned items are pruned when limit is reached.
- **Retention (Days)**: Automatically delete unpinned entries older than a specified number of days (default: 90 days).
- **Max Image Size (KB)**: Set image size threshold to prevent huge memory spikes (default: 5 MB).
- **Excluded Apps**: Semicolon-separated list of process names to ignore (e.g., `keepassxc;1password;bitwarden;mstsc`).
- **Database Path**: Customize the SQLite database storage path if desired.

---

## 🔒 Privacy & Data Storage

- All clipboard data and application settings are stored strictly on your local computer at:
  ```
  %LOCALAPPDATA%\UltimateClipboard\
  ├── clipboard.db       (SQLite database for history and snippets)
  └── settings.json      (User preferences)
  ```
- **No telemetry or data transmission**: Ultimate Clipboard does not contain any networking code or external tracking libraries.
- **No passwords stored in code**: Sensitive data filtering is provided for known password managers.

---

## 🧪 Running Automated Tests

To run the complete automated test suite:

```powershell
cd C:\Users\Hamza\Documents\AI-Projects\UltimateClipboard
dotnet test
```

All 16 unit tests cover database CRUD, full-text and partial search, duplicate hash resolution, type filtering, retention policies, settings serialization, and snippet management.

---

## 🙏 Acknowledgements & Credits

- Special credit and inspiration to **[MeheReal/clipboardmanager](https://github.com/MeheReal/clipboardmanager)** by [MeheReal](https://github.com/MeheReal) for inspiring key clipboard management ideas and workflows.

---

## 📂 Project Structure

```
UltimateClipboard/
├── dist/
│   └── UltimateClipboard/             # Self-contained portable executable
│       └── UltimateClipboard.exe
├── src/
│   └── UltimateClipboard/             # WPF Application project
│       ├── Assets/                    # Application icons
│       ├── Converters/                # XAML value converters
│       ├── Data/                      # SQLite persistence (ClipboardDatabase.cs)
│       ├── Helpers/                   # Win32 P/Invoke, Image processing, Hashing
│       ├── Models/                    # ClipboardEntry, Snippet, AppSettings
│       ├── Services/                  # Capture, Restore, Monitor, Hotkeys, Tray
│       ├── Themes/                    # DarkTheme, LightTheme, SharedStyles
│       ├── ViewModels/                # MainViewModel, SettingsViewModel, SnippetViewModel
│       └── Views/                     # ClipboardPanel, SettingsWindow
├── tests/
│   └── UltimateClipboard.Tests/       # xUnit unit test suite
├── PROGRESS.md                        # Progress tracking document
└── README.md                          # Documentation
```
