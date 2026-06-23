using System;
using System.IO;

namespace Curitiba.Core.DevTools
{
    /// <summary>
    /// Watches a stage data folder and signals when a file changes, so a desktop dev build can
    /// reload the stage without recompiling. <see cref="FileSystemWatcher"/> fires on a
    /// background thread, so this class only flips a flag and stores the path; the game loop
    /// drains it on the main thread via <see cref="TryConsume"/> and does the actual reload
    /// there (never touching graphics/state off-thread).
    /// </summary>
    internal sealed class StageHotReloader : IDisposable
    {
        private readonly FileSystemWatcher watcher;
        private readonly object gate = new object();
        private volatile bool dirty;
        private string pendingPath;

        /// <summary>The folder being watched (also where the editor should save), or null if disabled.</summary>
        public string WatchedDirectory { get; }

        private StageHotReloader(string directory, string filter)
        {
            WatchedDirectory = directory;
            watcher = new FileSystemWatcher(directory, filter)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
            };
            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Renamed += OnChanged;
        }

        /// <summary>Creates a watcher for the given directory/filter, or null if the directory is missing.</summary>
        public static StageHotReloader TryCreate(string directory, string filter = "*.json")
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return null;

            try
            {
                return new StageHotReloader(directory, filter);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            lock (gate)
            {
                pendingPath = e.FullPath;
            }
            dirty = true;
        }

        /// <summary>
        /// On the game thread: returns true once after a change, handing back the changed file
        /// path. Returns false when nothing has changed.
        /// </summary>
        public bool TryConsume(out string path)
        {
            if (!dirty)
            {
                path = null;
                return false;
            }

            dirty = false;
            lock (gate)
            {
                path = pendingPath;
            }
            return true;
        }

        public void Dispose()
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnChanged;
            watcher.Created -= OnChanged;
            watcher.Renamed -= OnChanged;
            watcher.Dispose();
        }
    }
}
