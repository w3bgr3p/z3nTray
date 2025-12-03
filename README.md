# z3nTray AIO


>


[Русская версия](README_RU.md)

## Features

### 🔐 OTP Generator
- Generate TOTP codes from secret keys
- Automatic clipboard copy
- Smart timeout handling (regenerates if <5s remaining)
- System tray integration

### ⚙️ Process Manager
- Real-time ZennoPoster process monitoring
- Memory and runtime tracking
- Automatic process termination by criteria
- Account binding detection via `--user-data-dir`
- Auto-restore corrupted `Tasks.dat` file
- Raw command line inspection mode

## Installation

### Requirements
- Windows 7 or higher
- .NET Framework 4.7.2+
- Administrator privileges (for WMI process inspection)

### NuGet Dependencies
```bash
Install-Package System.Management
Install-Package OtpNet
```

### Build from Source
```bash
git clone https://github.com/yourusername/OtpTrayApp.git
cd OtpTrayApp
# Open in Visual Studio or Rider
# Build → Build Solution
```

## Usage

### Tray Icon Controls

**Left Click** - Open OTP generator
**Right Click** - Context menu:
- Generate OTP
- Show Processes - statistics window
- Check & Kill Now - execute killer with current settings
- Settings - configure parameters
- Exit

### Process Statistics Window

Displays categorized process list:
- ⏰ **By Time** - processes exceeding age limit
- 💾 **By Memory** - processes exceeding memory limit
- 🌐 **With Browser** - processes with account binding
- ⚠ **Not Bound** - unbound processes + main ZennoPoster

Features:
- Auto-refresh every 5 seconds
- Manual refresh button
- Kill button with confirmation
- Color-coded display

### Configuration

Settings are stored in `App.config`:

```xml
<!-- Browser processes (zbe1) -->
<add key="MaxMemoryForInstance" value="1000" />      <!-- MB -->
<add key="MaxAgeForInstance" value="30" />            <!-- minutes -->

<!-- Main ZennoPoster process -->
<add key="MaxMemoryForZennoposter" value="20000" />  <!-- MB -->

<!-- Kill flags -->
<add key="KillOld" value="True" />
<add key="KillHeavy" value="True" />
<add key="KillMain" value="False" />                  <!-- DANGEROUS! -->

<!-- Automation -->
<add key="AutoCheckInterval" value="0" />             <!-- minutes, 0 = disabled -->

<!-- Resource Monitoring -->
<add key="EnableResourceMonitoring" value="False" />           <!-- enable monitoring -->
<add key="ResourceMonitoringIntervalMinutes" value="1" />      <!-- collection interval -->
<add key="MaxMonitoringRecordsPerFile" value="2000" />         <!-- records before rotation -->
<add key="MonitoringReportRetentionDays" value="7" />          <!-- days to keep reports -->

<!-- UI -->
<add key="ShowLogs" value="False" />                  <!-- balloon tips vs silent -->
<add key="ShowRawCommandLine" value="False" />        <!-- show full command line -->
```

## How It Works

### Process Detection

1. Scans all `zbe1.exe` processes
2. Extracts account from `--user-data-dir="path"` via WMI
3. Tracks memory usage and runtime
4. Categorizes by configured limits

### Killer Logic

```
IF age > MaxAgeForInstance AND KillOld → KILL
IF mem > MaxMemoryForInstance AND KillHeavy → KILL
IF mem > MaxMemoryForZennoposter AND KillMain → KILL ZennoPoster
```

After killing main process:
1. Waits for process exit (max 10s)
2. Checks `Tasks.dat` file size
3. If size < 50 bytes → restores from `Tasks.1.dat`

### Auto-Check

If `AutoCheckInterval > 0`:
- Timer runs every X minutes
- Executes Check & Kill automatically
- Logging depends on `ShowLogs` setting

### Resource Monitoring

