# OTP Tray App - Менеджер Процессов

> Tray хелпер для генерации OTP кодов и управления процессами ZennoPoster


[English version](README.md)

## Возможности

### 🔐 Генератор OTP
- Генерация TOTP кодов из секретных ключей
- Автоматическое копирование в буфер обмена
- Умная обработка таймаута (регенерация если осталось <5с)
- Интеграция в системный трей

### ⚙️ Менеджер Процессов
- Мониторинг процессов ZennoPoster в реальном времени
- Отслеживание памяти и времени работы
- Автоматическое завершение процессов по критериям
- Определение привязки аккаунтов через `--user-data-dir`
- Автовосстановление поврежденного файла `Tasks.dat`
- Режим отображения полной командной строки

## Установка

### Требования
- Windows 7 или выше
- .NET Framework 4.7.2+
- Права администратора (для WMI инспекции процессов)

### NuGet зависимости
```bash
Install-Package System.Management
Install-Package OtpNet
```

### Сборка из исходников
```bash
git clone https://github.com/yourusername/OtpTrayApp.git
cd OtpTrayApp
# Открыть в Visual Studio или Rider
# Build → Build Solution
```

## Использование

### Управление через трей

**ЛКМ** - Открыть генератор OTP
**ПКМ** - Контекстное меню:
- Генерировать OTP
- Показать процессы - окно статистики
- Check & Kill Now - выполнить киллер с текущими настройками
- Настройки - конфигурация параметров
- Выход

### Окно статистики процессов

Отображает категоризированный список процессов:
- ⏰ **По времени** - процессы превысившие лимит возраста
- 💾 **По памяти** - процессы превысившие лимит памяти
- 🌐 **С браузером** - процессы с привязкой аккаунта
- ⚠ **Не привязаны** - непривязанные процессы + главный ZennoPoster

Функции:
- Автообновление каждые 5 секунд
- Кнопка ручного обновления
- Кнопка убийства с подтверждением
- Цветовая индикация

### Конфигурация

Настройки хранятся в `App.config`:

```xml
<!-- Процессы браузера (zbe1) -->
<add key="MaxMemoryForInstance" value="1000" />      <!-- МБ -->
<add key="MaxAgeForInstance" value="30" />            <!-- минуты -->

<!-- Главный процесс ZennoPoster -->
<add key="MaxMemoryForZennoposter" value="20000" />  <!-- МБ -->

<!-- Флаги завершения -->
<add key="KillOld" value="True" />
<add key="KillHeavy" value="True" />
<add key="KillMain" value="False" />                  <!-- ОПАСНО! -->

<!-- Автоматизация -->
<add key="AutoCheckInterval" value="0" />             <!-- минуты, 0 = выкл -->

<!-- Мониторинг ресурсов -->
<add key="EnableResourceMonitoring" value="False" />           <!-- включить мониторинг -->
<add key="ResourceMonitoringIntervalMinutes" value="1" />      <!-- интервал сбора -->
<add key="MaxMonitoringRecordsPerFile" value="2000" />         <!-- записей до ротации -->
<add key="MonitoringReportRetentionDays" value="7" />          <!-- дней хранения отчетов -->

<!-- UI -->
<add key="ShowLogs" value="False" />                  <!-- всплывашки vs тихий режим -->
<add key="ShowRawCommandLine" value="False" />        <!-- полная командная строка -->
```

## Как это работает

### Обнаружение процессов

1. Сканирует все процессы `zbe1.exe`
2. Извлекает аккаунт из `--user-data-dir="path"` через WMI
3. Отслеживает использование памяти и время работы
4. Категоризирует по заданным лимитам

### Логика киллера

```
ЕСЛИ возраст > MaxAgeForInstance И KillOld → УБИТЬ
ЕСЛИ память > MaxMemoryForInstance И KillHeavy → УБИТЬ
ЕСЛИ память > MaxMemoryForZennoposter И KillMain → УБИТЬ ZennoPoster
```

После убийства главного процесса:
1. Ожидает завершения процесса (макс 10с)
2. Проверяет размер файла `Tasks.dat`
3. Если размер < 50 байт → восстанавливает из `Tasks.1.dat`

### Автопроверка

Если `AutoCheckInterval > 0`:
- Таймер запускается каждые X минут
- Выполняет Check & Kill автоматически
- Логирование зависит от настройки `ShowLogs`

### Мониторинг ресурсов

