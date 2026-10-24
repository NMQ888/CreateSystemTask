using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace CreateSystemTask
{
    public static class TaskSchedulerService
    {
        private const string TaskPrefix = "CreateSystemTask_";
        private static readonly XNamespace Namespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        public static void Register(TaskDefinition task)
        {
            string xmlPath = Path.Combine(Path.GetTempPath(), "CreateSystemTask_" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(xmlPath, BuildXml(task), Encoding.Unicode);
                RunSchtasks("/create /tn " + Quote(TaskName(task.Id)) + " /xml " + Quote(xmlPath) + " /f",
                    "注册计划任务失败", task.Schedule == ScheduleKinds.AtStartup);
            }
            finally
            {
                try { if (File.Exists(xmlPath)) File.Delete(xmlPath); }
                catch { }
            }
        }

        public static void SetEnabled(TaskDefinition task, bool enabled)
        {
            RunSchtasks("/change /tn " + Quote(TaskName(task.Id)) + (enabled ? " /enable" : " /disable"),
                enabled ? "启用计划任务失败" : "停用计划任务失败", task.Schedule == ScheduleKinds.AtStartup);
        }

        public static void Delete(TaskDefinition task)
        {
            bool elevated = task.Schedule == ScheduleKinds.AtStartup;
            string name = TaskName(task.Id);
            ProcessResult query = elevated
                ? InvokeSchtasksElevated("/query /tn " + Quote(name))
                : InvokeSchtasks("/query /tn " + Quote(name));
            if (query.ExitCode != 0) return;
            RunSchtasks("/delete /tn " + Quote(name) + " /f", "删除计划任务失败", elevated);
        }

        private static string BuildXml(TaskDefinition task)
        {
            DateTime start = task.StartAt;
            XElement trigger;
            if (task.Schedule == ScheduleKinds.Once)
            {
                trigger = new XElement(Namespace + "TimeTrigger",
                    new XElement(Namespace + "StartBoundary", start.ToString("yyyy-MM-dd'T'HH:mm:ss")),
                    new XElement(Namespace + "Enabled", "true"));
            }
            else if (task.Schedule == ScheduleKinds.AtStartup)
            {
                trigger = new XElement(Namespace + "BootTrigger",
                    new XElement(Namespace + "Enabled", "true"),
                    task.BootDelaySeconds > 0
                        ? new XElement(Namespace + "Delay", XmlConvert.ToString(TimeSpan.FromSeconds(task.BootDelaySeconds)))
                        : null);
            }
            else if (task.Schedule == ScheduleKinds.OnLogon)
            {
                trigger = new XElement(Namespace + "LogonTrigger",
                    new XElement(Namespace + "Enabled", "true"),
                    new XElement(Namespace + "UserId", WindowsIdentity.GetCurrent().Name));
            }
            else if (task.Schedule == ScheduleKinds.OnEvent)
            {
                trigger = new XElement(Namespace + "EventTrigger",
                    new XElement(Namespace + "Enabled", "true"),
                    new XElement(Namespace + "Subscription", BuildEventSubscription(task)));
            }
            else if (task.Schedule == ScheduleKinds.Monthly)
            {
                string day = task.MonthlyDayOfMonth >= 32 ? "Last" : Math.Max(1, Math.Min(31, task.MonthlyDayOfMonth)).ToString();
                XElement days = new XElement(Namespace + "DaysOfMonth", new XElement(Namespace + "Day", day));
                XElement months = new XElement(Namespace + "Months",
                    new XElement(Namespace + "January"), new XElement(Namespace + "February"),
                    new XElement(Namespace + "March"), new XElement(Namespace + "April"),
                    new XElement(Namespace + "May"), new XElement(Namespace + "June"),
                    new XElement(Namespace + "July"), new XElement(Namespace + "August"),
                    new XElement(Namespace + "September"), new XElement(Namespace + "October"),
                    new XElement(Namespace + "November"), new XElement(Namespace + "December"));
                trigger = new XElement(Namespace + "CalendarTrigger",
                    new XElement(Namespace + "StartBoundary", start.ToString("yyyy-MM-dd'T'HH:mm:ss")),
                    new XElement(Namespace + "Enabled", "true"),
                    new XElement(Namespace + "ScheduleByMonth", days, months));
            }
            else if (task.Schedule == ScheduleKinds.Weekly)
            {
                XElement days = new XElement(Namespace + "DaysOfWeek");
                AddDay(days, task.WeeklyDaysMask, DayOfWeek.Monday, "Monday");
                AddDay(days, task.WeeklyDaysMask, DayOfWeek.Tuesday, "Tuesday");
                AddDay(days, task.WeeklyDaysMask, DayOfWeek.Wednesday, "Wednesday");
                AddDay(days, task.WeeklyDaysMask, DayOfWeek.Thursday, "Thursday");
                AddDay(days, task.WeeklyDaysMask, DayOfWeek.Friday, "Friday");
                AddDay(days, task.WeeklyDaysMask, DayOfWeek.Saturday, "Saturday");
                AddDay(days, task.WeeklyDaysMask, DayOfWeek.Sunday, "Sunday");
                trigger = new XElement(Namespace + "CalendarTrigger",
                    new XElement(Namespace + "StartBoundary", start.ToString("yyyy-MM-dd'T'HH:mm:ss")),
                    new XElement(Namespace + "Enabled", "true"),
                    new XElement(Namespace + "ScheduleByWeek",
                        new XElement(Namespace + "WeeksInterval", "1"), days));
            }
            else
            {
                trigger = new XElement(Namespace + "CalendarTrigger",
                    new XElement(Namespace + "StartBoundary", start.ToString("yyyy-MM-dd'T'HH:mm:ss")),
                    new XElement(Namespace + "Enabled", "true"),
                    new XElement(Namespace + "ScheduleByDay", new XElement(Namespace + "DaysInterval", "1")));
            }

            string executable = Process.GetCurrentProcess().MainModule.FileName;
            XElement root = new XElement(Namespace + "Task",
                new XAttribute("version", "1.2"),
                new XElement(Namespace + "RegistrationInfo",
                    new XElement(Namespace + "Date", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")),
                    new XElement(Namespace + "Author", WindowsIdentity.GetCurrent().Name),
                    new XElement(Namespace + "Description", task.Name ?? "CreateSystemTask")),
                new XElement(Namespace + "Triggers", trigger),
                new XElement(Namespace + "Principals",
                    new XElement(Namespace + "Principal", new XAttribute("id", "Author"),
                        new XElement(Namespace + "UserId", WindowsIdentity.GetCurrent().Name),
                        new XElement(Namespace + "LogonType", task.Schedule == ScheduleKinds.AtStartup ? "S4U" : "InteractiveToken"),
                        new XElement(Namespace + "RunLevel", "LeastPrivilege"))),
                new XElement(Namespace + "Settings",
                    new XElement(Namespace + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(Namespace + "DisallowStartIfOnBatteries", "false"),
                    new XElement(Namespace + "StopIfGoingOnBatteries", "false"),
                    new XElement(Namespace + "StartWhenAvailable", "true"),
                    new XElement(Namespace + "ExecutionTimeLimit", "PT2H"),
                    new XElement(Namespace + "Enabled", task.Enabled ? "true" : "false"),
                    new XElement(Namespace + "Hidden", "false")),
                new XElement(Namespace + "Actions", new XAttribute("Context", "Author"),
                    new XElement(Namespace + "Exec",
                        new XElement(Namespace + "Command", executable),
                        new XElement(Namespace + "Arguments", "--run-task " + task.Id),
                        new XElement(Namespace + "WorkingDirectory", Path.GetDirectoryName(executable)))));

            return new XDocument(new XDeclaration("1.0", "utf-16", null), root).ToString(SaveOptions.DisableFormatting);
        }

        private static string BuildEventSubscription(TaskDefinition task)
        {
            string channel = String.IsNullOrWhiteSpace(task.EventChannel) ? "System" : task.EventChannel.Trim();
            string[] conditions = new string[3];
            int count = 0;
            if (!String.IsNullOrWhiteSpace(task.EventProvider))
                conditions[count++] = "Provider[@Name=" + XPathLiteral(task.EventProvider.Trim()) + "]";
            int eventId;
            if (!String.IsNullOrWhiteSpace(task.EventId) && Int32.TryParse(task.EventId, out eventId))
                conditions[count++] = "EventID=" + eventId;
            if (task.EventLevel >= 1 && task.EventLevel <= 5)
                conditions[count++] = "Level=" + task.EventLevel;

            string systemFilter = count == 0 ? "System" : "System[" + String.Join(" and ", conditions.Take(count)) + "]";
            XElement query = new XElement("QueryList",
                new XElement("Query", new XAttribute("Id", "1"), new XAttribute("Path", channel),
                    new XElement("Select", new XAttribute("Path", channel), "*[" + systemFilter + "]")));
            return query.ToString(SaveOptions.DisableFormatting);
        }

        private static string XPathLiteral(string value)
        {
            if (!value.Contains("'")) return "'" + value + "'";
            if (!value.Contains("\"")) return "\"" + value + "\"";
            string[] pieces = value.Split('\'');
            return "concat(" + String.Join(",\"'\",", pieces.Select(p => "'" + p + "'")) + ")";
        }

        private static void AddDay(XElement parent, int mask, DayOfWeek day, string element)
        {
            if ((mask & (1 << (int)day)) != 0) parent.Add(new XElement(Namespace + element));
        }

        private static string TaskName(string id)
        {
            return TaskPrefix + id;
        }

        private static void RunSchtasks(string arguments, string errorTitle, bool elevated = false)
        {
            ProcessResult result = elevated ? InvokeSchtasksElevated(arguments) : InvokeSchtasks(arguments);
            if (result.ExitCode != 0 && errorTitle != null)
            {
                string details = String.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error + Environment.NewLine + result.Output;
                throw new InvalidOperationException(errorTitle + "。" + Environment.NewLine + details.Trim());
            }
        }

        private static ProcessResult InvokeSchtasksElevated(string arguments)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process process = Process.Start(info))
                {
                    process.WaitForExit();
                    return new ProcessResult { ExitCode = process.ExitCode, Output = String.Empty, Error = String.Empty };
                }
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                    throw new InvalidOperationException("此操作需要管理员权限；Windows 权限提示已取消。", ex);
                throw;
            }
        }

        private static ProcessResult InvokeSchtasks(string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(info))
            {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(outputTask, errorTask);
                return new ProcessResult { ExitCode = process.ExitCode, Output = outputTask.Result, Error = errorTask.Result };
            }
        }

        private static string Quote(string value)
        {
            StringBuilder result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                result.Append(c);
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            result.Append('"');
            return result.ToString();
        }

        private sealed class ProcessResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }
    }
}
