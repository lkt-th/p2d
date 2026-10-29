using System.Collections.Generic;

namespace P2D.Domain
{
    public struct DialogueLine
    {
        public string speaker;
        public string text;

        public DialogueLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }

    /// <summary>
    /// Short scenes played before story stages (GDD §2 lore: the dragon outlives its riders, so the
    /// pact passes from rider to rider). "{dragon}" is replaced with the player's dragon name.
    /// English for now; Thai comes with the localization pass.
    /// </summary>
    public static class StoryScript
    {
        public const string DragonToken = "{dragon}";

        const string Maren = "Elder Maren";
        const string Tim = "Tim";
        const string Mina = "Mina";
        const string Lira = "Lira";
        const string Brogan = "Brogan";

        static readonly Dictionary<int, DialogueLine[]> Scenes = new Dictionary<int, DialogueLine[]>
        {
            [1] = new[]
            {
                new DialogueLine(Maren, "The egg chose you, child. A dragon outlives every rider, so the pact passes hand to hand."),
                new DialogueLine(Tim, "Then I'll hold it as long as I can. Come on, {dragon}, the wild drakes are at the cradle wall!"),
                new DialogueLine(Maren, "Stay close to it. A hatchling learns by watching its rider."),
            },
            [5] = new[]
            {
                new DialogueLine(Tim, "That hound is wreathed in cinders. Watch for the glow, it means a heavy bite."),
                new DialogueLine(Maren, "When it flares, swipe to dodge. Water in {dragon}'s heart beats fire."),
            },
            [6] = new[]
            {
                new DialogueLine(Maren, "The guardian's gear is yours. Bring forge stones to the anvil and temper it."),
                new DialogueLine(Tim, "Each blow pulls the gear toward a path. Pick the one you want {dragon} to grow into."),
            },
            [10] = new[]
            {
                new DialogueLine(Tim, "Ashmaw. It has burned every nest in this valley."),
                new DialogueLine(Mina, "Then it won't burn this one. I've followed you from the village, Tim. Let me ride beside you."),
                new DialogueLine(Maren, "Beat it and the riders' hall will open to you. The pact grows with every rider who joins it."),
            },
            [11] = new[]
            {
                new DialogueLine(Mina, "The steppe winds never stop. The drakes here strike fast and leave faster."),
                new DialogueLine(Tim, "Feed {dragon} well before we go deeper. It's still small for this place."),
            },
            [15] = new[]
            {
                new DialogueLine(Maren, "Beyond this harpy stands the Spire. Its floors change every week."),
                new DialogueLine(Mina, "Ancient scales sleep at the top. Without them {dragon} can never grow up."),
            },
            [20] = new[]
            {
                new DialogueLine(Lira, "Stormcrown rides the gale like a throne. I've chased it for three summers."),
                new DialogueLine(Lira, "Help me ground it, and my wind is yours."),
            },
            [21] = new[]
            {
                new DialogueLine(Maren, "The coast. Old gear washes up here, still humming with its first owner's path."),
                new DialogueLine(Lira, "The smiths at Tidecall can re-forge a path and refine what you've got. Nothing has to stay the way it was."),
            },
            [25] = new[]
            {
                new DialogueLine(Tim, "A reef crab the size of a house. Its shell turns most blows."),
                new DialogueLine(Lira, "Then don't trade blows. Use {dragon}'s skills and dodge the slam."),
            },
            [30] = new[]
            {
                new DialogueLine(Brogan, "You're the ones who grounded Stormcrown? Good. The Abyssal Maw is waking."),
                new DialogueLine(Brogan, "I swore an old pact with a dragon long gone. Let me keep it with {dragon}."),
                new DialogueLine(Maren, "A dragon outlives its riders, but its riders make it what it becomes. Go."),
            },
        };

        static readonly DialogueLine[] None = new DialogueLine[0];

        /// <summary>Scene before the stage (empty when it has none), with the dragon's name filled in.</summary>
        public static DialogueLine[] Before(int index, string dragonName)
        {
            DialogueLine[] scene;
            if (!Scenes.TryGetValue(index, out scene)) return None;
            var lines = new DialogueLine[scene.Length];
            for (int i = 0; i < scene.Length; i++)
                lines[i] = new DialogueLine(scene[i].speaker, scene[i].text.Replace(DragonToken, dragonName));
            return lines;
        }

        /// <summary>The scene plays the first time the player fights the stage; retries skip it.</summary>
        public static bool ShouldPlay(PlayerSave save, int index) =>
            Before(index, save.dragon.name).Length > 0 && save.story.dialogueSeen < index;

        public static void MarkSeen(PlayerSave save, int index)
        {
            if (save.story.dialogueSeen < index) save.story.dialogueSeen = index;
        }
    }
}
