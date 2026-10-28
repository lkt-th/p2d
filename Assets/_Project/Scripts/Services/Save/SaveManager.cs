using System;
using P2D.Domain;
using UnityEngine;

namespace P2D.Services
{
    /// <summary>
    /// Owns the loaded PlayerSave for the session. Put it in the Boot scene; it survives scene
    /// loads. Systems change <see cref="Current"/> and call <see cref="MarkDirty"/>; saving
    /// happens on a timer, when the app is paused (backgrounded) and on quit.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [SerializeField] string starterRiderId = "tim";
        [SerializeField] string defaultDragonName = "Ignis";
        [SerializeField, Min(5)] float autosaveSeconds = 30;

        SaveService service;
        float nextAutosave;
        bool dirty;

        public PlayerSave Current { get; private set; }
        public LoadResult LastLoad { get; private set; }

        /// <summary>Raised after loading, with what happened while the player was away.</summary>
        public event Action<PlayerSave, OfflineReport> Loaded;

        /// <summary>Set before the first SaveManager wakes to use other storage (tests use MemorySaveStorage).</summary>
        public static ISaveStorage StorageOverride;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            service = new SaveService(StorageOverride ?? new FileSaveStorage(), new JsonUtilitySaveSerializer());
            Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Load()
        {
            var now = ServerClock.UtcNow;
            LastLoad = service.Load(now);
            switch (LastLoad.status)
            {
                case LoadStatus.NewGame:
                    // Local id until the backend account (UGS Authentication) replaces it.
                    Current = PlayerSave.CreateNew(Guid.NewGuid().ToString("N"), defaultDragonName, now, starterRiderId);
                    dirty = true;
                    break;
                case LoadStatus.Loaded:
                case LoadStatus.RecoveredFromBackup:
                    Current = LastLoad.save;
                    dirty = true;
                    if (LastLoad.status == LoadStatus.RecoveredFromBackup)
                        Debug.LogWarning("[P2D] Main save was damaged; restored the previous save.");
                    break;
                default:
                    // Never overwrite a save we can't read (newer app version or both files damaged).
                    Debug.LogError("[P2D] Could not load save: " + LastLoad.error);
                    Current = null;
                    return;
            }
            nextAutosave = Time.unscaledTime + autosaveSeconds;
            if (Loaded != null) Loaded(Current, LastLoad.offline);
        }

        public void MarkDirty() => dirty = true;

        public void SaveNow()
        {
            if (Current == null) return;
            service.Save(Current, ServerClock.UtcNow);
            dirty = false;
            nextAutosave = Time.unscaledTime + autosaveSeconds;
        }

        void Update()
        {
            if (dirty && Time.unscaledTime >= nextAutosave) SaveNow();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && dirty) SaveNow();
        }

        void OnApplicationQuit()
        {
            if (dirty) SaveNow();
        }
    }
}
