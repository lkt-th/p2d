using System.Collections.Generic;

namespace P2D.Domain
{
    public enum Feature { Enhance, Riders, Hunt, Spire, Workshop }

    /// <summary>
    /// Features open as story stages are cleared (docs/GDD.md §11, Balance §14.1). The rule lives in
    /// Domain so the server can refuse a locked action; screens only mirror it.
    /// </summary>
    public static class FeatureUnlocks
    {
        /// <summary>Story stage (index across chapters) whose first clear opens the feature.</summary>
        public static int UnlockStage(Feature feature)
        {
            switch (feature)
            {
                case Feature.Enhance: return 6;   // 1-6
                case Feature.Riders: return 10;   // 1-10
                case Feature.Hunt: return 10;     // 1-10
                case Feature.Spire: return 15;    // 2-5
                default: return 20;               // Workshop (reforge + refine): finishing chapter 2 opens chapter 3
            }
        }

        public static bool IsUnlocked(PlayerSave save, Feature feature) =>
            save.story != null && save.story.cleared >= UnlockStage(feature);

        public static string Name(Feature feature)
        {
            switch (feature)
            {
                case Feature.Enhance: return "Enhance";
                case Feature.Riders: return "Riders & Summon";
                case Feature.Hunt: return "Hunting Grounds";
                case Feature.Spire: return "Weekly Spire";
                default: return "Workshop (reforge & refine)";
            }
        }

        /// <summary>"Clear story 1-6" for locked buttons.</summary>
        public static string Hint(Feature feature) => "Clear story " + StoryActions.Label(UnlockStage(feature));

        public static ActionResult Locked(Feature feature) => ActionResult.Fail(Name(feature) + " is locked: " + Hint(feature).ToLowerInvariant());

        /// <summary>Features whose unlock stage is exactly <paramref name="index"/>.</summary>
        public static List<Feature> OpenedBy(int index)
        {
            var list = new List<Feature>();
            foreach (Feature f in System.Enum.GetValues(typeof(Feature)))
                if (UnlockStage(f) == index) list.Add(f);
            return list;
        }
    }
}
