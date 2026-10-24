using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace CreateSystemTask
{
    public static class TaskStore
    {
        private static readonly string StoreDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CreateSystemTask");
        private static readonly string StorePath = Path.Combine(StoreDirectory, "tasks.json");
        private static readonly string MutexName = @"Local\CreateSystemTask.TaskStore";

        public static List<TaskDefinition> GetAll()
        {
            return WithStore(delegate(List<TaskDefinition> tasks) { return tasks.Select(t => t.Copy()).ToList(); }, false);
        }

        public static TaskDefinition Find(string id)
        {
            return WithStore(delegate(List<TaskDefinition> tasks)
            {
                TaskDefinition found = tasks.FirstOrDefault(t => String.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
                return found == null ? null : found.Copy();
            }, false);
        }

        public static void Add(TaskDefinition task)
        {
            WithStore(delegate(List<TaskDefinition> tasks)
            {
                if (tasks.Any(t => t.Id == task.Id)) throw new InvalidOperationException("任务 ID 已存在。");
                tasks.Add(task.Copy());
                return true;
            }, true);
        }

        public static void Update(TaskDefinition task)
        {
            WithStore(delegate(List<TaskDefinition> tasks)
            {
                int index = tasks.FindIndex(t => String.Equals(t.Id, task.Id, StringComparison.OrdinalIgnoreCase));
                if (index < 0) throw new InvalidOperationException("任务已不存在，请刷新列表后重试。");
                tasks[index] = task.Copy();
                return true;
            }, true);
        }

        public static void Delete(string id)
        {
            WithStore(delegate(List<TaskDefinition> tasks)
            {
                tasks.RemoveAll(t => String.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
                return true;
            }, true);
        }

        private static T WithStore<T>(Func<List<TaskDefinition>, T> action, bool save)
        {
            using (Mutex mutex = new Mutex(false, MutexName))
            {
                bool acquired = false;
                try
                {
                    try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(15)); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new TimeoutException("任务列表正被另一个进程使用，请稍后重试。");

                    Directory.CreateDirectory(StoreDirectory);
                    List<TaskDefinition> tasks = ReadFile();
                    T result = action(tasks);
                    if (save) WriteFile(tasks);
                    return result;
                }
                finally
                {
                    if (acquired) mutex.ReleaseMutex();
                }
            }
        }

        private static List<TaskDefinition> ReadFile()
        {
            if (!File.Exists(StorePath)) return new List<TaskDefinition>();
            using (FileStream stream = File.OpenRead(StorePath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<TaskDefinition>));
                List<TaskDefinition> tasks = (List<TaskDefinition>)serializer.ReadObject(stream) ?? new List<TaskDefinition>();
                foreach (TaskDefinition task in tasks)
                {
                    if (task.MonthlyDayOfMonth <= 0) task.MonthlyDayOfMonth = 1;
                    if (task.BootDelaySeconds < 0) task.BootDelaySeconds = 0;
                    if (String.IsNullOrWhiteSpace(task.EventChannel)) task.EventChannel = "System";
                    if (task.EventProvider == null) task.EventProvider = String.Empty;
                    if (task.EventId == null) task.EventId = String.Empty;
                    if (task.EventLevel < 0 || task.EventLevel > 5) task.EventLevel = 0;
                }
                return tasks;
            }
        }

        private static void WriteFile(List<TaskDefinition> tasks)
        {
            string temporaryPath = StorePath + ".tmp";
            using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new DataContractJsonSerializer(typeof(List<TaskDefinition>)).WriteObject(stream, tasks);
                stream.Flush(true);
            }

            if (File.Exists(StorePath)) File.Replace(temporaryPath, StorePath, null);
            else File.Move(temporaryPath, StorePath);
        }
    }
}
