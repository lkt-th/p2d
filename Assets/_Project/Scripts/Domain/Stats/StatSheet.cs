using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>
    /// Final = (Base + ΣFlat) × (1 + ΣPercent) — see docs/Balance.md §2.3.
    /// All percent sources (gear, riders, traits, sets) share one additive bucket.
    /// </summary>
    public sealed class StatSheet
    {
        static readonly int Count = Enum.GetValues(typeof(StatType)).Length;

        readonly double[] baseValues = new double[Count];
        readonly double[] flat = new double[Count];
        readonly double[] percent = new double[Count];

        public void SetBase(StatType stat, double value) => baseValues[(int)stat] = value;

        /// <summary>
        /// Stats that are already rates (CRIT 5%, CRIT DMG 150%, Element DMG …). A "+15%" on these
        /// adds 15 points (150% → 165%) instead of multiplying the base.
        /// </summary>
        public static bool IsRateStat(StatType stat) =>
            stat == StatType.CritRate || stat == StatType.CritDmg || stat == StatType.ElementDmg || stat == StatType.Resist;

        public void Add(StatModifier modifier)
        {
            if (modifier.type == ModType.Flat || IsRateStat(modifier.stat)) flat[(int)modifier.stat] += modifier.value;
            else percent[(int)modifier.stat] += modifier.value;
        }

        public void AddRange(IEnumerable<StatModifier> modifiers)
        {
            foreach (var m in modifiers) Add(m);
        }

        public double Get(StatType stat)
        {
            int i = (int)stat;
            return (baseValues[i] + flat[i]) * (1.0 + percent[i]);
        }

        /// <summary>
        /// Display-only power score (docs/Balance.md §2.4). Never used in combat math.
        /// </summary>
        public double PowerScore()
        {
            double atk = Get(StatType.ATK);
            return atk
                + Get(StatType.HP) * 0.1
                + Get(StatType.DEF) * 1.5
                + (Get(StatType.SPD) - 100) * 20
                + atk * (Get(StatType.CritRate) * (Get(StatType.CritDmg) - 1))
                + atk * Get(StatType.ElementDmg) * 0.8;
        }
    }
}
