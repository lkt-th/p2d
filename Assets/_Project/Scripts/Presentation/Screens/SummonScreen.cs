using System.Collections.Generic;
using P2D.Domain;
using P2D.Presentation.UI;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>Pact Summon: rates and pity counters, x1 / x10, and the last results.</summary>
    public sealed class SummonScreen : GameScreen
    {
        RectTransform list;
        List<SummonOutcome> last = new List<SummonOutcome>();

        protected override void Build() => list = UiKit.ScrollList(Root);

        public override void Refresh()
        {
            UiKit.Clear(list);
            var save = Save;
            var pity = save.summonPity;

            var head = UiKit.Row(list, 170, new Color(0.17f, 0.09f, 0.13f));
            Cell(head, "<b>Pact Summon</b>   sigils " + save.wallet.pactSigils
                + "\nRates  R 60% · SR 32% · SSR 7% · UR 1%   ·   10 pulls guarantee SR+"
                + "\nSSR+ within " + (SummonRules.SsrPity - pity.sinceSsr) + " · UR within " + (SummonRules.UrHardPity - pity.sinceUr)
                + (pity.featuredGuaranteed ? " · next UR is featured" : ""), 0.02f, 1, 28);

            var buttons = UiKit.Row(list, 140);
            CellButton(buttons, "SUMMON x1\n1 sigil", 0, 0.5f, () => Pull(1), save.wallet.pactSigils >= 1);
            CellButton(buttons, "SUMMON x10\n10 sigils", 0.5f, 1, () => Pull(10), save.wallet.pactSigils >= 10, UiKit.Accent);

            if (last.Count == 0) return;
            Header(list, "Results");
            foreach (var r in last)
            {
                var row = UiKit.Row(list, 70, UiKit.Surface);
                string tag = r.isNew ? "<color=#e9c46a>NEW!</color>" : r.shards > 0 ? "+" + r.shards + " shards" : "copy +1";
                Cell(row, "<b>" + r.rank + "</b>  " + r.riderId, 0.02f, 0.6f, 30, TextAnchor.MiddleLeft, RankColor(r.rank));
                Cell(row, tag, 0.6f, 0.98f, 28, TextAnchor.MiddleRight);
            }
        }

        void Pull(int count)
        {
            last = GameActions.Summon(Save, count, Menu.Random);
            Menu.Commit(last.Count > 0 ? ActionResult.Ok("Summoned " + last.Count) : ActionResult.Fail("Not enough sigils"));
        }

        static Color RankColor(RiderRank rank)
        {
            switch (rank)
            {
                case RiderRank.UR: return new Color(0.9f, 0.33f, 0.29f);
                case RiderRank.SSR: return new Color(0.94f, 0.63f, 0.29f);
                case RiderRank.SR: return new Color(0.35f, 0.66f, 0.9f);
                default: return new Color(0.78f, 0.8f, 0.84f);
            }
        }
    }
}
