using System.Collections.Generic;
using P2D.Domain;
using UnityEngine;

namespace P2D.Presentation.Battle
{
    /// <summary>
    /// How the greybox dragon looks (GDD §3: skin and shape follow stage and Growth Path). Pure data
    /// from stage + the dominant path (highest affinity) + its tier; <see cref="DragonFigure"/> builds
    /// it from 3D primitives, <see cref="UI.DragonPortrait"/> from uGUI images. Visual only — nothing
    /// here feeds back into the rules or the battle.
    /// </summary>
    public sealed class DragonLook
    {
        public int stage = 1;
        public GrowthPath path = GrowthPath.Fury;
        /// <summary>Affinity tier of the dominant path, 0–4.</summary>
        public int tier;

        public Color body;
        public Color belly;
        public Color wing;
        public Color accent;

        /// <summary>Overall size; grows with every stage (1.0 at Hatchling).</summary>
        public float scale = 1f;
        /// <summary>Body width relative to the base (Bastion is bulkier).</summary>
        public float bulk = 1f;
        public float wingLength = 1f;
        public float clawSize = 1f;
        public float hornSize = 1f;

        // Parts appear when their gear slot unlocks (DragonRules.UnlockStage).
        public bool horns;
        public bool wings;
        public bool tail;

        /// <summary>Bastion: a stone plate along the back.</summary>
        public bool backPlate;
        /// <summary>Arcane: a glowing orb floats beside the dragon.</summary>
        public bool orb;
        /// <summary>Tier II+: a crest of the accent colour on the head.</summary>
        public bool crest;
        /// <summary>Tier III+: the accent parts glow.</summary>
        public bool glow;
        /// <summary>No path affinity yet: the hatchling-blue skin.</summary>
        public bool plain;

        public static float StageScale(int stage) => 1f + 0.22f * (Mathf.Clamp(stage, 1, DragonRules.MaxStage) - 1);

        public static DragonLook For(PlayerSave save) => For(GameActions.ToLoadout(save));

        public static DragonLook For(Loadout loadout) => For(loadout.stage, LoadoutCalculator.Affinity(loadout));

        /// <summary>The look at <paramref name="stage"/> for this affinity (the dominant path picks the skin).</summary>
        public static DragonLook For(int stage, Dictionary<GrowthPath, int> affinity)
        {
            var path = AffinityRules.Dominant(affinity);
            int value = affinity[path];
            // No gear yet (all zero): a plain hatchling-blue dragon, no path skin.
            return value <= 0 ? Plain(stage) : For(stage, path, AffinityRules.Tier(value));
        }

        public static DragonLook For(int stage, GrowthPath path, int tier)
        {
            stage = Mathf.Clamp(stage, 1, DragonRules.MaxStage);
            var look = Base(stage);
            look.path = path;
            look.tier = Mathf.Clamp(tier, 0, AffinityRules.TierThresholds.Length);
            switch (path)
            {
                case GrowthPath.Fury:
                    look.body = new Color(0.82f, 0.26f, 0.18f);
                    look.belly = new Color(0.98f, 0.62f, 0.3f);
                    look.wing = new Color(0.55f, 0.14f, 0.1f);
                    look.accent = new Color(1f, 0.78f, 0.25f);
                    look.clawSize = 1.6f;
                    look.hornSize = 1.6f;
                    break;
                case GrowthPath.Bastion:
                    look.body = new Color(0.7f, 0.6f, 0.42f);
                    look.belly = new Color(0.86f, 0.79f, 0.62f);
                    look.wing = new Color(0.5f, 0.46f, 0.4f);
                    look.accent = new Color(0.62f, 0.66f, 0.7f);
                    look.bulk = 1.35f;
                    look.wingLength = 0.85f;
                    look.backPlate = true;
                    break;
                case GrowthPath.Tempest:
                    look.body = new Color(0.18f, 0.66f, 0.62f);
                    look.belly = new Color(0.62f, 0.92f, 0.86f);
                    look.wing = new Color(0.12f, 0.46f, 0.52f);
                    look.accent = new Color(0.75f, 1f, 0.95f);
                    look.bulk = 0.85f;
                    look.wingLength = 1.6f;
                    break;
                default:
                    look.body = new Color(0.48f, 0.34f, 0.78f);
                    look.belly = new Color(0.78f, 0.7f, 0.98f);
                    look.wing = new Color(0.3f, 0.2f, 0.55f);
                    look.accent = new Color(0.55f, 0.95f, 1f);
                    look.orb = true;
                    break;
            }
            look.crest = look.tier >= 2;
            look.glow = look.tier >= 3;
            return look;
        }

        public static DragonLook Plain(int stage)
        {
            var look = Base(Mathf.Clamp(stage, 1, DragonRules.MaxStage));
            look.plain = true;
            look.body = new Color(0.3f, 0.55f, 0.9f);
            look.belly = new Color(0.7f, 0.82f, 0.98f);
            look.wing = new Color(0.2f, 0.32f, 0.6f);
            look.accent = new Color(0.95f, 0.9f, 0.6f);
            return look;
        }

        static DragonLook Base(int stage) => new DragonLook
        {
            stage = stage,
            scale = StageScale(stage),
            horns = DragonRules.IsSlotUnlocked(EquipSlot.Horn, stage),
            wings = DragonRules.IsSlotUnlocked(EquipSlot.Wing, stage),
            tail = DragonRules.IsSlotUnlocked(EquipSlot.Tail, stage),
        };

        /// <summary>The same skin at another stage (for the evolution cutscene).</summary>
        public DragonLook AtStage(int newStage) => plain ? Plain(newStage) : For(newStage, path, tier);

        /// <summary>The accent colour, brightened when the tier makes it glow.</summary>
        public Color AccentShown => glow ? Color.Lerp(accent, Color.white, 0.35f) : accent;
    }
}
