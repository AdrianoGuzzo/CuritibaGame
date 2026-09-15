using System;
using System.Text.Json;
using Curitiba.Core.Settings;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// An <see cref="ISettingsStorage"/> that keeps the serialized payload in memory, so the
    /// save/load contract can be tested without the user's real settings file.
    /// </summary>
    /// <remarks>
    /// It serializes for real (rather than stashing the object) so round-trip tests still exercise
    /// <see cref="JsonSerializer"/>, and it can be told to hold corrupt data or to throw, which is how
    /// the failure paths of <c>SettingsManager</c> are reached without breaking a real file.
    /// </remarks>
    internal sealed class InMemorySettingsStorage : ISettingsStorage
    {
        private string payload;

        public string SettingsFileName { get; set; } = "settings.json";

        /// <summary>Number of times <see cref="SaveSettings{T}"/> was called (including failed ones).</summary>
        public int SaveCount { get; private set; }

        /// <summary>Number of times <see cref="LoadSettings{T}"/> was called.</summary>
        public int LoadCount { get; private set; }

        /// <summary>When set, <see cref="SaveSettings{T}"/> throws it instead of storing.</summary>
        public Exception ThrowOnSave { get; set; }

        /// <summary>When set, <see cref="LoadSettings{T}"/> throws it instead of reading.</summary>
        public Exception ThrowOnLoad { get; set; }

        /// <summary>When true, <see cref="LoadSettings{T}"/> returns null rather than a default instance.</summary>
        public bool ReturnsNull { get; set; }

        /// <summary>Seeds the storage with text that is not valid JSON for the settings type.</summary>
        public void Corrupt(string text = "{ not json at all") => payload = text;

        /// <summary>Seeds the storage with an already-serialized value.</summary>
        public void Seed<T>(T settings) where T : new() => payload = JsonSerializer.Serialize(settings);

        public void SaveSettings<T>(T settings) where T : new()
        {
            SaveCount++;
            if (ThrowOnSave != null)
                throw ThrowOnSave;

            payload = JsonSerializer.Serialize(settings);
        }

        public T LoadSettings<T>() where T : new()
        {
            LoadCount++;
            if (ThrowOnLoad != null)
                throw ThrowOnLoad;

            if (ReturnsNull)
                return default;

            if (payload == null)
                return new T();

            return JsonSerializer.Deserialize<T>(payload) ?? new T();
        }

        public bool SettingsExist() => payload != null;
    }
}
