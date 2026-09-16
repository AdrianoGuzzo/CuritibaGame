using System;
using System.IO;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// Locates the JSON fixtures that ship next to the test assembly.
    /// </summary>
    /// <remarks>
    /// Stage JSON is large and its exact shape is the thing under test, so the fixtures live as real
    /// files rather than as string literals — a broken fixture then reads like broken data, not like
    /// broken C#.
    /// </remarks>
    internal static class Fixtures
    {
        /// <summary>Directory holding the fixture files in the test output.</summary>
        public static string Directory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

        /// <summary>Absolute path of a fixture.</summary>
        public static string Path_(string fileName) => Path.Combine(Directory, fileName);

        /// <summary>Reads a fixture as text.</summary>
        public static string Read(string fileName) => File.ReadAllText(Path_(fileName));

        /// <summary>Directory the game's real stage data is copied into, mirroring the game layout.</summary>
        public static string StagesDirectory =>
            Path.Combine(AppContext.BaseDirectory, "Content", "Data", "Stages");
    }
}