Если `EnableResourceMonitoring = True`:
- Собирает данные об использовании памяти процессами ZennoPoster и zbe1
- Генерирует HTML отчеты с интерактивными графиками (Chart.js)
- Отчеты сохраняются в директорию `./reports/`

**Ротация файлов**:
- Отчеты имеют имена `resource_monitor_report_YYYY-MM-DD.html`
- Автоматическая ротация ежедневно или при достижении лимита записей
- Старые отчеты удаляются после истечения срока хранения
- Каждый отчет включает временные снимки и события жизненного цикла процессов

**Настройки по умолчанию**:
- Интервал сбора: 1 минута
- Макс. записей на файл: 2000
- Период хранения: 7 дней

## Структура файлов

```
OtpTrayApp/
├── OtpTrayContext.cs          # Главное приложение в трее
├── ProcessManager.cs          # Логика процессов (без z3nCore)
├── ResourceMonitor.cs         # Система мониторинга ресурсов
├── AppSettings.cs             # Управление App.config
├── ProcessStatsForm.cs        # Окно статистики
├── SettingsForm.cs            # Диалог настроек
├── OtpInputForm.cs            # Диалог ввода OTP
├── App.config                 # Файл конфигурации
└── reports/                   # Директория отчетов мониторинга
    └── resource_monitor_report_YYYY-MM-DD.html
```

## Технические детали

### Отслеживание процессов

**Без shared memory** - прямые вызовы Process API каждый раз:
```csharp
// Получить все процессы zbe1
Process.GetProcessesByName("zbe1")

// Извлечь аккаунт из командной строки
WMI: Win32_Process.CommandLine → --user-data-dir="path"

// Получить метрики
proc.WorkingSet64 / (1024 * 1024)  // Память в МБ
DateTime.Now - proc.StartTime       // Время работы
```

### Извлечение аккаунта

```csharp
// Пример командной строки:
"C:\...\zbe1.exe" --user-data-dir="F:\accounts\profilesFolder\534\\" ...

// Извлечение через Regex:
--user-data-dir="([^"]+)"

// Результат:
Path.GetFileName() → "534"
```

### Восстановление Tasks.dat

При убийстве главного ZennoPoster:
1. `proc.Kill()` → `proc.WaitForExit(10000)`
2. Проверка файла: `%AppData%\ZennoLab\ZennoPoster\7\ZennoPoster\Tasks.dat`
3. Если размер < 50 байт → битая резервная копия
4. Восстановление: `Tasks.1.dat` → `Tasks.dat`
5. Выполнение до перезапуска службой

## Примеры конфигураций

### Консервативная (по умолчанию)
```xml
<add key="MaxMemoryForInstance" value="1000" />
<add key="MaxAgeForInstance" value="30" />
<add key="KillOld" value="True" />
<add key="KillHeavy" value="True" />
<add key="KillMain" value="False" />
```

### Агрессивная
```xml
<add key="MaxMemoryForInstance" value="500" />
<add key="MaxAgeForInstance" value="15" />
<add key="KillOld" value="True" />
<add key="KillHeavy" value="True" />
<add key="KillMain" value="True" />
<add key="AutoCheckInterval" value="5" />
```

### Только мониторинг
```xml
<add key="KillOld" value="False" />
<add key="KillHeavy" value="False" />
<add key="KillMain" value="False" />
<add key="ShowLogs" value="True" />
```

## Безопасность

⚠️ **ВНИМАНИЕ**: `KillMain = True` может завершить сам ZennoPoster!
- Используйте только при наличии службы автоперезапуска
- По умолчанию `False` для безопасности
- Всегда отображается красным в UI

## Решение проблем

### Процессы "unknown" в списке
- **Причина**: WMI не может прочитать командную строку (права)
- **Решение**: Запустить от Администратора
- **Примечание**: Дочерние процессы (renderer, gpu) могут не иметь `--user-data-dir`

### Процесс не убивается
- Проверьте что настройки сохранены (App.config)
- Убедитесь что лимиты корректны
- Включите `ShowLogs` для просмотра деталей выполнения
- Проверьте права администратора

### Tasks.dat не восстанавливается
- Проверьте путь: `%AppData%\ZennoLab\ZennoPoster\7\ZennoPoster\`
- Убедитесь что `Tasks.1.dat` существует и размер >= 50 байт
- Включите `ShowLogs` для просмотра процесса восстановления


---


**Made with ❤️ by w3bgr3p**