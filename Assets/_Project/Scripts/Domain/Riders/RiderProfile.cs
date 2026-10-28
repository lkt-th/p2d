using System;

namespace P2D.Domain
{
    /// <summary>
    /// Static rider data used by the rules. Authored as a RiderDefinition ScriptableObject
    /// in Unity and converted with RiderDefinition.ToProfile().
    /// </summary>
    [Serializable]
    public sealed class RiderProfile
    {
        public string id;
        public RiderRank rank;
        public bool hasPath;
        public GrowthPath path;
        public int baseAffinity;

        /// <summary>Pact buffs at 1★.</summary>
        public StatModifier[] pactBuffs = new StatModifier[0];

        /// <summary>Extra buffs unlocked at 3★, at 1★ strength.</summary>
        public StatModifier[] star3Trait = new StatModifier[0];

        /// <summary>Hunter trait at 1★ (fraction, 0.10 = +10% rare encounter rate). 0 = none.</summary>
        public double huntBase;

        /// <summary>Extra seconds before rare monsters flee in the Hunting Grounds.</summary>
        public double huntEscapeDelaySeconds;

        /// <summary>UR legacy: Path Resonance works at 40 instead of 60.</summary>
        public bool legacyLowersResonance;

        /// <summary>UR legacy: winning a rare hunt may refund its stamina.</summary>
        public double legacyHuntRefundChance;

        public string commandSkillId;
    }

    [Serializable]
    public sealed class RiderState
    {
        public string riderId;
        public int star = 1;

        /// <summary>Unused duplicates waiting to be spent on star-ups.</summary>
        public int copies;
    }
}
