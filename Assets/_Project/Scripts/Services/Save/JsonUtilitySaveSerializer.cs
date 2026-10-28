using P2D.Domain;
using UnityEngine;

namespace P2D.Services
{
    /// <summary>JsonUtility is IL2CPP-safe and handles every type in PlayerSave (fields, arrays, List).</summary>
    public sealed class JsonUtilitySaveSerializer : ISaveSerializer
    {
        public string Serialize<T>(T value) => JsonUtility.ToJson(value);

        public T Deserialize<T>(string text) => JsonUtility.FromJson<T>(text);
    }
}
