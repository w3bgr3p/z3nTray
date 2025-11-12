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
        private bool isRunning = false;

        public ResourceMonitor()
        {
            // Create reports directory next to executable
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(exePath);
            reportsDirectory = Path.Combine(exeDir, "reports");

            if (!Directory.Exists(reportsDirectory))
            {
                Directory.CreateDirectory(reportsDirectory);
            }

            currentReportPath = Path.Combine(reportsDirectory, "current_report.html");
        }

        /// <summary>
        /// Start monitoring
        /// </summary>
        public void Start()
        {
            lock (lockObj)
            {
                if (isRunning) return;

                currentSession = new MonitoringSession
                {
                    StartTime = DateTime.Now
                };

                // Collect initial snapshot
                CollectMetrics(null);

                // Start timer for 1-minute intervals
                collectionTimer = new System.Threading.Timer(CollectMetrics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
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
        /// Collect metrics from all processes
        /// </summary>
        private void CollectMetrics(object state)
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
                GenerateHtmlReport(currentSession, currentReportPath);
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
            GenerateHtmlReport(session, reportPath);
        }

        /// <summary>
        /// Generate HTML report with charts
        /// </summary>
        private void GenerateHtmlReport(MonitoringSession session, string outputPath)
        {
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset='utf-8'>");
            html.AppendLine("    <title>Resource Usage Report</title>");
            html.AppendLine("    <script src='https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js'></script>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Consolas, sans-serif; margin: 20px; background: #1e1e1e; color: #fff; }");
            html.AppendLine("        h1, h2 { color: #4ec9b0; }");
            html.AppendLine("        .chart-container { margin: 30px 0; background: #2d2d30; padding: 20px; border-radius: 5px; }");
            html.AppendLine("        .info { background: #252526; padding: 15px; border-radius: 5px; margin: 20px 0; }");
            html.AppendLine("        .event { padding: 5px; margin: 2px 0; border-left: 3px solid #4ec9b0; background: #2d2d30; }");
            html.AppendLine("        .event.stopped { border-left-color: #f48771; }");
            html.AppendLine("        canvas { max-height: 400px; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            // Header
            html.AppendLine($"    <h1>Resource Usage Report</h1>");
            html.AppendLine($"    <div class='info'>");
            html.AppendLine($"        <p><strong>Session Start:</strong> {session.StartTime:yyyy-MM-dd HH:mm:ss}</p>");
            if (session.EndTime.HasValue)
            {
                html.AppendLine($"        <p><strong>Session End:</strong> {session.EndTime:yyyy-MM-dd HH:mm:ss}</p>");
                html.AppendLine($"        <p><strong>Duration:</strong> {(session.EndTime.Value - session.StartTime).TotalMinutes:F1} minutes</p>");
                html.AppendLine($"        <p><strong>End Reason:</strong> {session.EndReason}</p>");
            }
            else
            {
                html.AppendLine($"        <p><strong>Status:</strong> Active (auto-updating)</p>");
            }
            html.AppendLine($"    </div>");

            // ZennoPoster Memory Chart
            GenerateZennoPosterChart(html, session);

            // zbe1 Charts by Account
            GenerateZbe1Charts(html, session);

            // Events Timeline
            GenerateEventsTimeline(html, session);

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            File.WriteAllText(outputPath, html.ToString());
        }

        /// <summary>
        /// Generate ZennoPoster memory chart
        /// </summary>
        private void GenerateZennoPosterChart(StringBuilder html, MonitoringSession session)
        {
            var zennoSnapshots = session.Snapshots
                .Where(s => s.ProcessName == "ZennoPoster")
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (zennoSnapshots.Count == 0) return;

            // Group by PID
            var byPid = zennoSnapshots.GroupBy(s => s.Pid).ToList();

            html.AppendLine("    <div class='chart-container'>");
            html.AppendLine("        <h2>ZennoPoster Memory Usage</h2>");
            html.AppendLine("        <canvas id='zennoChart'></canvas>");
            html.AppendLine("    </div>");

            html.AppendLine("    <script>");
            html.AppendLine("        var zennoCtx = document.getElementById('zennoChart').getContext('2d');");
            html.AppendLine("        var zennoChart = new Chart(zennoCtx, {");
            html.AppendLine("            type: 'line',");
            html.AppendLine("            data: {");

            // Labels (timestamps)
            var timestamps = zennoSnapshots.Select(s => s.Timestamp).Distinct().OrderBy(t => t).ToList();
            html.AppendLine($"                labels: [{string.Join(",", timestamps.Select(t => $"'{t:HH:mm}'"))}],");

            html.AppendLine("                datasets: [");

            int colorIndex = 0;
            foreach (var group in byPid)
            {
                var pid = group.Key;
                var color = GetChartColor(colorIndex++);

                html.AppendLine("                    {");
                html.AppendLine($"                        label: 'ZennoPoster PID:{pid}',");
                html.AppendLine($"                        data: [{string.Join(",", timestamps.Select(t => group.FirstOrDefault(s => s.Timestamp == t)?.MemoryMB ?? 0))}],");
                html.AppendLine($"                        borderColor: '{color}',");
                html.AppendLine($"                        backgroundColor: '{color}33',");
                html.AppendLine("                        tension: 0.1");
                html.AppendLine("                    },");
            }

            html.AppendLine("                ]");
            html.AppendLine("            },");
            html.AppendLine("            options: {");
            html.AppendLine("                responsive: true,");
            html.AppendLine("                plugins: { legend: { labels: { color: '#fff' } } },");
            html.AppendLine("                scales: {");
            html.AppendLine("                    y: { beginAtZero: true, title: { display: true, text: 'Memory (MB)', color: '#fff' }, ticks: { color: '#fff' } },");
            html.AppendLine("                    x: { ticks: { color: '#fff' } }");
            html.AppendLine("                }");
            html.AppendLine("            }");
            html.AppendLine("        });");
            html.AppendLine("    </script>");
        }

        /// <summary>
        /// Generate zbe1 charts grouped by account
        /// </summary>
        private void GenerateZbe1Charts(StringBuilder html, MonitoringSession session)
        {
            var zbe1Snapshots = session.Snapshots
                .Where(s => s.ProcessName == "zbe1")
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (zbe1Snapshots.Count == 0) return;

            // Group by account
            var byAccount = zbe1Snapshots.GroupBy(s => s.Account ?? "unknown").ToList();

            foreach (var accountGroup in byAccount)
            {
                var account = accountGroup.Key;
                var chartId = $"zbe1Chart_{account.Replace(" ", "_").Replace("\"", "")}";

                html.AppendLine("    <div class='chart-container'>");
                html.AppendLine($"        <h2>zbe1 Memory Usage - Account: {account}</h2>");
                html.AppendLine($"        <canvas id='{chartId}'></canvas>");
                html.AppendLine("    </div>");

                html.AppendLine("    <script>");
                html.AppendLine($"        var ctx_{account.Replace(" ", "_").Replace("\"", "")} = document.getElementById('{chartId}').getContext('2d');");
                html.AppendLine($"        new Chart(ctx_{account.Replace(" ", "_").Replace("\"", "")}, {{");
                html.AppendLine("            type: 'line',");
                html.AppendLine("            data: {");

                var timestamps = accountGroup.Select(s => s.Timestamp).Distinct().OrderBy(t => t).ToList();
                html.AppendLine($"                labels: [{string.Join(",", timestamps.Select(t => $"'{t:HH:mm}'"))}],");

                html.AppendLine("                datasets: [");

                // Group by PID within account
                var byPid = accountGroup.GroupBy(s => s.Pid).ToList();
                int colorIndex = 0;
                foreach (var pidGroup in byPid)
                {
                    var pid = pidGroup.Key;
                    var color = GetChartColor(colorIndex++);

                    html.AppendLine("                    {");
                    html.AppendLine($"                        label: 'PID:{pid}',");
                    html.AppendLine($"                        data: [{string.Join(",", timestamps.Select(t => pidGroup.FirstOrDefault(s => s.Timestamp == t)?.MemoryMB ?? 0))}],");
                    html.AppendLine($"                        borderColor: '{color}',");
                    html.AppendLine($"                        backgroundColor: '{color}33',");
                    html.AppendLine("                        tension: 0.1");
                    html.AppendLine("                    },");
                }

                html.AppendLine("                ]");
                html.AppendLine("            },");
                html.AppendLine("            options: {");
                html.AppendLine("                responsive: true,");
                html.AppendLine("                plugins: { legend: { labels: { color: '#fff' } } },");
                html.AppendLine("                scales: {");
                html.AppendLine("                    y: { beginAtZero: true, title: { display: true, text: 'Memory (MB)', color: '#fff' }, ticks: { color: '#fff' } },");
                html.AppendLine("                    x: { ticks: { color: '#fff' } }");
                html.AppendLine("                }");
                html.AppendLine("            }");
                html.AppendLine("        });");
                html.AppendLine("    </script>");
            }
        }

        /// <summary>
        /// Generate events timeline
        /// </summary>
        private void GenerateEventsTimeline(StringBuilder html, MonitoringSession session)
        {
            html.AppendLine("    <div class='chart-container'>");
            html.AppendLine("        <h2>Process Events Timeline</h2>");

            foreach (var evt in session.Events.OrderBy(e => e.Timestamp))
            {
                var cssClass = evt.EventType == "stopped" ? "event stopped" : "event";
                var icon = evt.EventType == "started" ? "▶" : "■";

                html.AppendLine($"        <div class='{cssClass}'>");
                html.AppendLine($"            <strong>{evt.Timestamp:HH:mm:ss}</strong> {icon} ");
                html.AppendLine($"            {evt.ProcessName} PID:{evt.Pid} {evt.EventType}");

                if (!string.IsNullOrEmpty(evt.Account) && evt.Account != "unknown")
                {
                    html.AppendLine($"            - Account: {evt.Account}");
                }

                if (!string.IsNullOrEmpty(evt.CommandLine) && evt.CommandLine.Length < 200)
                {
                    html.AppendLine($"            <br><small>{System.Security.SecurityElement.Escape(evt.CommandLine)}</small>");
                }

                html.AppendLine("        </div>");
            }

            html.AppendLine("    </div>");
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
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var exeDir = Path.GetDirectoryName(exePath);
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
