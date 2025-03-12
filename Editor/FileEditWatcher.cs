using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Text.RegularExpressions;

namespace VibeGamedev
{
    public class FileEditWatcher
    {
        private const string JSON_QUEUE_PATH = "vgd_json_edit_queue.txt";
        private readonly HashSet<string> knownJsonFiles = new();

        private readonly string jsonQueuePath;

        private readonly Dictionary<string, DateTime> lastProcessedTimes = new();
        private const int DEBOUNCE_MS = 100; // 100ms debounce window

        private readonly FileSystemWatcher jsonWatcher;
        private readonly FileSystemWatcher directoryWatcher;
        private bool paused = true;
        private readonly object pausedLock = new();
        private int queueSize = 0;

        public FileEditWatcher(string rootDirectory)
        {
            jsonQueuePath = JSON_QUEUE_PATH;
            if (File.Exists(jsonQueuePath))
            {
                queueSize = File.ReadAllLines(jsonQueuePath).Where(line => line.Length > 0).Count();
            }
            else
            {
                using (File.Create(jsonQueuePath)) { }
            }

            // Watcher for JSON files
            jsonWatcher = new()
            {
                Path = rootDirectory,
                Filter = "*info.json",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Security
            };
            jsonWatcher.Created += OnJsonChanged;
            jsonWatcher.Changed += OnJsonChanged;
            jsonWatcher.Deleted += OnJsonDeleted;

            // Watcher for directories
            directoryWatcher = new()
            {
                Path = rootDirectory,
                Filter = "*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.DirectoryName
            };
            directoryWatcher.Deleted += OnDirectoryDeleted;
            directoryWatcher.Renamed += OnDirectoryRenamed;

            foreach (string jsonPath in Directory.GetFiles(rootDirectory, "*info.json", SearchOption.AllDirectories))
            {
                knownJsonFiles.Add(Path.GetFullPath(jsonPath));
            }
        }

        public void Dispose()
        {
            if (jsonWatcher != null)
            {
                jsonWatcher.EnableRaisingEvents = false;
                jsonWatcher.Dispose();
            }
            if (directoryWatcher != null)
            {
                directoryWatcher.EnableRaisingEvents = false;
                directoryWatcher.Dispose();
            }
        }

        public void Pause()
        {
            lock (pausedLock)
            {
                paused = true;
            }
        }

        public void Resume()
        {
            lock (pausedLock)
            {
                paused = false;
            }
        }

        public enum FileActionType
        {
            Delete,
            Edit
        }

        public struct FileAction
        {
            public string jsonPath;
            public FileActionType actionType;
            public string jsonContent;

            public FileAction(string line)
            {
                string[] parts = line.Split('*');
                jsonPath = parts[1];
                jsonContent = string.Join("*", parts[2..]);
                actionType = parts[0] switch
                {
                    "d" => FileActionType.Delete,
                    "e" => FileActionType.Edit,
                    _ => throw new ArgumentException($"Invalid file action type: {parts[0]}")
                };
            }
        }

        public List<FileAction> ReadQueue()
        {
            if (queueSize == 0)
            {
                return new List<FileAction>();
            }
            string[] lines;
            lock (jsonQueuePath)
            {
                lines = File.ReadAllLines(jsonQueuePath);
                File.WriteAllText(jsonQueuePath, "");
                queueSize = 0;
            }
            return lines.Where(line => line.Length > 0).Select(line => new FileAction(line)).ToList();
        }

        public bool IsQueueEmpty()
        {
            return queueSize == 0;
        }

        void OnJsonChanged(object sender, FileSystemEventArgs e)
        {
            lock (pausedLock)
            {
                if (paused)
                {
                    return;
                }
            }
            var now = DateTime.Now;
            bool shouldProcess = false;
            lock (lastProcessedTimes)
            {
                if (!lastProcessedTimes.ContainsKey(e.FullPath) ||
                    (now - lastProcessedTimes[e.FullPath]).TotalMilliseconds >= DEBOUNCE_MS)
                {
                    shouldProcess = true;
                    lastProcessedTimes[e.FullPath] = now;
                }
            }
            if (shouldProcess)
            {
                string jsonContent = File.ReadAllText(e.FullPath).Replace("\n", "");
                if (jsonContent == null || jsonContent == "" || !jsonContent.StartsWith("{"))
                {
                    return;
                }
                lock (jsonQueuePath)
                {
                    File.AppendAllText(jsonQueuePath, "e*" + e.FullPath + "*" + jsonContent + "\n");
                    knownJsonFiles.Add(Path.GetFullPath(e.FullPath));
                }
                queueSize++;
                SettingsWindow.Log("JSON changed (queue size: " + queueSize + "): " + e.FullPath);
            }
        }

        void OnJsonDeleted(object sender, FileSystemEventArgs e)
        {
            lock (pausedLock)
            {
                if (paused)
                {
                    return;
                }
            }
            lock (jsonQueuePath)
            {
                File.AppendAllText(jsonQueuePath, "d*" + e.FullPath + "*\n");
                knownJsonFiles.Remove(Path.GetFullPath(e.FullPath));
                queueSize++;
            }
            SettingsWindow.Log("JSON deleted (queue size: " + queueSize + "): " + e.FullPath);
        }

        void OnDirectoryDeleted(object sender, FileSystemEventArgs e)
        {
            lock (pausedLock)
            {
                if (paused)
                {
                    return;
                }
            }
            lock (jsonQueuePath)
            {
                foreach (string jsonFile in knownJsonFiles.Where(jsonFile => jsonFile.StartsWith(e.FullPath)))
                {
                    lock (jsonQueuePath)
                    {
                        File.AppendAllText(jsonQueuePath, "d*" + jsonFile + "*\n");
                        knownJsonFiles.Remove(Path.GetFullPath(jsonFile));
                        queueSize++;
                    }
                    SettingsWindow.Log("JSON deleted via directory deletion (queue size: " + queueSize + "): " + jsonFile);
                }
            }
        }

        void OnDirectoryRenamed(object sender, RenamedEventArgs e)
        {
            lock (pausedLock)
            {
                if (paused)
                {
                    return;
                }
            }
            lock (jsonQueuePath)
            {
                string[] oldJsonFiles = knownJsonFiles.Where(jsonFile => jsonFile.StartsWith(Path.GetFullPath(e.OldFullPath))).ToArray();
                foreach (string jsonFile in oldJsonFiles)
                {
                    lock (jsonQueuePath)
                    {
                        knownJsonFiles.Remove(Path.GetFullPath(jsonFile));
                        string newJsonFile = jsonFile.Replace(Path.GetFullPath(e.OldFullPath), Path.GetFullPath(e.FullPath));
                        if (File.Exists(newJsonFile))
                        {
                            string jsonContent = File.ReadAllText(newJsonFile).Replace("\n", "");
                            File.AppendAllText(jsonQueuePath, "e*" + newJsonFile + "*" + jsonContent + "\n");
                            knownJsonFiles.Add(newJsonFile);
                            queueSize++;
                            SettingsWindow.Log($"Parent directory renamed, queued JSON file for move: {jsonFile} -> {newJsonFile}");
                        }
                    }
                }
            }
        }
    }
}