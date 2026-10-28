using System.Collections.Generic;
using P2D.Domain;

namespace P2D.Services
{
    /// <summary>In-memory save storage for tests and throwaway sessions.</summary>
    public sealed class MemorySaveStorage : ISaveStorage
    {
        readonly Dictionary<string, string> data = new Dictionary<string, string>();

        public bool Exists(string key) => data.ContainsKey(key);
        public string Read(string key) => data[key];
        public void Write(string key, string text) => data[key] = text;
        public void Delete(string key) => data.Remove(key);
    }
}
