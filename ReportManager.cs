using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using z3nCore; // Твои Db и Sql

namespace OtpTrayApp
{
    public class ReportManager
    {
        private readonly string _reportsFolder;
        private readonly Sql _sql;
        private readonly string _dbMode;

        
        public ReportManager(Sql sql, string dbMode)
        {
            _sql = sql;
            _dbMode = dbMode;
            _reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".reports");
        }

        public async Task GenerateAsync()
        {
            // Вариант А: Автоматический поиск таблиц через твой Sql.DbReadAsync
            string queryTables = _dbMode == "PostgreSQL" 
                ? "SELECT table_name FROM information_schema.tables WHERE table_name LIKE '__%' AND table_schema = 'public';"
                : "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE '__%';";

            string rawTables = await _sql.DbReadAsync(queryTables, "¦", "·");
            if (string.IsNullOrEmpty(rawTables)) return;

            var tableNames = rawTables.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
            var projectList = new List<object>();

            foreach (var table in tableNames)
            {
                var data = await CollectProjectData(table.Trim());
                if (data != null) projectList.Add(data);
            }

            SaveAll(projectList);
        }

        private async Task<object> CollectProjectData(string tableName)
        {
            // Логика из твоего JsonReportGenerator.CollectData
            string query = $"SELECT id, last FROM \"{tableName}\" WHERE last LIKE '+ %' OR last LIKE '- %';";
            string rawData = await _sql.DbReadAsync(query, "¦", "·");

            if (string.IsNullOrEmpty(rawData)) return null;

            var accounts = new Dictionary<string, object>();
            var rows = rawData.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var row in rows)
            {
                var cols = row.Split('¦');
                if (cols.Length < 2) continue;

                string id = cols[0].Trim();
                string last = cols[1].Trim();

                var lines = last.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) continue;

                var meta = lines[0].Split(' ');

                accounts[id] = new
                {
                    status = meta[0],
                    timestamp = meta.Length > 1 ? meta[1] : "",
                    completionSec = meta.Length > 2 ? meta[2] : "0",
                    report = lines.Length > 1 ? string.Join("\n", lines.Skip(1)).Trim() : ""
                };
            }

            return new
            {
                name = tableName.Replace("__", ""),
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                accounts = accounts
            };
        }

        private void SaveAll(List<object> projects)
        {
            if (!Directory.Exists(_reportsFolder)) Directory.CreateDirectory(_reportsFolder);
            string projectsDir = Path.Combine(_reportsFolder, "projects");
            if (!Directory.Exists(projectsDir)) Directory.CreateDirectory(projectsDir);

            foreach (dynamic p in projects)
            {
                string json = JsonConvert.SerializeObject(p, Formatting.Indented);
                string cleanName = p.name.Replace(" ", "_").Replace("-", "_");
                File.WriteAllText(Path.Combine(projectsDir, $"{p.name}.js"), $"window.project_{cleanName} = {json};", Encoding.UTF8);
            }

            // metadata.js для reportLoader.js
            var meta = new
            {
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                projects = projects.Select(p => ((dynamic)p).name).ToList()
            };
            File.WriteAllText(Path.Combine(_reportsFolder, "metadata.js"), $"window.reportMetadata = {JsonConvert.SerializeObject(meta)};", Encoding.UTF8);
        }
    }
}