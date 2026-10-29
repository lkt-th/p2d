using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    public enum SpireAffix { ThemeSurge, ThickHide, Mist, RiderBlessing, Rush }

    /// <summary>
    /// Weekly spire affixes (GDD §6.3, Balance §5.9): the theme affix every week plus one positive and
    /// one negative that rotate with the week. They change spire battles only (not story or hunting)
    /// and are applied after the normal battle setup, so the balance baseline stays affix-free.
    /// The GDD's elite split, path +1 tier and floor-clear heal are not in yet.
    /// </summary>
    public static class SpireAffixes
    {
        public const double ThemeAttackBonus = 0.10;
        public const double ThickHideDefense = 0.15;
        public const double ThickHideSlow = 0.10;
        public const double MistCritRate = 0.10;
        public const double BlessingRatio = 0.25;
        public const double RushSeconds = 75;
        public const double RushRewardBonus = 0.20;

        static readonly SpireAffix[] Positive = { SpireAffix.RiderBlessing, SpireAffix.Rush };
        static readonly SpireAffix[] Negative = { SpireAffix.ThickHide, SpireAffix.Mist };

        /// <summary>
        /// Off until the numbers are signed off: with them the B2 boss and the B4 Bastion boss get much
        /// harder (balance-report.md, "Affix"); softened in round 7. The simulator can run them either way.
        /// </summary>
        public static bool Live = false;

        /// <summary>This week's affixes in live spire battles, or null while affixes are off.</summary>
        public static SpireAffix[] Current(PlayerSave save) => Live ? ForWeek(save.spire.weekIndex) : null;

        static int Mod(int a, int n) => ((a % n) + n) % n;

        public static SpireAffix[] ForWeek(int weekIndex) =>
            new[] { SpireAffix.ThemeSurge, Positive[Mod(weekIndex, Positive.Length)], Negative[Mod(weekIndex / 2, Negative.Length)] };

        public static bool Has(SpireAffix[] affixes, SpireAffix affix) => affixes != null && Array.IndexOf(affixes, affix) >= 0;

        /// <summary>Rush shortens normal and guardian floors only; the boss keeps its 120 s.</summary>
        public static bool RushApplies(int floor) => SpireRules.TypeOf(floor) != FloorType.Boss;

        /// <summary>Changes a built spire battle for this week's affixes.</summary>
        public static void Apply(BattleSetup setup, Loadout loadout, SpireAffix[] affixes, int floor)
        {
            if (affixes == null) return;
            if (Has(affixes, SpireAffix.RiderBlessing) && loadout.mainRider != null)
            {
                var sheet = LoadoutCalculator.Compute(loadout);
                sheet.AddRange(RiderRules.Buffs(loadout.mainRider, loadout.mainRiderState, BlessingRatio));
                setup.dragon = DragonCombatStats.FromSheet(sheet, setup.dragon.dodgeBonus);
            }
            if (Has(affixes, SpireAffix.Mist)) setup.dragon.critRate = Math.Max(0, setup.dragon.critRate - MistCritRate);
            foreach (var e in setup.enemies)
            {
                if (Has(affixes, SpireAffix.ThemeSurge)) e.attack *= 1 + ThemeAttackBonus;
                if (Has(affixes, SpireAffix.ThickHide))
                {
                    e.defense *= 1 + ThickHideDefense;
                    e.attackIntervalSeconds /= 1 - ThickHideSlow;
                }
            }
            if (Has(affixes, SpireAffix.Rush) && RushApplies(floor)) setup.timeLimitSeconds = Math.Min(setup.timeLimitSeconds, RushSeconds);
        }

        /// <summary>Rush pays 20% more first-clear materials on the floors it shortens.</summary>
        public static FloorRewards Boost(SpireAffix[] affixes, int floor, FloorRewards rewards)
        {
            if (!Has(affixes, SpireAffix.Rush) || !RushApplies(floor)) return rewards;
            double m = 1 + RushRewardBonus;
            rewards.gold = (long)Math.Floor(rewards.gold * m);
            rewards.forgeStones = (int)Math.Floor(rewards.forgeStones * m);
            rewards.dragonEssence = (int)Math.Floor(rewards.dragonEssence * m);
            rewards.elementCrystals = (int)Math.Floor(rewards.elementCrystals * m);
            return rewards;
        }

        public static string Name(SpireAffix affix)
        {
            switch (affix)
            {
                case SpireAffix.ThemeSurge: return Loc.T("Theme surge");
                case SpireAffix.ThickHide: return Loc.T("Thick hide");
                case SpireAffix.Mist: return Loc.T("Veil of mist");
                case SpireAffix.RiderBlessing: return Loc.T("Rider's blessing");
                default: return Loc.T("Rush");
            }
        }

        public static string Describe(SpireAffix affix)
        {
            switch (affix)
            {
                case SpireAffix.ThemeSurge: return Loc.T("Enemies ATK +10%");
                case SpireAffix.ThickHide: return Loc.T("Enemies DEF +15%, attack 10% slower");
                case SpireAffix.Mist: return Loc.T("Dragon CRIT Rate −10%");
                case SpireAffix.RiderBlessing: return Loc.T("Main rider's pact buffs +25%");
                default: return Loc.T("75 s limit on every floor but the boss, +20% first-clear materials");
            }
        }

        public static bool IsPositive(SpireAffix affix) => affix == SpireAffix.RiderBlessing || affix == SpireAffix.Rush;
    }
}
