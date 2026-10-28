using System;

namespace P2D.Domain
{
    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public ModType type;

        /// <summary>Percent values are fractions: 0.12 = 12%.</summary>
        public double value;

        public StatModifier(StatType stat, ModType type, double value)
        {
            this.stat = stat;
            this.type = type;
            this.value = value;
        }

        public StatModifier Scaled(double factor) => new StatModifier(stat, type, value * factor);
    }
}
