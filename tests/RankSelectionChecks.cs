using YxPrediction;

internal static partial class Program
{
    static void RankSelectionChecks()
    {
        Check("unbought players start unselected and rank buttons wrap without choosing for others", delegate
        {
            MatchSnapshot s = Snapshot(1); Market m = TradeBook(s).Find(s.Id); var choices = new RankSelection(); choices.Observe(m);
            for (int i = 0; i < 8; i++) Equal(choices.Selected(i), 0);
            choices.Step(3, true); Equal(choices.Selected(3), 1);
            choices.Step(3, false); Equal(choices.Selected(3), 8);
            choices.Step(3, true); Equal(choices.Selected(3), 1);
            choices.Step(7, false); Equal(choices.Selected(7), 8);
            for (int i = 0; i < 7; i++) if (i != 3) Equal(choices.Selected(i), 0);
            choices.Step(-1, true); choices.Step(8, false); Equal(choices.Selected(8), 0);
        });
        Check("typed ranks accept one through eight and reject invalid values without changing the draft", delegate
        {
            var choices = new RankSelection();
            True(choices.SelectText(2, "6")); Equal(choices.Selected(2), 6);
            foreach (string text in new string[] { "0", "9", "10", "-1", "1.5", "?", " 2", "2147483648" })
            { True(!choices.SelectText(2, text)); Equal(choices.Selected(2), 6); }
            True(!choices.SelectText(-1, "3")); True(!choices.SelectText(8, "3"));
            True(choices.SelectText(2, "")); Equal(choices.Selected(2), 0);
        });
        Check("buying after both views were opened replaces the stale selection with the purchased rank", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Market m = b.Find(s.Id);
            var single = new RankSelection(); var detail = new RankSelection(); single.Observe(m); detail.Observe(m);
            single.SelectText(6, "3"); detail.SelectText(6, "7");
            Equal(Buy(b, s, 6, single.Selected(6), 10), null);
            single.Observe(m); detail.Observe(m);
            Equal(single.Selected(6), 3); Equal(detail.Selected(6), 3); Equal(detail.Selected(0), 0);
            Equal(RankSelection.DisplayOrder(m)[0], 6);
        });
        Check("quote refresh and partial sales preserve a pending change until switching the position", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Market m = b.Find(s.Id);
            Buy(b, s, 3, 2, 20); var choices = new RankSelection(); choices.Observe(m); Equal(choices.Selected(3), 2);
            choices.SelectText(3, "4"); s.Round = 2; b.Observe(s, null); choices.Observe(m); Equal(choices.Selected(3), 4);
            Equal(Sell(b, s, 3, true), null); choices.Observe(m); Equal(choices.Selected(3), 4);
            Equal(Switch(b, s, 3, 5, 10), null); choices.Observe(m); Equal(choices.Selected(3), 5);
            Equal(m.Positions[3].Rank, 5);
        });
        Check("full liquidation clears both selection and priority and failed purchases do not create a position", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Market m = b.Find(s.Id); Buy(b, s, 5, 2, 20);
            var choices = new RankSelection(); choices.Observe(m); Equal(choices.Selected(5), 2);
            Equal(Sell(b, s, 5, false), null); choices.Observe(m); Equal(choices.Selected(5), 0);
            Equal(RankSelection.DisplayOrder(m)[0], 0);
            choices.SelectText(7, "8"); True(Buy(b, s, 7, 8, 10000) != null); choices.Observe(m);
            Equal(RankSelection.PurchasedRank(m, 7), 0); Equal(choices.Selected(7), 8);
            Equal(RankSelection.DisplayOrder(m)[0], 0);
        });
        Check("switching matches or loading saved positions resets drafts and restores actual purchased ranks", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Buy(b, s, 2, 7, 10);
            var choices = new RankSelection(); choices.Observe(b.Find(s.Id)); choices.SelectText(1, "5");
            b = Reload(b); choices.Observe(b.Find(s.Id)); Equal(choices.Selected(2), 7); Equal(choices.Selected(1), 5);
            MatchSnapshot next = Snapshot(1); next.Id = "next"; Market other = TradeBook(next).Find(next.Id);
            choices.Observe(other); Equal(choices.Selected(2), 0); Equal(choices.Selected(1), 0);
            choices.Observe(b.Find(s.Id)); Equal(choices.Selected(2), 7); Equal(choices.Selected(1), 0);
            choices.Observe(null); Equal(choices.Selected(2), 0);
        });
        Check("purchased-first order is stable and leaves all player identities and positions intact", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Market m = b.Find(s.Id);
            Buy(b, s, 6, 2, 10); Buy(b, s, 1, 5, 10); Buy(b, s, 4, 5, 10);
            int[] order = RankSelection.DisplayOrder(m); int[] expected = new int[] { 1, 4, 6, 0, 2, 3, 5, 7 };
            for (int i = 0; i < 8; i++) { Equal(order[i], expected[i]); Equal(m.Players[i].Uid, "player" + i); }
            Equal(m.Positions[1].Rank, 5); Equal(m.Positions[4].Rank, 5); Equal(m.Positions[6].Rank, 2);
        });
        Check("legacy tickets restore purchased ranks and appear before unbought rows", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = Open(s); Market m = b.Find(s.Id); var choices = new RankSelection(); choices.Observe(m);
            Equal(b.Bet(s, s.Players[7].Uid, 3, 10), null); choices.Observe(m);
            Equal(choices.Selected(7), 3); Equal(choices.Selected(0), 0); Equal(RankSelection.DisplayOrder(m)[0], 7);
            b = Reload(b); choices.Observe(b.Find(s.Id)); Equal(choices.Selected(7), 3);
        });
        Check("settled purchased ranks remain visible while unbought final ranks stay unselected", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Buy(b, s, 5, 4, 10); var choices = new RankSelection(); choices.Observe(b.Find(s.Id));
            s.Ended = true; s.Ranks = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 }; b.Observe(s, null);
            Market m = b.Find(s.Id); choices.Observe(m); True(m.Settled);
            Equal(choices.Selected(5), 4); Equal(choices.Selected(0), 0); Equal(RankSelection.DisplayOrder(m)[0], 5);
        });
        Check("stake drafts default to 10, accept per player edits, and reset only on new market", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Market m = b.Find(s.Id);
            var choices = new RankSelection(); choices.Observe(m);
            for (int i = 0; i < 8; i++) Equal(choices.Stake(i), "10");
            choices.SetStake(2, "50"); choices.SetStake(5, "100");
            Equal(choices.Stake(2), "50"); Equal(choices.Stake(5), "100");
            Equal(choices.Stake(0), "10");

            // Refresh within same market preserves stakes
            s.Round = 2; b.Observe(s, null); choices.Observe(m);
            Equal(choices.Stake(2), "50"); Equal(choices.Stake(5), "100");

            // New market resets stakes to 10
            MatchSnapshot s2 = Snapshot(1); s2.Id = "fixture2"; PredictionBook b2 = TradeBook(s2);
            choices.Observe(b2.Find(s2.Id));
            Equal(choices.Stake(2), "10"); Equal(choices.Stake(5), "10");
        });
    }
}
