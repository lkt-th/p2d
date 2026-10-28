using System;

namespace P2D.Domain
{
    public struct DamageInput
    {
        public double attack;
        public double skillMultiplier;
        public int attackerLevel;
        public double targetDefense;
        public double elementDamageBonus;
        public Element attackerElement;
        public Element targetElement;
        public double critRate;
        public double critDamage;
        public double extraMultiplier;

        /// <summary>Added to ATK × multiplier before defense (e.g. a share of max HP).</summary>
        public double flatBonus;
    }

    /// <summary>Damage and timing formulas — docs/Balance.md §2.</summary>
    public static class CombatFormulas
    {
        public const double Advantage = 1.3;
        public const double Disadvantage = 0.8;
        public const double BaseAttackInterval = 1.6;
        public const double MaxAutoDodge = 0.25;
        public const double MaxTotalDodge = 0.40;

        public static double DefenseFactor(int attackerLevel, double defense)
        {
            double k = 500 + 10 * attackerLevel;
            return k / (k + Math.Max(0, defense));
        }

        /// <summary>Cycle: Fire → Wind → Earth → Water → Fire. Light and Dark beat each other.</summary>
        public static double ElementMultiplier(Element attacker, Element target)
        {
            if (Beats(attacker, target)) return Advantage;
            if (Beats(target, attacker) && !IsLightDark(attacker)) return Disadvantage;
            return 1.0;
        }

        static bool IsLightDark(Element e) => e == Element.Light || e == Element.Dark;

        static bool Beats(Element a, Element b)
        {
            switch (a)
            {
                case Element.Fire: return b == Element.Wind;
                case Element.Wind: return b == Element.Earth;
                case Element.Earth: return b == Element.Water;
                case Element.Water: return b == Element.Fire;
                case Element.Light: return b == Element.Dark;
                case Element.Dark: return b == Element.Light;
                default: return false;
            }
        }

        /// <summary>Expected-value damage without the crit roll or ±5% spread.</summary>
        public static double BaseDamage(DamageInput input)
        {
            double extra = input.extraMultiplier <= 0 ? 1.0 : input.extraMultiplier;
            return (input.attack * input.skillMultiplier + input.flatBonus)
                * DefenseFactor(input.attackerLevel, input.targetDefense)
                * (1 + input.elementDamageBonus)
                * ElementMultiplier(input.attackerElement, input.targetElement)
                * extra;
        }

        public static double RollDamage(DamageInput input, IRandom random, out bool crit)
        {
            crit = random.NextDouble() < input.critRate;
            double spread = 0.95 + random.NextDouble() * 0.10;
            return BaseDamage(input) * (crit ? input.critDamage : 1.0) * spread;
        }

        public static double AttackInterval(double speed) => BaseAttackInterval * 100 / Math.Max(1, speed);

        public static double Cooldown(double baseCooldown, double skillHaste) =>
            baseCooldown * 100 / (100 + Math.Max(0, skillHaste));

        public static double AutoDodgeChance(double speed, double enemySpeed, double traitBonus)
        {
            double auto = Math.Max(0, Math.Min(MaxAutoDodge, (speed - enemySpeed) * 0.002));
            return Math.Min(MaxTotalDodge, auto + traitBonus);
        }
    }
}
