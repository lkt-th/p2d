using P2D.Domain;
using UnityEngine;

namespace P2D.Data
{
    [CreateAssetMenu(menuName = "P2D/Rider Definition", fileName = "Rider_")]
    public sealed class RiderDefinition : ScriptableObject
    {
        public string id;
        public string displayNameKey;
        public RiderRank rank;
        public bool hasPath = true;
        public GrowthPath path;
        [Min(0)] public int baseAffinity;

        [Tooltip("Pact buffs at 1★. Percent values are fractions: 0.12 = 12%.")]
        public StatModifier[] pactBuffs = new StatModifier[0];

        [Tooltip("Extra buffs unlocked at 3★, at 1★ strength.")]
        public StatModifier[] star3Trait = new StatModifier[0];

        [Header("Hunter trait (Hunting Grounds only)")]
        [Range(0, 0.5f)] public float huntBase;
        [Min(0)] public float huntEscapeDelaySeconds;

        [Header("UR legacy")]
        public bool legacyLowersResonance;
        [Range(0, 1)] public float legacyHuntRefundChance;

        public string commandSkillId;
        public Sprite portrait;

        public RiderProfile ToProfile() => new RiderProfile
        {
            id = id,
            rank = rank,
            hasPath = hasPath,
            path = path,
            baseAffinity = baseAffinity,
            pactBuffs = pactBuffs,
            star3Trait = star3Trait,
            huntBase = huntBase,
            huntEscapeDelaySeconds = huntEscapeDelaySeconds,
            legacyLowersResonance = legacyLowersResonance,
            legacyHuntRefundChance = legacyHuntRefundChance,
            commandSkillId = commandSkillId,
        };
    }
}
