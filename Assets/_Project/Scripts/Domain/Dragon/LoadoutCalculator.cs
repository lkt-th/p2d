using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>Everything that feeds the dragon's final stats.</summary>
    public sealed class Loadout
    {
        public int level = 1;
        public int stage = 1;
        public List<EquipmentState> equipped = new List<EquipmentState>();
        public RiderProfile mainRider;
        public RiderState mainRiderState;
        public List<RiderProfile> supportRiders = new List<RiderProfile>();
        public List<RiderState> supportRiderStates = new List<RiderState>();
    }

    /// <summary>
    /// Order: dragon base (level × stage) → gear main/sub → main rider ×1.0 → supports ×0.3.
    /// Path traits and set bonuses are added by the caller once their data exists.
    /// Recompute only when the loadout changes, never per frame.
    /// </summary>
    public static class LoadoutCalculator
    {
        public static StatSheet Compute(Loadout loadout)
        {
            var sheet = DragonRules.BaseSheet(loadout.level, loadout.stage);

            foreach (var item in loadout.equipped)
            {
                if (item == null || !DragonRules.IsSlotUnlocked(item.slot, loadout.stage)) continue;
                sheet.Add(EquipmentRules.MainModifier(item));
                sheet.AddRange(item.subs);
            }

            if (loadout.mainRider != null)
                sheet.AddRange(RiderRules.Buffs(loadout.mainRider, loadout.mainRiderState));

            int supportSlots = DragonRules.SupportRiderSlots(loadout.stage);
            for (int i = 0; i < loadout.supportRiders.Count && i < supportSlots; i++)
                if (loadout.supportRiders[i] != null)
                    sheet.AddRange(RiderRules.Buffs(loadout.supportRiders[i], loadout.supportRiderStates[i], RiderRules.SupportRatio));

            return sheet;
        }

        public static Dictionary<GrowthPath, int> Affinity(Loadout loadout)
        {
            var unlocked = new List<EquipmentState>();
            foreach (var item in loadout.equipped)
                if (item != null && DragonRules.IsSlotUnlocked(item.slot, loadout.stage))
                    unlocked.Add(item);
            return AffinityRules.Compute(unlocked, loadout.mainRider, loadout.mainRiderState);
        }
    }
}