If `EnableResourceMonitoring = True`:
- Collects memory usage data from ZennoPoster and zbe1 processes
- Generates HTML reports with interactive charts (Chart.js)
- Reports stored in `./reports/` directory

**File Rotation**:
- Reports are named `resource_monitor_report_YYYY-MM-DD.html`
- Automatically rotates daily or when record limit is reached
- Old reports are deleted after retention period expires
- Each report includes both timestamped snapshots and process lifecycle events

**Default Settings**:
- Collection interval: 1 minute
- Max records per file: 2000
- Retention period: 7 days

## File Structure

```
OtpTrayApp/
├── OtpTrayContext.cs          # Main tray application
├── ProcessManager.cs          # Process logic (no z3nCore deps)
├── ResourceMonitor.cs         # Resource monitoring system
├── AppSettings.cs             # App.config management
├── ProcessStatsForm.cs        # Statistics window
├── SettingsForm.cs            # Settings dialog
├── OtpInputForm.cs            # OTP input dialog
├── App.config                 # Configuration file
└── reports/                   # Monitoring reports directory
    └── resource_monitor_report_YYYY-MM-DD.html
```

## Technical Details

### Process Tracking

**Without shared memory** - direct Process API calls each time:
```csharp
// Get all zbe1 processes
Process.GetProcessesByName("zbe1")

// Extract account from command line
WMI: Win32_Process.CommandLine → --user-data-dir="path"

// Get metrics
proc.WorkingSet64 / (1024 * 1024)  // Memory in MB
DateTime.Now - proc.StartTime       // Runtime
```

### Account Extraction

```csharp
// Command line example:
"C:\...\zbe1.exe" --user-data-dir="F:\accounts\profilesFolder\534\\" ...

// Regex extraction:
--user-data-dir="([^"]+)"

// Result:
Path.GetFileName() → "534"
```

### Tasks.dat Recovery

When killing main ZennoPoster:
1. `proc.Kill()` → `proc.WaitForExit(10000)`
2. Check file: `%AppData%\ZennoLab\ZennoPoster\7\ZennoPoster\Tasks.dat`
3. If size < 50 bytes → corrupted backup
4. Restore: `Tasks.1.dat` → `Tasks.dat`
5. Execute before service restart

## Configuration Examples

### Conservative (default)
```xml
<add key="MaxMemoryForInstance" value="1000" />
<add key="MaxAgeForInstance" value="30" />
<add key="KillOld" value="True" />
<add key="KillHeavy" value="True" />
<add key="KillMain" value="False" />
```

### Aggressive
```xml
<add key="MaxMemoryForInstance" value="500" />
<add key="MaxAgeForInstance" value="15" />
<add key="KillOld" value="True" />
<add key="KillHeavy" value="True" />
<add key="KillMain" value="True" />
<add key="AutoCheckInterval" value="5" />
```

### Monitoring Only
```xml
<add key="KillOld" value="False" />
<add key="KillHeavy" value="False" />
<add key="KillMain" value="False" />
<add key="ShowLogs" value="True" />
```

## Safety

⚠️ **WARNING**: `KillMain = True` can terminate ZennoPoster itself!
- Use only if you have automatic restart service
- Default is `False` for safety
- Always shown in red in UI

## Troubleshooting

### "Unknown" processes in list
- **Cause**: WMI cannot read command line (permissions)
- **Solution**: Run as Administrator
- **Note**: Child processes (renderer, gpu) may not have `--user-data-dir`

### Process not killed
- Check settings are saved (App.config)
- Verify limits are correct
- Enable `ShowLogs` to see execution details
- Check admin rights

### Tasks.dat not restored
- Verify path: `%AppData%\ZennoLab\ZennoPoster\7\ZennoPoster\`
- Check `Tasks.1.dat` exists and size >= 50 bytes
- Enable `ShowLogs` to see recovery process



---

**Made with ❤️ by w3bgr3p**