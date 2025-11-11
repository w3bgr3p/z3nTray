using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace OtpTrayApp
{
    /// <summary>
    /// Менеджер процессов ZennoPoster - standalone версия
    /// Работает только со стандартными Process API
    /// </summary>
    public static class ProcessManager
    {
        #region Process Statistics

        public class ProcessStats
        {
            public List<string> All { get; set; } = new List<string>();
            public List<string> LimitedByTime { get; set; } = new List<string>();
            public List<string> LimitedByMem { get; set; } = new List<string>();
            public List<string> WithBrowser { get; set; } = new List<string>();
            public List<string> NoBrowser { get; set; } = new List<string>();
            public Dictionary<int, ProcessInfo> ProcessInfos { get; set; } = new Dictionary<int, ProcessInfo>();
        }

        public class ProcessInfo
        {
            public int Pid { get; set; }
            public int Mem { get; set; }
            public int Age { get; set; }
            public string Acc { get; set; }
        }

        /// <summary>
        /// Получить статистику по всем процессам zbe1
        /// </summary>
        public static ProcessStats GetProcessStats(AppSettings settings)
        {
            var stats = new ProcessStats();
            var processes = GetZbe1Processes();

            foreach (var proc in processes)
            {
                try
                {
                    var memMB = (int)(proc.WorkingSet64 / (1024 * 1024));
                    var ageMin = (int)(DateTime.Now - proc.StartTime).TotalMinutes;
                    
                    string acc;
                    if (settings.ShowRawCommandLine)
                    {
                        // Показываем полную командную строку
                        acc = GetProcessCommandLine(proc.Id) ?? "unknown";
                    }
                    else
                    {
                        // Извлекаем только acc из --user-data-dir
                        acc = GetAccFromCommandLine(proc) ?? "unknown";
                    }
                    
                    var procInfo = new ProcessInfo
                    {
                        Pid = proc.Id,
                        Mem = memMB,
                        Age = ageMin,
                        Acc = acc
                    };

                    string info = $"pid: {proc.Id}, age: {ageMin}Min, mem: {memMB}Mb, arg: {procInfo.Acc}";
                    stats.All.Add(info);

                    // Категоризация
                    if (memMB > settings.MaxMemoryForInstance)
                        stats.LimitedByMem.Add(info);
                    
                    if (ageMin > settings.MaxAgeForInstance)
                        stats.LimitedByTime.Add(info);

                    if (string.IsNullOrEmpty(acc) || acc == "unknown")
                    {
                        stats.NoBrowser.Add(info);
                    }
                    else
                    {
                        stats.WithBrowser.Add(info);
                        stats.ProcessInfos.Add(proc.Id, procInfo);
                    }
                }
                catch { }
            }
            
            // Добавляем главные процессы ZennoPoster
            var zennoProcesses = Process.GetProcessesByName("ZennoPoster");
            foreach (var proc in zennoProcesses)
            {
                try
                {
                    var memMB = (int)(proc.WorkingSet64 / (1024 * 1024));
                    var ageMin = (int)(DateTime.Now - proc.StartTime).TotalMinutes;
            
                    string info = $"pid: {proc.Id}, type: ZennoPoster, age: {ageMin}Min, mem: {memMB}Mb";
                    stats.All.Add(info);
                    stats.NoBrowser.Add(info);
                }
                catch { }
            }

            return stats;
        }

        #endregion

        #region Process Killer

        public class KillResult
        {
            public int KilledByTime { get; set; }
            public int KilledByMem { get; set; }
            public int KilledMain { get; set; }
            public List<string> Messages { get; set; } = new List<string>();
        }

        /// <summary>
        /// Убить процессы по критериям
        /// </summary>
        public static KillResult KillProcesses(AppSettings settings)
        {
            var result = new KillResult();

            try
            {
                var browserProcesses = GetZbe1Processes();
                result.Messages.Add($"Найдено процессов браузера: {browserProcesses.Count}");

                var killByTime = new List<int>();
                var killByMem = new List<int>();

                foreach (var proc in browserProcesses)
                {
                    try
                    {
                        var memMB = proc.WorkingSet64 / (1024 * 1024);
                        var ageMin = (int)(DateTime.Now - proc.StartTime).TotalMinutes;

                        bool isHeavy = memMB > settings.MaxMemoryForInstance;
                        bool isOld = ageMin > settings.MaxAgeForInstance;

                        if (isHeavy && settings.KillHeavy)
                            killByMem.Add(proc.Id);

                        if (isOld && settings.KillOld)
                            killByTime.Add(proc.Id);
                    }
                    catch { }
                }

                // Kill old
                if (settings.KillOld && killByTime.Count > 0)
                {
                    result.Messages.Add($"\nУбиваем {killByTime.Count} старых процессов...");
                    foreach (int pid in killByTime)
                    {
                        try
                        {
                            Process.GetProcessById(pid).Kill();
                            result.KilledByTime++;
                            result.Messages.Add($"✓ Убит PID: {pid}");
                        }
                        catch (Exception ex)
                        {
                            result.Messages.Add($"✗ Не удалось убить PID: {pid} - {ex.Message}");
                        }
                    }
                }

                // Kill heavy
                if (settings.KillHeavy && killByMem.Count > 0)
                {
                    result.Messages.Add($"\nУбиваем {killByMem.Count} тяжелых процессов...");
                    foreach (int pid in killByMem)
                    {
                        try
                        {
                            Process.GetProcessById(pid).Kill();
                            result.KilledByMem++;
                            result.Messages.Add($"✓ Убит PID: {pid}");
                        }
                        catch (Exception ex)
                        {
                            result.Messages.Add($"✗ Не удалось убить PID: {pid} - {ex.Message}");
                        }
                    }
                }

                // Kill main ZennoPoster
                if (settings.KillMain)
                {
                    var zennoProcs = Process.GetProcessesByName("ZennoPoster");
                    var killZP = new List<Process>();

                    foreach (var proc in zennoProcs)
                    {
                        try
                        {
                            var memMB = proc.WorkingSet64 / (1024 * 1024);
                            result.Messages.Add($"ZennoPoster PID:{proc.Id} использует {memMB}MB (лимит: {settings.MaxMemoryForZennoposter}MB)");
                            
                            if (memMB > settings.MaxMemoryForZennoposter)
                            {
                                killZP.Add(proc);
                                result.Messages.Add($"  → Превышен лимит! Будет убит.");
                            }
                            else
                            {
                                result.Messages.Add($"  → В пределах нормы.");
                            }
                        }
                        catch { }
                    }

                    if (killZP.Count > 0)
                    {
                        result.Messages.Add($"\n⚠ ОПАСНО: Убиваем {killZP.Count} главный процесс ZennoPoster...");
                        foreach (var proc in killZP)
                        {
                            try
                            {
                                var pid = proc.Id;
                                var memMB = proc.WorkingSet64 / (1024 * 1024);
                                
                                proc.Kill();
                                result.Messages.Add($"☠ Harakiri PID: {pid} (было {memMB}MB)");
                                
                                // Ждем завершения процесса
                                result.Messages.Add($"⏳ Ожидаем завершения процесса {pid}...");
                                proc.WaitForExit(10000); // максимум 10 секунд
                                
                                // Проверяем и восстанавливаем Tasks.dat если он пустой
                                RestoreTasksIfEmpty(result);
                                
                                result.KilledMain++;
                            }
                            catch (Exception ex)
                            {
                                result.Messages.Add($"✗ Не удалось убить PID: {proc.Id} - {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        result.Messages.Add("\n✓ Главный процесс ZennoPoster в норме, не убиваем.");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Messages.Add($"\nКритическая ошибка: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Получить все процессы zbe1
        /// </summary>
        private static List<Process> GetZbe1Processes()
        {
            var processes = new List<Process>();
            try
            {
                processes.AddRange(Process.GetProcessesByName("zbe1"));
            }
            catch { }
            return processes;
        }

        /// <summary>
        /// Получить аккаунт из командной строки процесса
        /// </summary>
        private static string GetAccFromCommandLine(Process proc)
        {
            try
            {
                var cmdLine = GetProcessCommandLine(proc.Id);
                if (string.IsNullOrEmpty(cmdLine))
                    return null;

                // Ищем --user-data-dir="path"
                var match = Regex.Match(cmdLine, @"--user-data-dir=""([^""]+)""");
                if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
                {
                    var path = match.Groups[1].Value.Trim('\\');
                    return Path.GetFileName(path);
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Получить командную строку процесса через WMI
        /// </summary>
        private static string GetProcessCommandLine(int pid)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}"))
                {
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        var cmdLine = obj["CommandLine"];
                        return cmdLine?.ToString();
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Восстановить Tasks.dat из Tasks.1.dat если он пустой
        /// </summary>
        private static void RestoreTasksIfEmpty(KillResult result)
        {
            try
            {
                // Путь к папке ZennoPoster
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var zennoFolder = Path.Combine(appData, "ZennoLab", "ZennoPoster", "7", "ZennoPoster");
                var tasksFile = Path.Combine(zennoFolder, "Tasks.dat");
                var backupFile = Path.Combine(zennoFolder, "Tasks.1.dat");

                result.Messages.Add($"🔍 Проверяем Tasks.dat...");

                // Проверяем существование файлов
                if (!File.Exists(tasksFile))
                {
                    result.Messages.Add($"⚠ Tasks.dat не найден: {tasksFile}");
                    return;
                }

                if (!File.Exists(backupFile))
                {
                    result.Messages.Add($"⚠ Tasks.1.dat не найден: {backupFile}");
                    return;
                }

                // Проверяем размер Tasks.dat
                var fileInfo = new FileInfo(tasksFile);
                result.Messages.Add($"📊 Размер Tasks.dat: {fileInfo.Length} байт");

                if (fileInfo.Length == 0)
                {
                    result.Messages.Add($"⚠ Tasks.dat ПУСТОЙ! Восстанавливаем из Tasks.1.dat...");

                    // Проверяем размер бэкапа
                    var backupInfo = new FileInfo(backupFile);
                    result.Messages.Add($"📊 Размер Tasks.1.dat: {backupInfo.Length} байт");

                    if (backupInfo.Length == 0)
                    {
                        result.Messages.Add($"❌ Tasks.1.dat тоже пустой! Восстановление невозможно.");
                        return;
                    }

                    // Удаляем пустой Tasks.dat
                    File.Delete(tasksFile);
                    result.Messages.Add($"🗑 Удален пустой Tasks.dat");

                    // Копируем Tasks.1.dat → Tasks.dat
                    File.Copy(backupFile, tasksFile);
                    result.Messages.Add($"✓ Tasks.dat восстановлен из резервной копии ({backupInfo.Length} байт)");
                }
                else
                {
                    result.Messages.Add($"✓ Tasks.dat в норме, восстановление не требуется.");
                }
            }
            catch (Exception ex)
            {
                result.Messages.Add($"❌ Ошибка восстановления Tasks.dat: {ex.Message}");
            }
        }

        #endregion
    }
}