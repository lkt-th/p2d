using System.IO;
using System.Text;
using P2D.Domain;
using UnityEngine;

namespace P2D.Services
{
    /// <summary>
    /// One file per key under Application.persistentDataPath/saves. Writes go to a temp file
    /// first and then replace the target, so a crash mid-write never leaves a half file.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        readonly string directory;

        public FileSaveStorage(string directory = null)
        {
            this.directory = directory ?? Path.Combine(Application.persistentDataPath, "saves");
            Directory.CreateDirectory(this.directory);
        }

        string PathFor(string key) => Path.Combine(directory, key + ".json");

        public bool Exists(string key) => File.Exists(PathFor(key));

        public string Read(string key) => File.ReadAllText(PathFor(key), Encoding.UTF8);

        public void Write(string key, string text)
        {
            string target = PathFor(key);
            string temp = target + ".tmp";
            File.WriteAllText(temp, text, Encoding.UTF8);
            if (File.Exists(target))
            {
                try
                {
                    File.Replace(temp, target, null);
                    return;
                }
                catch (IOException)
                {
                    File.Delete(target);
                }
            }
            File.Move(temp, target);
        }

        public void Delete(string key)
        {
            string target = PathFor(key);
            if (File.Exists(target)) File.Delete(target);
        }
    }
}
