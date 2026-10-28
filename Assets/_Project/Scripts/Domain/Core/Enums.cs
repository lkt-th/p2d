namespace P2D.Domain
{
    public enum StatType { HP, ATK, DEF, SPD, CritRate, CritDmg, ElementDmg, SkillHaste, Resist }

    public enum ModType { Flat, Percent }

    public enum GrowthPath { Fury, Bastion, Tempest, Arcane }

    public enum Element { Fire, Water, Wind, Earth, Light, Dark }

    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

    public enum RiderRank { R, SR, SSR, UR }

    public enum EquipSlot { HeartCore, Claw, Chest, Horn, Wing, Tail }

    public enum EncounterKind { Normal, Rare, Epic }

    public enum FloorType { Normal, Guardian, Boss, Endless }
}
