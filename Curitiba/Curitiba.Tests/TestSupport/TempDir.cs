using System;
using System.IO;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// A scratch directory that exists only for the lifetime of one test.
    /// </summary>
    /// <remarks>
    /// Used by the handful of tests whose subject really is the filesystem (<c>StageLoader</c>
    /// reading and writing files, <c>BaseSettingsStorage</c> paths). Each instance gets its own
    /// unique directory so tests stay independent even when xunit runs them in parallel.
    /// </remarks>
    internal sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "curitiba-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>Absolute path of <paramref name="fileName"/> inside this directory.</summary>
        public string File(string fileName) => System.IO.Path.Combine(Path, fileName);

        /// <summary>Writes <paramref name="contents"/> to <paramref name="fileName"/> and returns its path.</summary>
        public string Write(string fileName, string contents)
        {
            string path = File(fileName);
            System.IO.File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A locked file must never fail the test that owns this directory.
            }
        }
    }
}
