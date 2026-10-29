using System;

namespace P2D.Domain
{
    [Serializable]
    public sealed class EquipmentState
    {
        public string uid;
        public string definitionId;
        public EquipSlot slot;
        public Rarity rarity;
        public GrowthPath path;
        public string setId;
        public int enhance;
        public StatModifier main;
        public StatModifier[] subs = new StatModifier[0];

        /// <summary>Breath element of a Heart Core (GDD §3.5); unused on other slots.</summary>
        public Element element;
    }
}
