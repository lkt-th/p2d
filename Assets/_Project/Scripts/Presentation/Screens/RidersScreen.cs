using System.Collections.Generic;
using P2D.Domain;
using P2D.Presentation.UI;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>Owned riders: buffs at their star, ride (switch main rider) and star up.</summary>
    public sealed class RidersScreen : GameScreen
    {
        RectTransform list;

        protected override void Build() => list = UiKit.ScrollList(Root);

        public override void Refresh()
        {
            UiKit.Clear(list);
            var save = Save;
            if (LockedCard(list, Feature.Riders)) return;
            var seals = save.wallet.soulSeals;
            var info = UiKit.Row(list, 90, UiKit.Surface);
            Cell(info, Loc.F("Soul seals  R {0} · SR {1} · SSR {2} · UR {3}    Shards {4}", seals[0], seals[1], seals[2], seals[3],
                save.wallet.soulShards), 0.02f, 0.72f, 28);
            CellButton(info, Loc.T("SUMMON"), 0.74f, 1, () => Menu.Show(ScreenId.Summon), true, UiKit.Accent);

            var owned = new List<RiderState>(save.riders);
            owned.Sort((a, b) =>
            {
                var pa = RiderCatalog.Find(a.riderId);
                var pb = RiderCatalog.Find(b.riderId);
                int rank = (pb != null ? (int)pb.rank : 0).CompareTo(pa != null ? (int)pa.rank : 0);
                return rank != 0 ? rank : b.star.CompareTo(a.star);
            });

            foreach (var state in owned)
            {
                var profile = RiderCatalog.Find(state.riderId);
                if (profile == null) continue;
                bool riding = save.dragon.mainRiderId == state.riderId;
                var row = UiKit.Row(list, 170, riding ? new Color(0.22f, 0.16f, 0.12f) : UiKit.Surface);

                var buffs = new List<string>();
                foreach (var m in RiderRules.Buffs(profile, state)) buffs.Add(Fmt.Stat(m));
                if (profile.hasPath) buffs.Add(Loc.Enum(profile.path) + " +" + RiderRules.Affinity(profile, state));
                if (profile.huntBase > 0) buffs.Add(Loc.F("Hunt +{0}%", (RiderRules.HuntBonus(profile, state) * 100).ToString("0.#")));
                var command = RiderCatalog.Command(profile.commandSkillId);

                Cell(row, "<b>" + Loc.Rider(state.riderId) + "</b>  " + profile.rank + "  " + Fmt.Stars(state.star)
                    + "   " + Loc.F("copies {0}", state.copies) + "\n" + string.Join(", ", buffs.ToArray())
                    + (command != null ? "\n<color=#a3a8ba>" + Loc.F("Command: {0}", Loc.Skill(command.id)) + "</color>" : ""), 0.02f, 0.6f, 26);

                string id = state.riderId;
                CellButton(row, riding ? Loc.T("RIDING") : Loc.T("RIDE"), 0.61f, 0.8f,
                    () => Menu.Commit(GameActions.SetMainRider(save, id), Loc.F("Now riding {0}", Loc.Rider(id))), !riding);

                bool maxed = state.star >= RiderRules.MaxStar;
                string starText = maxed ? "5*" : Loc.T("STAR UP") + "\n" + Loc.F("{0} gold", Fmt.Num(GameActions.StarUpGold(profile.rank, state.star + 1)));
                CellButton(row, starText, 0.8f, 1, () => Menu.Commit(GameActions.StarUp(save, id)),
                    !maxed && RiderRules.CanStarUp(state, seals[(int)profile.rank]), UiKit.SurfaceAlt);
            }
        }
    }
}
