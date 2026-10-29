using P2D.Domain;
using UnityEngine;

namespace P2D.Services
{
    /// <summary>
    /// The player's UI language. A device setting (PlayerPrefs), not part of the save: two devices on
    /// one account may want different languages. Thai is the default.
    /// </summary>
    public static class LanguageSetting
    {
        const string Key = "p2d.language";

        /// <summary>Tests pin the language so they never read or write the real PlayerPrefs.</summary>
        public static Language? Override;

        public static Language Stored
        {
            get
            {
                if (Override.HasValue) return Override.Value;
                return PlayerPrefs.GetInt(Key, (int)Language.Thai) == (int)Language.English ? Language.English : Language.Thai;
            }
        }

        /// <summary>Sets Loc.Current from the setting; call before building any text.</summary>
        public static void Apply() => Loc.Current = Stored;

        public static void Set(Language language)
        {
            Loc.Current = language;
            if (Override.HasValue)
            {
                Override = language;
                return;
            }
            PlayerPrefs.SetInt(Key, (int)language);
            PlayerPrefs.Save();
        }
    }
}
