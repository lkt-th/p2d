using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    public enum Language { English, Thai }

    /// <summary>
    /// Player-facing text. The English source string is the key: <see cref="T"/> returns it as is in
    /// English and looks it up in <see cref="ThaiText"/> in Thai (falling back to English when a line
    /// is missing). Strings with numbers use <see cref="F"/> with {0}-style placeholders, so the Thai
    /// line can move the numbers where Thai grammar wants them. Enums use "Type.Value" keys.
    /// Pure C#: the server can format the same messages.
    /// </summary>
    public static class Loc
    {
        public static Language Current = Language.English;

        public static string T(string english)
        {
            if (Current == Language.Thai && english != null)
            {
                string thai;
                if (ThaiText.Table.TryGetValue(english, out thai)) return thai;
            }
            return english;
        }

        public static string F(string english, params object[] args) => string.Format(T(english), args);

        /// <summary>Enum names: "Rarity.Epic" → "มหากาพย์" in Thai, "Epic" in English.</summary>
        public static string Enum<TEnum>(TEnum value) where TEnum : struct
        {
            string name = value.ToString();
            if (Current == Language.Thai)
            {
                string thai;
                if (ThaiText.Table.TryGetValue(typeof(TEnum).Name + "." + name, out thai)) return thai;
            }
            return name;
        }

        /// <summary>Rider display name from its id ("tim" → "Tim" / "ทิม").</summary>
        public static string Rider(string riderId)
        {
            if (string.IsNullOrEmpty(riderId)) return "";
            if (Current == Language.Thai)
            {
                string thai;
                if (ThaiText.Table.TryGetValue("Rider." + riderId, out thai)) return thai;
            }
            return char.ToUpperInvariant(riderId[0]) + riderId.Substring(1);
        }

        /// <summary>Skill display name from its id ("flame_claw" → "Flame Claw" / Thai).</summary>
        public static string Skill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return "";
            if (Current == Language.Thai)
            {
                string thai;
                if (ThaiText.Table.TryGetValue("Skill." + skillId, out thai)) return thai;
            }
            var words = skillId.Split('_');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        /// <summary>The placeholders ({0}, {1}, …) a format string uses; tests check both languages agree.</summary>
        public static SortedSet<int> Placeholders(string format)
        {
            var set = new SortedSet<int>();
            for (int i = 0; i < format.Length; i++)
            {
                if (format[i] != '{') continue;
                if (i + 1 < format.Length && format[i + 1] == '{') { i++; continue; }
                int end = format.IndexOf('}', i);
                if (end < 0) break;
                string inner = format.Substring(i + 1, end - i - 1);
                int colon = inner.IndexOf(':');
                if (colon >= 0) inner = inner.Substring(0, colon);
                int n;
                if (int.TryParse(inner, out n)) set.Add(n);
                i = end;
            }
            return set;
        }
    }
}
