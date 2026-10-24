using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CreateSystemTask
{
    public static class TaskExecutor
    {
        public static bool RunScheduledTask(string id)
        {
            TaskDefinition task = TaskStore.Find(id);
            if (task == null || !task.Enabled) return false;
            return Execute(task);
        }

        public static bool RunNow(string id)
        {
            TaskDefinition task = TaskStore.Find(id);
            if (task == null) return false;
            return Execute(task);
        }

        private static bool Execute(TaskDefinition task)
        {
            string result;
            bool success;
            try
            {
                if (task.Kind == TaskKinds.Cleanup) result = Cleanup(task);
                else if (task.Kind == TaskKinds.Launch) result = Launch(task);
                else throw new InvalidOperationException("此版本无法执行该任务类型。");
                success = !result.StartsWith("有错误", StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                success = false;
                result = "执行失败：" + ex.Message;
            }

            task.LastRunAt = DateTime.Now;
            task.LastRunResult = result;
            try { TaskStore.Update(task); }
            catch { }
            return success;
        }

        private static string Launch(TaskDefinition task)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = task.TargetPath,
                Arguments = task.LaunchArguments ?? String.Empty,
                WorkingDirectory = Path.GetDirectoryName(task.TargetPath),
                UseShellExecute = true
            };
            Process.Start(info);
            return "已启动程序";
        }

        private static string Cleanup(TaskDefinition task)
        {
            string fullRoot = Path.GetFullPath(task.TargetPath);
            string pathRoot = Path.GetPathRoot(fullRoot);
            if (String.Equals(fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                (pathRoot ?? String.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("不能把磁盘根目录设为清理目标。");
            string root = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("目标文件夹不存在：" + root);
            DirectoryInfo rootInfo = new DirectoryInfo(root);
            if (rootInfo.Parent == null) throw new InvalidOperationException("不能把磁盘根目录设为清理目标。");

            string[] patterns = (task.FilePatterns ?? "*.*").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            if (patterns.Length == 0) patterns = new[] { "*.*" };
            DateTime cutoff = DateTime.Now.AddDays(-Math.Max(0, task.RetentionDays));
            CleanupCounts counts = new CleanupCounts();
            ProcessDirectory(rootInfo, root, patterns, cutoff, true, task.IncludeSubdirectories, task.DeleteEmptyFolders, counts);

            string summary = "已删除 " + counts.FilesDeleted + " 个文件";
            if (task.DeleteEmptyFolders) summary += "、" + counts.FoldersDeleted + " 个空文件夹";
            if (counts.Errors.Count > 0)
            {
                summary = "有错误：" + summary + "；失败 " + counts.Errors.Count + " 项（" + String.Join("；", counts.Errors.Take(3)) + "）";
            }
            return summary;
        }

        private static bool ProcessDirectory(DirectoryInfo directory, string root, string[] patterns, DateTime cutoff,
            bool processFiles, bool includeSubdirectories, bool deleteEmptyFolders, CleanupCounts counts)
        {
            try
            {
                if (processFiles)
                {
                    foreach (string pattern in patterns)
                    {
                        string[] files;
                        try { files = Directory.GetFiles(directory.FullName, pattern, SearchOption.TopDirectoryOnly); }
                        catch (Exception ex) { counts.Errors.Add(directory.Name + "：" + ex.Message); continue; }

                        foreach (string file in files)
                        {
                            try
                            {
                                FileInfo info = new FileInfo(file);
                                if (info.LastAccessTime < cutoff)
                                {
                                    info.IsReadOnly = false;
                                    info.Delete();
                                    counts.FilesDeleted++;
                                }
                            }
                            catch (Exception ex) { counts.Errors.Add(Path.GetFileName(file) + "：" + ex.Message); }
                        }
                    }
                }

                if (includeSubdirectories || deleteEmptyFolders)
                {
                    DirectoryInfo[] children;
                    try { children = directory.GetDirectories(); }
                    catch (Exception ex) { counts.Errors.Add(directory.Name + "：" + ex.Message); return false; }
                    foreach (DirectoryInfo child in children)
                    {
                        try
                        {
                            if ((child.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                            ProcessDirectory(child, root, patterns, cutoff, includeSubdirectories, includeSubdirectories, deleteEmptyFolders, counts);
                        }
                        catch (Exception ex) { counts.Errors.Add(child.Name + "：" + ex.Message); }
                    }
                }

                if (deleteEmptyFolders && !String.Equals(directory.FullName.TrimEnd(Path.DirectorySeparatorChar), root,
                    StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (!directory.EnumerateFileSystemInfos().Any())
                        {
                            directory.Delete();
                            counts.FoldersDeleted++;
                            return true;
                        }
                    }
                    catch (Exception ex) { counts.Errors.Add(directory.Name + "：" + ex.Message); }
                }
            }
            catch (Exception ex) { counts.Errors.Add(directory.Name + "：" + ex.Message); }
            return false;
        }

        private sealed class CleanupCounts
        {
            public int FilesDeleted;
            public int FoldersDeleted;
            public readonly List<string> Errors = new List<string>();
        }
    }
}
