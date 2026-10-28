using System;

namespace P2D.Domain
{
    public struct SpireTheme
    {
        public string id;
        public string name;
        public Element element;
        public string boss;
    }

    /// <summary>The four rotating weekly themes (docs/GDD.md §6.2). Remote Config will pick the live one.</summary>
    public static class SpireThemes
    {
        public static readonly SpireTheme[] All =
        {
            new SpireTheme { id = "volcano", name = "Roaring Volcano", element = Element.Fire, boss = "Ignar the Lava King" },
            new SpireTheme { id = "glacier", name = "Eternal Glacier", element = Element.Water, boss = "Hydroscale" },
            new SpireTheme { id = "thunder", name = "Thunder Peak", element = Element.Wind, boss = "Voltra" },
            new SpireTheme { id = "starstone", name = "Starstone Forest", element = Element.Earth, boss = "Galaxy Golem" },
        };

        public static SpireTheme ForWeek(int weekIndex) => All[WeeklyCalendar.FallbackThemeIndex(weekIndex, All.Length)];

        public static SpireTheme ForTime(DateTime utcNow) => ForWeek(WeeklyCalendar.WeekIndex(utcNow));

        /// <summary>The element that beats this theme's enemies (the spire screen's advice).</summary>
        public static Element Counter(Element enemy)
        {
            switch (enemy)
            {
                case Element.Fire: return Element.Water;
                case Element.Water: return Element.Earth;
                case Element.Earth: return Element.Wind;
                case Element.Wind: return Element.Fire;
                case Element.Light: return Element.Dark;
                default: return Element.Light;
            }
        }
    }
}
