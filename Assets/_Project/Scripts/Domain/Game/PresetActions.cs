using System;

namespace P2D.Domain
{
    /// <summary>
    /// Loadout presets (GDD §4.7): save the equipped gear, skills and main rider into one of three
    /// slots and switch back with one tap. Opens with the workshop (story 2-10, GDD §11 step 7).
    /// </summary>
    public static class PresetActions
    {
        public const int Slots = 3;

        static LoadoutPresetSave Slot(PlayerSave save, int index)
        {
            while (save.presets.Count < Slots) save.presets.Add(new LoadoutPresetSave { name = "" });
            return index >= 0 && index < save.presets.Count ? save.presets[index] : null;
        }

        public static bool IsEmpty(LoadoutPresetSave preset)
        {
            if (preset == null) return true;
            foreach (var uid in preset.equipped) if (!string.IsNullOrEmpty(uid)) return false;
            return string.IsNullOrEmpty(preset.mainRiderId);
        }

        /// <summary>Display name: the stored one, or "Set N".</summary>
        public static string Name(PlayerSave save, int index)
        {
            var preset = Slot(save, index);
            return preset != null && !string.IsNullOrEmpty(preset.name) && !preset.name.StartsWith("ชุด ")
                ? preset.name : Loc.F("Set {0}", index + 1);
        }

        public static ActionResult Store(PlayerSave save, int index)
        {
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Workshop)) return FeatureUnlocks.Locked(Feature.Workshop);
            var preset = Slot(save, index);
            if (preset == null) return ActionResult.Fail(Loc.T("No such preset"));
            preset.equipped = (string[])save.dragon.equipped.Clone();
            preset.skillIds = (string[])save.dragon.skillIds.Clone();
            preset.mainRiderId = save.dragon.mainRiderId;
            return ActionResult.Ok(Loc.F("Saved to {0}", Name(save, index)));
        }

        /// <summary>
        /// Equips a stored preset. Items that were salvaged since, or whose slot is locked, are skipped;
        /// the rider is used only if still owned; skills fall back to auto-pick when they no longer fit.
        /// </summary>
        public static ActionResult Apply(PlayerSave save, int index)
        {
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Workshop)) return FeatureUnlocks.Locked(Feature.Workshop);
            var preset = Slot(save, index);
            if (IsEmpty(preset)) return ActionResult.Fail(Loc.T("This preset is empty"));

            int missing = 0;
            for (int slot = 0; slot < save.dragon.equipped.Length && slot < preset.equipped.Length; slot++)
            {
                string uid = preset.equipped[slot];
                if (string.IsNullOrEmpty(uid)) continue;
                var item = save.FindEquipment(uid);
                if (item == null || !DragonRules.IsSlotUnlocked(item.slot, save.dragon.stage)) { missing++; continue; }
                save.dragon.equipped[slot] = uid;
            }
            if (!string.IsNullOrEmpty(preset.mainRiderId) && save.FindRider(preset.mainRiderId) != null)
                save.dragon.mainRiderId = preset.mainRiderId;

            Array.Copy(preset.skillIds, save.dragon.skillIds, Math.Min(preset.skillIds.Length, save.dragon.skillIds.Length));
            var affinity = LoadoutCalculator.Affinity(GameActions.ToLoadout(save));
            if (SkillLoadoutRules.Validate(save.dragon.skillIds, affinity, save.dragon.stage) != null) GameActions.AutoSkills(save);

            return ActionResult.Ok(missing > 0 ? Loc.F("Equipped {0} ({1} items missing)", Name(save, index), missing) : Loc.F("Equipped {0}", Name(save, index)));
        }
    }
}
