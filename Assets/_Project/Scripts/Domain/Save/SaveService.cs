using System;
using System.Security.Cryptography;
using System.Text;

namespace P2D.Domain
{
    /// <summary>Turns a save into text and back. Unity uses JsonUtility (see JsonUtilitySaveSerializer).</summary>
    public interface ISaveSerializer
    {
        string Serialize<T>(T value);
        T Deserialize<T>(string text);
    }

    /// <summary>Key-value text storage (local files, PlayerPrefs, or a cloud save adapter).</summary>
    public interface ISaveStorage
    {
        bool Exists(string key);
        string Read(string key);

        /// <summary>Must replace the value atomically (write a temp file, then move).</summary>
        void Write(string key, string text);
        void Delete(string key);
    }

    [Serializable]
    public sealed class SaveEnvelope
    {
        public int version;
        public long savedUtcTicks;
        public string checksum = "";
        public string payload = "";
    }

    public enum LoadStatus
    {
        NewGame,
        Loaded,
        RecoveredFromBackup,
        Corrupt,

        /// <summary>The save came from a newer build: ask the player to update, never overwrite it.</summary>
        NewerVersion,
    }

    public struct LoadResult
    {
        public LoadStatus status;
        public PlayerSave save;
        public OfflineReport offline;
        public string error;
    }

    /// <summary>
    /// Local save with a checksum and a backup of the previous good save. The checksum only
    /// detects corruption (a crash mid-write, a damaged file); the server stays authoritative
    /// for currencies, drops and the weekly reset.
    /// </summary>
    public sealed class SaveService
    {
        public const string MainKey = "player";
        public const string BackupKey = "player.bak";

        readonly ISaveStorage storage;
        readonly ISaveSerializer serializer;
        readonly SaveMigrator migrator;

        public SaveService(ISaveStorage storage, ISaveSerializer serializer, SaveMigrator migrator = null)
        {
            this.storage = storage;
            this.serializer = serializer;
            this.migrator = migrator ?? SaveMigrator.Default();
        }

        public void Save(PlayerSave save, DateTime utcNow)
        {
            save.savedUtcTicks = utcNow.Ticks;
            string payload = serializer.Serialize(save);
            var envelope = new SaveEnvelope
            {
                version = save.version,
                savedUtcTicks = save.savedUtcTicks,
                checksum = Checksum(payload),
                payload = payload,
            };

            // Keep the last good save as a backup before overwriting it.
            if (storage.Exists(MainKey) && TryOpen(storage.Read(MainKey)) != null)
                storage.Write(BackupKey, storage.Read(MainKey));

            storage.Write(MainKey, serializer.Serialize(envelope));
        }

        /// <summary>
        /// Loads the main save, falls back to the backup if it is damaged, migrates it and
        /// applies offline progress. Returns NewGame when nothing is stored.
        /// </summary>
        public LoadResult Load(DateTime utcNow)
        {
            if (!storage.Exists(MainKey) && !storage.Exists(BackupKey))
                return new LoadResult { status = LoadStatus.NewGame };

            var status = LoadStatus.Loaded;
            PlayerSave save = storage.Exists(MainKey) ? TryOpen(storage.Read(MainKey)) : null;
            if (save == null)
            {
                save = storage.Exists(BackupKey) ? TryOpen(storage.Read(BackupKey)) : null;
                status = LoadStatus.RecoveredFromBackup;
            }
            if (save == null)
                return new LoadResult { status = LoadStatus.Corrupt, error = "Main save and backup are both unreadable." };

            if (save.version > migrator.TargetVersion)
                return new LoadResult
                {
                    status = LoadStatus.NewerVersion,
                    error = "Save version " + save.version + " needs a newer app (this build reads " + migrator.TargetVersion + ").",
                };

            try
            {
                migrator.Migrate(save);
            }
            catch (InvalidOperationException e)
            {
                return new LoadResult { status = LoadStatus.Corrupt, error = e.Message };
            }

            var offline = OfflineProgress.Apply(save, utcNow);
            return new LoadResult { status = status, save = save, offline = offline };
        }

        public void DeleteAll()
        {
            storage.Delete(MainKey);
            storage.Delete(BackupKey);
        }

        PlayerSave TryOpen(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                var envelope = serializer.Deserialize<SaveEnvelope>(text);
                if (envelope == null || string.IsNullOrEmpty(envelope.payload)) return null;
                if (envelope.checksum != Checksum(envelope.payload)) return null;
                return serializer.Deserialize<PlayerSave>(envelope.payload);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string Checksum(string payload)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
