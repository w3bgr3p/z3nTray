using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OtpTrayApp;

public class EmbeddedServer
{
    private HttpListener _listener;
    private readonly AppSettings _settings;
    private bool _isRunning;
    private readonly string _logPath;
    private const int DefaultPort = 10993;
    private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

    public EmbeddedServer(AppSettings settings)
    {
        _settings = settings;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{DefaultPort}/"); 
        
        _logPath = Path.Combine(AppContext.BaseDirectory, "logs");
        if (!Directory.Exists(_logPath)) Directory.CreateDirectory(_logPath);
    }

    public void Start()
    {
        try {
            _isRunning = true;
            _listener.Start();
            Task.Run(Listen);
        } catch (HttpListenerException ex) when (ex.ErrorCode == 5) {
            System.Windows.Forms.MessageBox.Show(
                "Ошибка доступа! Запустите CMD от админа и выполните:\n" +
                $"netsh http add urlacl url=http://*:10993/ user=Everyone",
                "Нужны права доступа", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
        } catch (Exception ex) {
            System.Windows.Forms.MessageBox.Show($"Ошибка сервера: {ex.Message}");
        }
    }

    private async Task Listen()
    {
        while (_isRunning)
        {
            try {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequest(context)); 
            } catch { if (!_isRunning) break; }
        }
    }

