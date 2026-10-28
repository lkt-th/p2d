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
    }
}
