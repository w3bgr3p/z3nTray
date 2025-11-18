using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OtpTrayApp
{
    /// <summary>
    /// Snapshot of process metrics at a specific time
    /// </summary>
    public class ProcessSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int Pid { get; set; }
        public string ProcessName { get; set; }
        public long MemoryMB { get; set; }
        public string CommandLine { get; set; }
        public string Account { get; set; } // from --user-data-dir
        public bool IsAlive { get; set; }
    }

    /// <summary>
    /// Process lifecycle event (start/stop)
    /// </summary>
    public class ProcessEvent
    {
        public DateTime Timestamp { get; set; }
        public int Pid { get; set; }
        public string ProcessName { get; set; }
        public string EventType { get; set; } // "started" or "stopped"
        public string CommandLine { get; set; }
        public string Account { get; set; }
    }

    /// <summary>
    /// Monitoring session (from app start to ZennoPoster kill or app exit)
    /// </summary>
    public class MonitoringSession
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public List<ProcessSnapshot> Snapshots { get; set; } = new List<ProcessSnapshot>();
        public List<ProcessEvent> Events { get; set; } = new List<ProcessEvent>();
        public string EndReason { get; set; } // "ZennoPosterKilled", "AppExit", etc.
    }

    /// <summary>
    /// Report data for JSON serialization
    /// </summary>
    public class ReportData
    {
        public SessionInfo Session { get; set; }
        public ChartData ZennoPoster { get; set; }
        public ChartData Zbe1 { get; set; }
        public List<EventData> Events { get; set; }
    }

    public class SessionInfo
    {
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public double DurationMinutes { get; set; }
        public string EndReason { get; set; }
        public bool IsActive { get; set; }
    }

    public class ChartData
    {
        public List<string> Labels { get; set; }
        public List<DatasetInfo> Datasets { get; set; }
    }

    public class DatasetInfo
    {
        public string Label { get; set; }
        public List<long> Data { get; set; }
        public string Color { get; set; }
        public string CommandLine { get; set; }
    }

    public class EventData
    {
        public string Timestamp { get; set; }
        public int Pid { get; set; }
        public string ProcessName { get; set; }
        public string EventType { get; set; }
        public string CommandLine { get; set; }
        public string Account { get; set; }
    }

    /// <summary>
    /// Resource monitoring system for debugging memory leaks
    /// </summary>
    public class ResourceMonitor : IDisposable
    {
        private readonly object lockObj = new object();
        private System.Threading.Timer collectionTimer;
        private MonitoringSession currentSession;
        private Dictionary<int, ProcessSnapshot> lastSnapshots = new Dictionary<int, ProcessSnapshot>();
        private readonly string reportsDirectory;
        private readonly string currentReportPath;
        private readonly string currentDataPath;
        private bool isRunning = false;
        private int intervalMinutes = 1;

        public ResourceMonitor()
        {
            // Create reports directory next to executable
            var exeDir = AppContext.BaseDirectory;
            reportsDirectory = Path.Combine(exeDir, "reports");


            if (!Directory.Exists(reportsDirectory))
            {
                Directory.CreateDirectory(reportsDirectory);
            }

            currentReportPath = Path.Combine(reportsDirectory, "current_report.html");
            currentDataPath = Path.Combine(reportsDirectory, "current_data.json");
        }

        /// <summary>
        /// Start monitoring
        /// </summary>
        public void Start(int intervalMinutes = 1)
        {
            lock (lockObj)
            {
                if (isRunning) return;

                this.intervalMinutes = intervalMinutes;

                currentSession = new MonitoringSession
                {
                    StartTime = DateTime.Now
                };

                // Start timer with configured interval - first collection happens after interval
                // This prevents blocking the UI thread on startup
                collectionTimer = new System.Threading.Timer(CollectMetrics, null, TimeSpan.FromMinutes(intervalMinutes), TimeSpan.FromMinutes(intervalMinutes));
                isRunning = true;
            }
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void Stop(string reason = "AppExit")
        {
            lock (lockObj)
            {
                if (!isRunning) return;

                collectionTimer?.Dispose();
                collectionTimer = null;

                if (currentSession != null)
                {
                    currentSession.EndTime = DateTime.Now;
                    currentSession.EndReason = reason;

                    // Save final report
                    SaveSessionReport(currentSession);
                }

                isRunning = false;
            }
        }

        /// <summary>
        /// Create checkpoint (save current session and start new one)
        /// Called when ZennoPoster is killed
        /// </summary>
        public void CreateCheckpoint(string reason = "ZennoPosterKilled")
        {
            lock (lockObj)
            {
                if (currentSession == null) return;

                currentSession.EndTime = DateTime.Now;
                currentSession.EndReason = reason;

                // Save current session to timestamped file
                SaveSessionReport(currentSession);

                // Start new session
                currentSession = new MonitoringSession
                {
                    StartTime = DateTime.Now
                };
                lastSnapshots.Clear();

                // Collect initial snapshot for new session
                CollectMetrics(null);
            }
        }

        /// <summary>
        /// Collect metrics from all processes (async via ThreadPool)
        /// </summary>
        private void CollectMetrics(object state)
        {
            // Run in background thread to avoid blocking
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    CollectMetricsInternal();
                }
                catch (Exception ex)
                {
                    // Log error silently, don't crash the timer
                    System.Diagnostics.Debug.WriteLine($"Error collecting metrics: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Internal synchronous metrics collection
        /// </summary>
        private void CollectMetricsInternal()
        {
            lock (lockObj)
            {
                if (currentSession == null) return;

                var timestamp = DateTime.Now;
                var currentPids = new HashSet<int>();

                // Collect ZennoPoster processes
                var zennoProcesses = Process.GetProcessesByName("ZennoPoster");
                foreach (var proc in zennoProcesses)
                {
                    try
                    {
                        var snapshot = CreateSnapshot(proc, timestamp, "ZennoPoster");
                        currentSession.Snapshots.Add(snapshot);
                        currentPids.Add(proc.Id);

                        // Track start event
                        if (!lastSnapshots.ContainsKey(proc.Id))
                        {
                            currentSession.Events.Add(new ProcessEvent
                            {
                                Timestamp = timestamp,
                                Pid = proc.Id,
                                ProcessName = "ZennoPoster",
                                EventType = "started",
                                CommandLine = snapshot.CommandLine,
                                Account = snapshot.Account
                            });
                        }
                    }
                    catch { }
                }

                // Collect zbe1 processes
                var zbe1Processes = Process.GetProcessesByName("zbe1");
                foreach (var proc in zbe1Processes)
                {
                    try
                    {
                        var snapshot = CreateSnapshot(proc, timestamp, "zbe1");
                        currentSession.Snapshots.Add(snapshot);
                        currentPids.Add(proc.Id);

                        // Track start event
                        if (!lastSnapshots.ContainsKey(proc.Id))
                        {
                            currentSession.Events.Add(new ProcessEvent
                            {
                                Timestamp = timestamp,
                                Pid = proc.Id,
                                ProcessName = "zbe1",
                                EventType = "started",
                                CommandLine = snapshot.CommandLine,
                                Account = snapshot.Account
                            });
                        }
                    }
                    catch { }
                }

                // Detect stopped processes
                foreach (var lastPid in lastSnapshots.Keys.ToList())
                {
                    if (!currentPids.Contains(lastPid))
                    {
                        var lastSnapshot = lastSnapshots[lastPid];
                        currentSession.Events.Add(new ProcessEvent
                        {
                            Timestamp = timestamp,
                            Pid = lastPid,
                            ProcessName = lastSnapshot.ProcessName,
                            EventType = "stopped",
                            CommandLine = lastSnapshot.CommandLine,
                            Account = lastSnapshot.Account
                        });
                    }
                }

                // Update last snapshots
                lastSnapshots.Clear();
                foreach (var pid in currentPids)
                {
                    var snapshot = currentSession.Snapshots.LastOrDefault(s => s.Pid == pid);
                    if (snapshot != null)
                    {
                        lastSnapshots[pid] = snapshot;
                    }
                }

                // Update current report
                GenerateReport(currentSession, currentReportPath, currentDataPath);
            }
        }

        /// <summary>
        /// Create snapshot from process
        /// </summary>
        private ProcessSnapshot CreateSnapshot(Process proc, DateTime timestamp, string processName)
        {
            var memMB = proc.WorkingSet64 / (1024 * 1024);
            var cmdLine = GetProcessCommandLine(proc.Id);
            var account = ExtractAccount(cmdLine);

            return new ProcessSnapshot
            {
                Timestamp = timestamp,
                Pid = proc.Id,
                ProcessName = processName,
                MemoryMB = memMB,
                CommandLine = cmdLine ?? "",
                Account = account ?? "unknown",
                IsAlive = true
            };
        }

        /// <summary>
        /// Get process command line via WMI
        /// </summary>
        private string GetProcessCommandLine(int pid)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}"))
                {
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        return obj["CommandLine"]?.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract account from --user-data-dir parameter
        /// </summary>
        private string ExtractAccount(string cmdLine)
        {
            if (string.IsNullOrEmpty(cmdLine)) return null;

            var match = System.Text.RegularExpressions.Regex.Match(
                cmdLine,
                @"--user-data-dir=""([^""]+)""");

            if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
            {
                var path = match.Groups[1].Value.Trim('\\');
                return Path.GetFileName(path);
            }

            return null;
        }

        /// <summary>
        /// Save session report to timestamped file
        /// </summary>
        private void SaveSessionReport(MonitoringSession session)
        {
            var timestamp = session.StartTime.ToString("yyyy-MM-dd_HH-mm-ss");
            var reportPath = Path.Combine(reportsDirectory, $"report_{timestamp}.html");
            var dataPath = Path.Combine(reportsDirectory, $"data_{timestamp}.json");
            GenerateReport(session, reportPath, dataPath);
        }

        /// <summary>
        /// Generate HTML report with inline JSON data
        /// </summary>
        private void GenerateReport(MonitoringSession session, string htmlPath, string jsonPath)
        {
            // Generate JSON data
            var reportData = PrepareReportData(session);
            var json = SerializeToJson(reportData);

            // Save JSON as separate file for reference (optional)
            File.WriteAllText(jsonPath, json);

            // Generate HTML with inline JSON
            var html = GenerateHtmlTemplate(json, session);
            File.WriteAllText(htmlPath, html);
        }

        /// <summary>
        /// Prepare data for JSON serialization
        /// </summary>
        private ReportData PrepareReportData(MonitoringSession session)
        {
            var data = new ReportData
            {
                Session = new SessionInfo
                {
                    StartTime = session.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    EndTime = session.EndTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    DurationMinutes = session.EndTime.HasValue
                        ? (session.EndTime.Value - session.StartTime).TotalMinutes
                        : 0,
                    EndReason = session.EndReason,
                    IsActive = !session.EndTime.HasValue
                },
                ZennoPoster = PrepareChartData(session, "ZennoPoster"),
                Zbe1 = PrepareChartData(session, "zbe1"),
                Events = session.Events.OrderBy(e => e.Timestamp).Select(e => new EventData
                {
                    Timestamp = e.Timestamp.ToString("HH:mm:ss"),
                    Pid = e.Pid,
                    ProcessName = e.ProcessName,
                    EventType = e.EventType,
                    CommandLine = e.CommandLine ?? "",
                    Account = e.Account ?? "unknown"
                }).ToList()
            };

            return data;
        }

        /// <summary>
        /// Prepare chart data for specific process type
        /// </summary>
        private ChartData PrepareChartData(MonitoringSession session, string processName)
        {
            var snapshots = session.Snapshots
                .Where(s => s.ProcessName == processName)
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (snapshots.Count == 0)
                return new ChartData { Labels = new List<string>(), Datasets = new List<DatasetInfo>() };

            var timestamps = snapshots.Select(s => s.Timestamp).Distinct().OrderBy(t => t).ToList();
            var labels = timestamps.Select(t => t.ToString("HH:mm")).ToList();

            var byPid = snapshots.GroupBy(s => s.Pid).ToList();
            var datasets = new List<DatasetInfo>();

            int colorIndex = 0;
            foreach (var group in byPid)
            {
                var pid = group.Key;
                var account = group.FirstOrDefault()?.Account ?? "unknown";
                var label = processName == "zbe1" && account != "unknown"
                    ? $"PID:{pid} ({account})"
                    : $"{processName} PID:{pid}";

                var dataset = new DatasetInfo
                {
                    Label = label,
                    Data = timestamps.Select(t =>
                        group.FirstOrDefault(s => s.Timestamp == t)?.MemoryMB ?? 0
                    ).ToList(),
                    Color = GetChartColor(colorIndex++),
                    CommandLine = group.FirstOrDefault()?.CommandLine ?? ""
                };

                datasets.Add(dataset);
            }

            return new ChartData
            {
                Labels = labels,
                Datasets = datasets
            };
        }

        /// <summary>
        /// Simple JSON serializer (to avoid dependency on System.Text.Json or Newtonsoft)
        /// </summary>
        private string SerializeToJson(ReportData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");

            // Session
            sb.AppendLine("  \"session\": {");
            sb.AppendLine($"    \"startTime\": \"{EscapeJson(data.Session.StartTime)}\",");
            sb.AppendLine($"    \"endTime\": {(data.Session.EndTime != null ? "\"" + EscapeJson(data.Session.EndTime) + "\"" : "null")},");
            sb.AppendLine($"    \"durationMinutes\": {data.Session.DurationMinutes:F1},");
            sb.AppendLine($"    \"endReason\": {(data.Session.EndReason != null ? "\"" + EscapeJson(data.Session.EndReason) + "\"" : "null")},");
            sb.AppendLine($"    \"isActive\": {data.Session.IsActive.ToString().ToLower()}");
            sb.AppendLine("  },");

            // ZennoPoster chart
            sb.AppendLine("  \"zennoPoster\": {");
            SerializeChartData(sb, data.ZennoPoster);
            sb.AppendLine("  },");

            // zbe1 chart
            sb.AppendLine("  \"zbe1\": {");
            SerializeChartData(sb, data.Zbe1);
            sb.AppendLine("  },");

            // Events
            sb.AppendLine("  \"events\": [");
            for (int i = 0; i < data.Events.Count; i++)
            {
                var evt = data.Events[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"timestamp\": \"{EscapeJson(evt.Timestamp)}\",");
                sb.AppendLine($"      \"pid\": {evt.Pid},");
                sb.AppendLine($"      \"processName\": \"{EscapeJson(evt.ProcessName)}\",");
                sb.AppendLine($"      \"eventType\": \"{EscapeJson(evt.EventType)}\",");
                sb.AppendLine($"      \"commandLine\": \"{EscapeJson(evt.CommandLine)}\",");
                sb.AppendLine($"      \"account\": \"{EscapeJson(evt.Account)}\"");
                sb.Append("    }");
                if (i < data.Events.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }
            sb.AppendLine("  ]");

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Serialize chart data to JSON
        /// </summary>
        private void SerializeChartData(StringBuilder sb, ChartData chart)
        {
            // Labels
            sb.AppendLine("    \"labels\": [");
            for (int i = 0; i < chart.Labels.Count; i++)
            {
                sb.Append($"      \"{EscapeJson(chart.Labels[i])}\"");
                if (i < chart.Labels.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }
            sb.AppendLine("    ],");

            // Datasets
            sb.AppendLine("    \"datasets\": [");
            for (int i = 0; i < chart.Datasets.Count; i++)
            {
                var ds = chart.Datasets[i];
                sb.AppendLine("      {");
                sb.AppendLine($"        \"label\": \"{EscapeJson(ds.Label)}\",");
                sb.Append("        \"data\": [");
                sb.Append(string.Join(", ", ds.Data));
                sb.AppendLine("],");
                sb.AppendLine($"        \"color\": \"{EscapeJson(ds.Color)}\",");
                sb.AppendLine($"        \"commandLine\": \"{EscapeJson(ds.CommandLine)}\"");
                sb.Append("      }");
                if (i < chart.Datasets.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }
            sb.AppendLine("    ]");
        }

        /// <summary>
        /// Escape string for JSON
        /// </summary>
        private string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Generate HTML template with inline JSON
        /// </summary>
        private string GenerateHtmlTemplate(string jsonData, MonitoringSession session)
        {
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset='utf-8'>");
            html.AppendLine("    <title>Resource Usage Report</title>");
            html.AppendLine("    <script src='https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js'></script>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: 'Iosevka', 'Consolas', monospace; background: #0d1117; color: #c9d1d9; padding: 15px; }");
            html.AppendLine("        * { margin: 0; padding: 0; box-sizing: border-box; }");
            html.AppendLine("        h1, h2 { color: #c9d1d9; font-weight: 600; }");
            html.AppendLine("        .container { max-width: 1900px; margin: 0 auto; }");
            html.AppendLine("        .header { background: #161b22; border: 1px solid #30363d; padding: 12px 20px; border-radius: 6px; margin-bottom: 15px; }");
            html.AppendLine("        .chart-container { margin: 0 0 15px 0; background: #161b22; border: 1px solid #30363d; padding: 15px; border-radius: 6px; }");
            html.AppendLine("        .info { background: #161b22; border: 1px solid #30363d; padding: 12px 20px; border-radius: 6px; margin: 0 0 15px 0; }");
            html.AppendLine("        .event { padding: 8px 12px; margin-bottom: 6px; border-left: 3px solid #3fb950; background: #0d1117; border-radius: 4px; font-size: 12px; }");
            html.AppendLine("        .event.stopped { border-left-color: #f85149; }");
            html.AppendLine("        .pid-info { color: #58a6ff; cursor: pointer; font-weight: 600; text-decoration: underline dotted; }");
            html.AppendLine("        .cmd-tooltip {");
            html.AppendLine("            position: fixed;");
            html.AppendLine("            background: rgba(0, 0, 0, 0.95);");
            html.AppendLine("            color: #a5d6ff;");
            html.AppendLine("            padding: 10px;");
            html.AppendLine("            border-radius: 5px;");
            html.AppendLine("            max-width: 600px;");
            html.AppendLine("            word-break: break-all;");
            html.AppendLine("            font-size: 11px;");
            html.AppendLine("            font-family: 'Iosevka', 'Consolas', monospace;");
            html.AppendLine("            z-index: 10000;");
            html.AppendLine("            border: 1px solid #58a6ff;");
            html.AppendLine("            pointer-events: none;");
            html.AppendLine("            display: none;");
            html.AppendLine("        }");
            html.AppendLine("        .copy-feedback { ");
            html.AppendLine("            position: fixed; ");
            html.AppendLine("            top: 20px; ");
            html.AppendLine("            right: 20px; ");
            html.AppendLine("            background: #238636; ");
            html.AppendLine("            color: #fff; ");
            html.AppendLine("            padding: 10px 20px; ");
            html.AppendLine("            border-radius: 6px; ");
            html.AppendLine("            font-weight: 600; ");
            html.AppendLine("            z-index: 1000; ");
            html.AppendLine("            animation: fadeInOut 2s ease-in-out; ");
            html.AppendLine("        }");
            html.AppendLine("        @keyframes fadeInOut { ");
            html.AppendLine("            0% { opacity: 0; transform: translateY(-10px); } ");
            html.AppendLine("            10% { opacity: 1; transform: translateY(0); } ");
            html.AppendLine("            90% { opacity: 1; transform: translateY(0); } ");
            html.AppendLine("            100% { opacity: 0; transform: translateY(-10px); } ");
            html.AppendLine("        }");
            html.AppendLine("        canvas { max-height: 400px; }");
            html.AppendLine("        .loading { text-align: center; color: #8b949e; padding: 40px; }");
            html.AppendLine("        .error { background: #da3633; color: #fff; padding: 12px 20px; border-radius: 6px; margin: 15px 0; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("    <div class='container'>");
            html.AppendLine("        <div class='header'>");
            html.AppendLine("            <h1>Resource Usage Report</h1>");
            html.AppendLine("        </div>");
            html.AppendLine("        <div id='sessionInfo' class='info'></div>");
            html.AppendLine("        <div id='zennoChart' class='chart-container'></div>");
            html.AppendLine("        <div id='zbe1Chart' class='chart-container'></div>");
            html.AppendLine("        <div id='eventsTimeline' class='chart-container'></div>");
            html.AppendLine("        <div class='loading'>Loading data...</div>");
            html.AppendLine("    </div>");
            html.AppendLine("    <div id='cmdTooltip' class='cmd-tooltip'></div>");

            html.AppendLine("    <script>");
            html.AppendLine("        // Inline JSON data");
            html.AppendLine($"        const reportData = {jsonData};");
            html.AppendLine();

            // Build commandLines dictionary (PID -> CommandLine)
            html.AppendLine("        // Build command line dictionary (PID -> CommandLine)");
            html.AppendLine("        const commandLines = {};");
            html.AppendLine("        reportData.events.forEach(evt => {");
            html.AppendLine("            if (evt.commandLine && !commandLines[evt.pid]) {");
            html.AppendLine("                commandLines[evt.pid] = evt.commandLine;");
            html.AppendLine("            }");
            html.AppendLine("        });");
            html.AppendLine();

            // Copy to clipboard function
            html.AppendLine("        function copyToClipboard(text) {");
            html.AppendLine("            navigator.clipboard.writeText(text).then(function() {");
            html.AppendLine("                showCopyFeedback();");
            html.AppendLine("            }).catch(function(err) {");
            html.AppendLine("                console.error('Failed to copy: ', err);");
            html.AppendLine("            });");
            html.AppendLine("        }");
            html.AppendLine();

            html.AppendLine("        function showCopyFeedback() {");
            html.AppendLine("            var feedback = document.createElement('div');");
            html.AppendLine("            feedback.className = 'copy-feedback';");
            html.AppendLine("            feedback.textContent = 'Copied to clipboard!';");
            html.AppendLine("            document.body.appendChild(feedback);");
            html.AppendLine("            setTimeout(function() {");
            html.AppendLine("                document.body.removeChild(feedback);");
            html.AppendLine("            }, 2000);");
            html.AppendLine("        }");
            html.AppendLine();

            // Tooltip functions
            html.AppendLine("        function showCommandTooltip(pid, event) {");
            html.AppendLine("            const cmdLine = commandLines[pid];");
            html.AppendLine("            if (!cmdLine) return;");
            html.AppendLine("            const tooltip = document.getElementById('cmdTooltip');");
            html.AppendLine("            tooltip.textContent = cmdLine;");
            html.AppendLine("            tooltip.style.display = 'block';");
            html.AppendLine("            tooltip.style.left = (event.clientX + 10) + 'px';");
            html.AppendLine("            tooltip.style.top = (event.clientY + 10) + 'px';");
            html.AppendLine("        }");
            html.AppendLine();

            html.AppendLine("        function hideCommandTooltip() {");
            html.AppendLine("            const tooltip = document.getElementById('cmdTooltip');");
            html.AppendLine("            tooltip.style.display = 'none';");
            html.AppendLine("        }");
            html.AppendLine();

            html.AppendLine("        function copyCommandLine(pid) {");
            html.AppendLine("            const cmdLine = commandLines[pid];");
            html.AppendLine("            if (cmdLine) {");
            html.AppendLine("                copyToClipboard(cmdLine);");
            html.AppendLine("            }");
            html.AppendLine("        }");
            html.AppendLine();

            // Load data and render
            html.AppendLine("        function escapeHtml(text) {");
            html.AppendLine("            const div = document.createElement('div');");
            html.AppendLine("            div.textContent = text;");
            html.AppendLine("            return div.innerHTML;");
            html.AppendLine("        }");
            html.AppendLine();

            html.AppendLine("        function renderSessionInfo() {");
            html.AppendLine("            const info = document.getElementById('sessionInfo');");
            html.AppendLine("            const s = reportData.session;");
            html.AppendLine("            let html = '<p><strong>Session Start:</strong> ' + escapeHtml(s.startTime) + '</p>';");
            html.AppendLine("            if (!s.isActive) {");
            html.AppendLine("                html += '<p><strong>Session End:</strong> ' + escapeHtml(s.endTime) + '</p>';");
            html.AppendLine("                html += '<p><strong>Duration:</strong> ' + s.durationMinutes.toFixed(1) + ' minutes</p>';");
            html.AppendLine("                html += '<p><strong>End Reason:</strong> ' + escapeHtml(s.endReason) + '</p>';");
            html.AppendLine("            } else {");
            html.AppendLine("                html += '<p><strong>Status:</strong> Active (auto-updating)</p>';");
            html.AppendLine("            }");
            html.AppendLine("            info.innerHTML = html;");
            html.AppendLine("        }");
            html.AppendLine();

            html.AppendLine("        function renderChart(containerId, title, chartData) {");
            html.AppendLine("            if (chartData.datasets.length === 0) return;");
            html.AppendLine("            ");
            html.AppendLine("            const container = document.getElementById(containerId);");
            html.AppendLine("            container.innerHTML = '<h2>' + escapeHtml(title) + '</h2><canvas id=\"' + containerId + 'Canvas\"></canvas>';");
            html.AppendLine("            ");
            html.AppendLine("            const canvas = document.getElementById(containerId + 'Canvas');");
            html.AppendLine("            const ctx = canvas.getContext('2d');");
            html.AppendLine("            ");
            html.AppendLine("            const datasets = chartData.datasets.map(ds => ({");
            html.AppendLine("                label: ds.label,");
            html.AppendLine("                data: ds.data,");
            html.AppendLine("                borderColor: ds.color,");
            html.AppendLine("                backgroundColor: ds.color + '33',");
            html.AppendLine("                tension: 0.1,");
            html.AppendLine("                commandLine: ds.commandLine");
            html.AppendLine("            }));");
            html.AppendLine("            ");
            html.AppendLine("            new Chart(ctx, {");
            html.AppendLine("                type: 'line',");
            html.AppendLine("                data: {");
            html.AppendLine("                    labels: chartData.labels,");
            html.AppendLine("                    datasets: datasets");
            html.AppendLine("                },");
            html.AppendLine("                options: {");
            html.AppendLine("                    responsive: true,");
            html.AppendLine("                    onClick: function(event, elements) {");
            html.AppendLine("                        if (elements.length > 0) {");
            html.AppendLine("                            var datasetIndex = elements[0].datasetIndex;");
            html.AppendLine("                            var dataset = event.chart.data.datasets[datasetIndex];");
            html.AppendLine("                            if (dataset.commandLine) {");
            html.AppendLine("                                copyToClipboard(dataset.commandLine);");
            html.AppendLine("                            }");
            html.AppendLine("                        }");
            html.AppendLine("                    },");
            html.AppendLine("                    plugins: {");
            html.AppendLine("                        legend: { ");
            html.AppendLine("                            labels: { ");
            html.AppendLine("                                color: '#c9d1d9',");
            html.AppendLine("                                boxWidth: 12,");
            html.AppendLine("                                boxHeight: 12,");
            html.AppendLine("                                padding: 15,");
            html.AppendLine("                                usePointStyle: false");
            html.AppendLine("                            }");
            html.AppendLine("                        },");
            html.AppendLine("                        tooltip: {");
            html.AppendLine("                            backgroundColor: 'rgba(0, 0, 0, 0.9)',");
            html.AppendLine("                            titleColor: '#58a6ff',");
            html.AppendLine("                            bodyColor: '#c9d1d9',");
            html.AppendLine("                            padding: 12,");
            html.AppendLine("                            displayColors: true,");
            html.AppendLine("                            bodyFont: { family: 'monospace', size: 11 },");
            html.AppendLine("                            callbacks: {");
            html.AppendLine("                                label: function(context) {");
            html.AppendLine("                                    return context.dataset.label + ': ' + context.parsed.y + ' MB';");
            html.AppendLine("                                },");
            html.AppendLine("                                afterLabel: function(context) {");
            html.AppendLine("                                    var cmdLine = context.dataset.commandLine;");
            html.AppendLine("                                    if (cmdLine && cmdLine !== 'N/A' && cmdLine !== '') {");
            html.AppendLine("                                        var maxLen = 200;");
            html.AppendLine("                                        var lines = [];");
            html.AppendLine("                                        for (var i = 0; i < cmdLine.length; i += maxLen) {");
            html.AppendLine("                                            lines.push(cmdLine.substring(i, i + maxLen));");
            html.AppendLine("                                        }");
            html.AppendLine("                                        return '\\nCommand:\\n' + lines.join('\\n') + '\\n\\n[Click to copy]';");
            html.AppendLine("                                    }");
            html.AppendLine("                                    return '';");
            html.AppendLine("                                }");
            html.AppendLine("                            }");
            html.AppendLine("                        }");
            html.AppendLine("                    },");
            html.AppendLine("                    scales: {");
            html.AppendLine("                        y: { beginAtZero: true, title: { display: true, text: 'Memory (MB)', color: '#c9d1d9' }, ticks: { color: '#c9d1d9' } },");
            html.AppendLine("                        x: { ticks: { color: '#c9d1d9' } }");
            html.AppendLine("                    }");
            html.AppendLine("                }");
            html.AppendLine("            });");
            html.AppendLine("        }");
            html.AppendLine();

            html.AppendLine("        function renderEvents() {");
            html.AppendLine("            if (reportData.events.length === 0) return;");
            html.AppendLine("            ");
            html.AppendLine("            const container = document.getElementById('eventsTimeline');");
            html.AppendLine("            let html = '<h2>Process Events Timeline</h2>';");
            html.AppendLine("            ");
            html.AppendLine("            reportData.events.forEach(evt => {");
            html.AppendLine("                const cssClass = evt.eventType === 'stopped' ? 'event stopped' : 'event';");
            html.AppendLine("                const icon = evt.eventType === 'started' ? '▶' : '■';");
            html.AppendLine("                ");
            html.AppendLine("                html += '<div class=\"' + cssClass + '\">';");
            html.AppendLine("                html += '<strong>' + escapeHtml(evt.timestamp) + '</strong> ' + icon + ' ';");
            html.AppendLine("                ");
            html.AppendLine("                if (evt.commandLine) {");
            html.AppendLine("                    html += evt.processName + ' <span class=\"pid-info\" onclick=\"copyCommandLine(' + evt.pid + ')\" onmousemove=\"showCommandTooltip(' + evt.pid + ', event)\" onmouseleave=\"hideCommandTooltip()\" title=\"Hover to view, click to copy\">PID:' + evt.pid + '</span> ' + evt.eventType;");
            html.AppendLine("                } else {");
            html.AppendLine("                    html += evt.processName + ' PID:' + evt.pid + ' ' + evt.eventType;");
            html.AppendLine("                }");
            html.AppendLine("                ");
            html.AppendLine("                if (evt.account && evt.account !== 'unknown') {");
            html.AppendLine("                    html += ' - Account: ' + escapeHtml(evt.account);");
            html.AppendLine("                }");
            html.AppendLine("                ");
            html.AppendLine("                html += '</div>';");
            html.AppendLine("            });");
            html.AppendLine("            ");
            html.AppendLine("            container.innerHTML = html;");
            html.AppendLine("        }");
            html.AppendLine();

            html.AppendLine("        function render() {");
            html.AppendLine("            document.querySelector('.loading').style.display = 'none';");
            html.AppendLine("            renderSessionInfo();");
            html.AppendLine("            renderChart('zennoChart', 'ZennoPoster Memory Usage', reportData.zennoPoster);");
            html.AppendLine("            renderChart('zbe1Chart', 'zbe1 Memory Usage (All Processes)', reportData.zbe1);");
            html.AppendLine("            renderEvents();");
            html.AppendLine("        }");
            html.AppendLine();

            html.AppendLine("        // Render on page load");
            html.AppendLine("        window.addEventListener('DOMContentLoaded', render);");
            html.AppendLine("    </script>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        /// <summary>
        /// Get chart color by index
        /// </summary>
        private string GetChartColor(int index)
        {
            var colors = new[]
            {
                "#4ec9b0", "#ce9178", "#c586c0", "#9cdcfe", "#4fc1ff",
                "#f48771", "#b5cea8", "#d4d4d4", "#569cd6", "#dcdcaa"
            };
            return colors[index % colors.Length];
        }

        /// <summary>
        /// Get path to last report
        /// </summary>
        public string GetLastReportPath()
        {
            if (File.Exists(currentReportPath))
            {
                return currentReportPath;
            }

            // Find latest timestamped report
            var reports = Directory.GetFiles(reportsDirectory, "report_*.html");
            if (reports.Length > 0)
            {
                return reports.OrderByDescending(f => File.GetLastWriteTime(f)).First();
            }

            return null;
        }

        /// <summary>
        /// Static method to get last report path without monitor instance
        /// </summary>
        public static string GetLastReportPathStatic()
        {
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var reportsDirectory = Path.Combine(exeDir, "reports");

                if (!Directory.Exists(reportsDirectory))
                    return null;

                var currentReportPath = Path.Combine(reportsDirectory, "current_report.html");

                if (File.Exists(currentReportPath))
                {
                    return currentReportPath;
                }

                // Find latest timestamped report
                var reports = Directory.GetFiles(reportsDirectory, "report_*.html");
                if (reports.Length > 0)
                {
                    return reports.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                }
            }
            catch { }

            return null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
