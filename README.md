# Claude Sessions for Command Palette

A PowerToys Command Palette extension that shows your live **Claude Code sessions** in the **Dock** and jumps to the right Windows Terminal tab when you click one.

> Unofficial community project. Not affiliated with or endorsed by Anthropic or Microsoft.

<!-- screenshot: Dock band -->
<!-- screenshot: session list flyout -->

- **In the Dock:** one compact item showing your sessions and their state (🟢 working, 🟠 waiting for you, ⚪ idle).
- **Click it:** the session list; pick a session to bring its terminal tab to the front.
- **Also in the palette:** search **Claude Sessions** for the same list, with folder, status and how long the session has been in that status.

The extension is **read-only** for Claude Code's data: it never writes to your Claude files and makes no network calls.

## How status is read

Claude Code writes one file per running session to `~/.claude/sessions/<pid>.json`. The extension polls that folder (every 2 s by default), keeps only interactive sessions whose process is still alive (PID-reuse safe), and maps the `status` field:

| `status` | Shown as |
|---|---|
| `busy`, `shell` | 🟢 Working (`shell` = running a shell command) |
| `waiting` | 🟠 Waiting, with the `waitingFor` text (e.g. "input needed") |
| anything else | ⚪ Idle |

Sessions are sorted with waiting first, then working, then idle. `CLAUDE_CONFIG_DIR` is honoured.

## How click-to-focus works

1. `AttachConsole(pid)` attaches to the session's console (for about 100 ms), then `GetConsoleWindow` gives the console window and its root owner is the **Windows Terminal window**.
2. The session's console title is temporarily set to a unique marker. UI Automation then finds the tab with that name and selects it; the original title is restored right after.
3. If Claude keeps rewriting its own title (the busy spinner), the extension falls back to matching the tab by the session's original title with the spinner stripped, accepted only when exactly one tab matches.
4. The window is restored if minimized and brought to the foreground.

Details and limits:
- **Single-tab windows** have no tab strip, so only the window is focused.
- A profile with `suppressApplicationTitle` set keeps a fixed tab title, so the marker never shows; you get the window, not the tab.
- **Elevated sessions can't be reached** from a non-elevated extension. You get a "Couldn't reach this session's terminal (running as administrator?)" toast.

## Requirements

- Windows 10 19041+ / Windows 11, PowerToys with Command Palette **0.9 or later** (Dock support)
- Claude Code running in **Windows Terminal** for tab-level focus (other hosts get window-only focus)

## Installation

### 1. Turn on the Dock

1. Install or update [PowerToys](https://github.com/microsoft/PowerToys/releases) and make sure **Command Palette** is enabled in PowerToys Settings.
2. Open Command Palette (default <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>Space</kbd>) → **Settings** → **Dock (Preview)** → turn on **Enable Dock**.

### 2. Download

From the latest release download `ClaudeSessionsDev.cer` and `ClaudeSessions_<version>_x64.msix` (Intel/AMD) or `..._arm64.msix` (Arm). Not sure? Run `$env:PROCESSOR_ARCHITECTURE`: `AMD64` → x64, `ARM64` → arm64.

### 3. Trust the certificate (once per machine)

The package is signed with a self-signed certificate. In **PowerShell as Administrator**, in the download folder:

```powershell
Import-Certificate .\ClaudeSessionsDev.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

### 4. Install

```powershell
Add-AppxPackage .\ClaudeSessions_<version>_x64.msix
```

### 5. Show it in the Dock

1. Open Command Palette and run **Reload**.
2. The *Claude Sessions* band usually appears by itself. If not, search **Claude Sessions**, open its context menu and run **Pin to Dock**.

### Update / Uninstall

Update: install the newer `.msix` the same way, then **Reload**. Uninstall:

```powershell
Get-AppxPackage ClaudeSessions | Remove-AppxPackage
```

## Dock layouts

Command Palette → *Claude Sessions* → *Settings* → **Dock layout**:

| Layout | What you get |
|---|---|
| **Compact: opens the session list** (default) | One Dock item; clicking opens a flyout listing every session |
| **Compact: opens a summary table** | One Dock item; the flyout shows a table (session, folder, status, time in status) with a focus command per session (the host shows these in the bottom bar and the More (Ctrl+K) menu, not as a list) |
| **One icon per session** | One Dock icon per session (🟢/🟠/⚪); the tooltip is "name · folder · status" and clicking focuses it |

## Settings

- **Dock layout**: see above.
- **Refresh interval**: 1, 2 (default), 5 or 10 seconds. Only visible changes (status, waiting text, name, folder, or the session set) repaint the Dock.
- **Notifications**: a Windows toast when a session starts waiting for you (permission prompt, input, dialog) and when a turn of at least 15 seconds finishes. Choices: waiting + finished (default), only waiting, or off. States present at startup and sessions opening or closing never toast; a newer toast for the same session replaces the older one. Clicking a toast only dismisses it.

## Build from source

Needs the .NET 10 SDK. A full Windows SDK / Visual Studio is **not** required.

```powershell
dotnet build ClaudeSessions.sln -p:Platform=x64
dotnet test tests\ClaudeSessions.Tests -p:Platform=x64   # unit tests
.\scripts\dev-deploy.ps1                                 # build + register (Developer Mode on)
.\scripts\dev-deploy.ps1 -Remove                         # unregister
```

After deploying, run **Reload** in Command Palette.

### MSIX package

```powershell
.\scripts\pack.ps1 -Platform x64 -Sign   # dist\...\ClaudeSessions_<ver>_x64.msix + dist\ClaudeSessionsDev.cer
```

`-Platform ARM64` builds the Arm package. The first `-Sign` run creates a self-signed `CN=ClaudeSessionsDev` code-signing certificate in `Cert:\CurrentUser\My` and reuses it afterwards. The Release build is trimmed and must publish with 0 trim/AOT warnings.

## Layout

```
src/ClaudeSessions/        Command Palette extension (Dock band, flyout, list pages, settings)
src/ClaudeSessions.Core/   Plain .NET library: session parser, scanner, liveness, terminal focus (UIA)
tests/ClaudeSessions.Tests xUnit tests for Core
scripts/                   dev-deploy.ps1, pack.ps1
```

## Diagnostics

Errors and one line per click (result and elapsed ms) go to `%LOCALAPPDATA%\ClaudeSessions\diag.log` (under `%LOCALAPPDATA%\Packages\<package family>\LocalCache\Local\ClaudeSessions\` when installed as MSIX), rolled at 1 MB.

## Known limitations

- Tab-level focus targets **Windows Terminal**; other terminal hosts get window-only focus.
- The `~/.claude/sessions` file format is **undocumented** and may change with Claude Code updates; unknown statuses show as Idle.
- Elevated sessions can't be reached (see above); `suppressApplicationTitle` profiles get window-only focus.
- Command Palette host issues (Dock bands not repainting, flyout not opening after long idle) are fixed by running **Reload**.

## License

[MIT](LICENSE). Parts derived from the PowerToys extension template are © Microsoft, MIT; see [NOTICE](NOTICE).