    private async Task ProcessRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        
        // CORS для дашборда
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS") {
            response.StatusCode = 204;
            response.Close();
            return;
        }

        try {
            string path = request.Url?.AbsolutePath.ToLower() ?? "";

            // 1. ПРИЕМ ЛОГОВ
            if (request.HttpMethod == "POST" && path == "/log") {
                using var reader = new StreamReader(request.InputStream);
                var json = await reader.ReadToEndAsync();
                await SaveLog(json);
                response.StatusCode = 200;
            }
            // 2. ВЫДАЧА ЛОГОВ (ДЛЯ ДАШБОРДА)
            else if (request.HttpMethod == "GET" && path == "/logs") {
                var query = request.QueryString;
                int limit = int.TryParse(query["limit"], out var l) ? l : 100;
                var logs = await ReadLogs(limit, query["level"], query["machine"], query["project"], query["session"], query["port"], query["pid"]);
                await WriteJsonResponse(response, logs);
            }
            // 3. СТАТИСТИКА
            else if (request.HttpMethod == "GET" && path == "/stats") {
                var stats = await GetStats();
                await WriteJsonResponse(response, stats);
            }
            // 4. ДАШБОРД (HTML)
            else if (path == "/" || path == "/index.html") {
                await ServeDashboard(response);
            }
            // 5. ОЧИСТКА ЛОГОВ
            else if (path == "/clear" && request.HttpMethod == "POST")
            {
                await _fileLock.WaitAsync();
                try 
                {
                    string currentLog = Path.Combine(_logPath, "current.jsonl"); 
        
                    if (File.Exists(currentLog)) {
                        File.WriteAllText(currentLog, string.Empty);
                    }

                    var oldLogs = Directory.GetFiles(_logPath, "log_*.jsonl");
                    foreach (var oldFile in oldLogs) {
                        File.Delete(oldFile);
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes("OK");
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    response.StatusCode = 500;
                    byte[] error = Encoding.UTF8.GetBytes($"Ошибка удаления: {ex.Message}");
                    await response.OutputStream.WriteAsync(error, 0, error.Length);
                }
                finally 
                {
                    _fileLock.Release();
                    response.Close();
                }
                return;
            }
            else if (path == "/report") {
                string reportPath = ResourceMonitor.GetLastReportPathStatic();
    
                if (!string.IsNullOrEmpty(reportPath) && File.Exists(reportPath)) {
                    byte[] buffer = await File.ReadAllBytesAsync(reportPath);
                    response.ContentType = "text/html; charset=utf-8";
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                } else {
                    byte[] noReport = Encoding.UTF8.GetBytes("<h1>Отчет еще не сформирован</h1><p>Проверьте настройки мониторинга.</p>");
                    response.StatusCode = 404;
                    await response.OutputStream.WriteAsync(noReport, 0, noReport.Length);
                }
            }
            else {
                response.StatusCode = 404;
            }
        }
        catch (Exception ex) {
            response.StatusCode = 500;
            byte[] error = Encoding.UTF8.GetBytes(ex.Message);
            response.OutputStream.Write(error, 0, error.Length);
        }
        finally { response.Close(); }
    }

    private async Task SaveLog(string json) {
        await _fileLock.WaitAsync();
        try {
            string filePath = Path.Combine(_logPath, "current.jsonl");
            
            if (File.Exists(filePath) && new FileInfo(filePath).Length > 100 * 1024 * 1024) {
                File.Move(filePath, Path.Combine(_logPath, $"log_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl"));
            }

            await File.AppendAllTextAsync(filePath, json + Environment.NewLine);
        } finally { _fileLock.Release(); }
    }

    private async Task<List<object>> ReadLogs(int limit, string? level, string? machine, string? project, string? session, string? port, string? pid) {
        var result = new List<object>();
        var files = Directory.GetFiles(_logPath, "*.jsonl")
            .OrderByDescending(File.GetCreationTime)
            .Take(5);

        foreach (var file in files) {
            var lines = (await File.ReadAllLinesAsync(file)).Reverse();
            foreach (var line in lines) {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try {
                    var log = JsonSerializer.Deserialize<JsonElement>(line);
                    
                    // Фильтрация
                    if (!string.IsNullOrEmpty(level) && !log.GetProperty("level").ToString().Equals(level, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrEmpty(machine) && !log.GetProperty("machine").ToString().Contains(machine, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrEmpty(project) && !log.GetProperty("project").ToString().Contains(project, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrEmpty(session)) {
                        if (!log.TryGetProperty("session", out var sessionProp) || 
                            !sessionProp.ToString().Contains(session, StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    if (!string.IsNullOrEmpty(port)) {
                        if (!log.TryGetProperty("port", out var portProp) || 
                            !portProp.ToString().Contains(port, StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    if (!string.IsNullOrEmpty(pid)) {
                        if (!log.TryGetProperty("pid", out var pidProp) || 
                            !pidProp.ToString().Contains(pid, StringComparison.OrdinalIgnoreCase)) continue;
                    }

                    result.Add(log);
                    if (result.Count >= limit) return result;
                } catch { continue; }
            }
        }
        return result;
    }

    private async Task<object> GetStats()
    {
        var logs = await ReadLogs(2000, null, null, null, null, null, null); 
    
        var total = logs.Count;
        var levels = new Dictionary<string, int>();
        var machines = new Dictionary<string, int>();
        var projects = new Dictionary<string, int>();
        var sessions = new Dictionary<string, int>();
        var ports = new Dictionary<string, int>();
        var pids = new Dictionary<string, int>();

        foreach (JsonElement log in logs)
        {
            try {
                string lvl = log.TryGetProperty("level", out var l) ? l.ToString() : "UNKNOWN";
                string mch = log.TryGetProperty("machine", out var m) ? m.ToString() : "UNKNOWN";
                string prj = log.TryGetProperty("project", out var p) ? p.ToString() : "UNKNOWN";
                string sess = log.TryGetProperty("session", out var s) ? s.ToString() : "0";
                string prt = log.TryGetProperty("port", out var pt) ? pt.ToString() : "UNKNOWN";
                string pd = log.TryGetProperty("pid", out var pi) ? pi.ToString() : "UNKNOWN";

                levels[lvl] = levels.GetValueOrDefault(lvl) + 1;
                machines[mch] = machines.GetValueOrDefault(mch) + 1;
                projects[prj] = projects.GetValueOrDefault(prj) + 1;
                sessions[sess] = sessions.GetValueOrDefault(sess) + 1;
                ports[prt] = ports.GetValueOrDefault(prt) + 1;
                pids[pd] = pids.GetValueOrDefault(pd) + 1;
            } catch { continue; }
        }

        return new {
            totalLogs = total,
            byLevel = levels,
            byMachine = machines,
            byProject = projects,
            bySession = sessions,
            byPort = ports,
            byPid = pids
        };
    }

    private async Task ServeDashboard(HttpListenerResponse response) {
        string dashPath = Path.Combine(AppContext.BaseDirectory, "dashboard.html");
        if (File.Exists(dashPath)) {
            byte[] buffer = await File.ReadAllBytesAsync(dashPath);
            response.ContentType = "text/html; charset=utf-8";
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
    }

    private async Task WriteJsonResponse(HttpListenerResponse response, object data) {
        response.ContentType = "application/json";
        byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    public void Stop() {
        _isRunning = false;
        if (_listener.IsListening) _listener.Stop();
    }
}