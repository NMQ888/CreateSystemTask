using System;
using System.Runtime.Serialization;

namespace CreateSystemTask
{
    [DataContract]
    public sealed class TaskDefinition
    {
        public TaskDefinition()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "新建任务";
            Kind = TaskKinds.Cleanup;
            Enabled = true;
            Schedule = ScheduleKinds.Daily;
            StartAt = DateTime.Now.AddMinutes(5);
            WeeklyDaysMask = (1 << (int)DateTime.Now.DayOfWeek);
            FilePatterns = "*.*";
            RetentionDays = 30;
            IncludeSubdirectories = true;
            MonthlyDayOfMonth = 1;
            BootDelaySeconds = 30;
            EventChannel = "System";
            EventProvider = String.Empty;
            EventId = String.Empty;
            EventLevel = 0;
        }

        [DataMember(Order = 1)] public string Id { get; set; }
        [DataMember(Order = 2)] public string Name { get; set; }
        [DataMember(Order = 3)] public string Kind { get; set; }
        [DataMember(Order = 4)] public bool Enabled { get; set; }
        [DataMember(Order = 5)] public string Schedule { get; set; }
        [DataMember(Order = 6)] public DateTime StartAt { get; set; }
        [DataMember(Order = 7)] public int WeeklyDaysMask { get; set; }
        [DataMember(Order = 8)] public string TargetPath { get; set; }
        [DataMember(Order = 9)] public string FilePatterns { get; set; }
        [DataMember(Order = 10)] public int RetentionDays { get; set; }
        [DataMember(Order = 11)] public bool IncludeSubdirectories { get; set; }
        [DataMember(Order = 12)] public bool DeleteEmptyFolders { get; set; }
        [DataMember(Order = 13)] public string LaunchArguments { get; set; }
        [DataMember(Order = 14)] public DateTime? LastRunAt { get; set; }
        [DataMember(Order = 15)] public string LastRunResult { get; set; }
        [DataMember(Order = 16)] public int MonthlyDayOfMonth { get; set; }
        [DataMember(Order = 17)] public int BootDelaySeconds { get; set; }
        [DataMember(Order = 18)] public string EventChannel { get; set; }
        [DataMember(Order = 19)] public string EventProvider { get; set; }
        [DataMember(Order = 20)] public string EventId { get; set; }
        [DataMember(Order = 21)] public int EventLevel { get; set; }

        public TaskDefinition Copy()
        {
            return (TaskDefinition)MemberwiseClone();
        }
    }

    public static class TaskKinds
    {
        public const string Cleanup = "Cleanup";
        public const string Launch = "Launch";
    }

    public static class ScheduleKinds
    {
        public const string Daily = "Daily";
        public const string Weekly = "Weekly";
        public const string Monthly = "Monthly";
        public const string Once = "Once";
        public const string AtStartup = "AtStartup";
        public const string OnLogon = "OnLogon";
        public const string OnEvent = "OnEvent";
    }
}
