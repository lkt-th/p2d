using System;
using System.Collections.Generic;
using P2D.Domain;

namespace P2D.Presentation.Battle
{
    /// <summary>
    /// Carries a battle from a menu scene into the battle scene and the result back.
    /// The menu sets <see cref="Pending"/> and loads the battle scene; when the player leaves
    /// the result screen, <see cref="Request.onFinished"/> runs and the menu scene reloads.
    /// </summary>
    public static class BattleHandoff
    {
        public const string BattleScene = "GreyboxBattle";
        public const string MenuScene = "Main";

        public sealed class Request
        {
            public BattleSetup setup;
            public string title = "";
            public string returnScene = MenuScene;
            public Action<BattleOutcome, IReadOnlyList<BattleInput>> onFinished;
        }

        public static Request Pending;

        /// <summary>A line for the menu to show after returning (e.g. the rewards earned).</summary>
        public static string LastMessage;
    }
}
