using System;
using YxPrediction;

internal static partial class Program
{
    static void LifeShareChecks()
    {
        Check("equal life gives each player 12.50 percent and normalized share axes", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Market m = b.Find(s.Id);
            double sum = 0;
            for (int i = 0; i < 8; i++)
            {
                var chart = new LifeShareChartData(m, i); True(chart.HasCurrentValue); Near(chart.CurrentShare, 0.125);
                Equal(PredictionMod.Percent(chart.CurrentShare), "12.50%"); Near(chart.X[0], 0.5); Near(chart.Maximum, 0.2); Near(chart.Y[0], 0.625);
                sum += chart.CurrentShare;
            }
            Near(sum, 1);
        });
        Check("share changes when only other players change their life in the same round", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Market m = b.Find(s.Id); int revision = m.Life[0].Revision;
            for (int i = 1; i < 8; i++) s.States[i] = new PlayerState(60, 0, 0, 0);
            b.Observe(s, null); Equal(m.Life[0].Revision, revision);
            var chart = new LifeShareChartData(m, 0); Equal(chart.X.Length, 1); Near(chart.CurrentShare, 100.0 / 520);
            Equal(PredictionMod.Percent(chart.CurrentShare), "19.23%"); Equal(b.Balance, 1000L);
        });
        Check("share follows the focused UID across reordered snapshots and reload", delegate
        {
            MatchSnapshot s = Snapshot(1); s.FocusedUid = "player0"; PredictionBook b = TradeBook(s);
            Array.Reverse(s.Players); Array.Reverse(s.States); s.Round = 2; s.States[7] = new PlayerState(70, 0, 1, 0); b.Observe(s, null);
            b = Reload(b); Market m = b.Find(s.Id); var chart = new LifeShareChartData(m, FocusSelection.Index(m, s));
            Equal(chart.X.Length, 2); True(chart.Connected[1]); Near(chart.CurrentShare, 70.0 / 770); Equal(PredictionMod.Percent(chart.CurrentShare), "9.09%");
            s.FocusedUid = "player7"; chart = new LifeShareChartData(m, FocusSelection.Index(m, s)); Near(chart.CurrentShare, 100.0 / 770);
        });
        Check("share requires all eight samples from exactly the same round and leaves gaps", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s);
            s.Round = 2; b.Observe(s, null); s.Round = 3; b.Observe(s, null); Market m = b.Find(s.Id);
            m.Life[7].Points.RemoveAt(1);
            var chart = new LifeShareChartData(m, 0); Equal(chart.X.Length, 2); Equal(chart.FirstRound, 1); Equal(chart.LastRound, 3);
            True(!chart.Connected[1]); Near(chart.X[1], 1);
            m.Life[7].Points.RemoveAt(1); chart = new LifeShareChartData(m, 0);
            Equal(chart.X.Length, 1); True(!chart.HasCurrentValue);
        });
        Check("zero total life leaves no current ratio and does not join across the gap", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s);
            s.Round = 2; for (int i = 0; i < 8; i++) s.States[i] = new PlayerState(0, 0, 0, 0); b.Observe(s, null);
            var chart = new LifeShareChartData(b.Find(s.Id), 0); Equal(chart.X.Length, 1); True(!chart.HasCurrentValue);
            s.Round = 3; s.States[0] = new PlayerState(100, 0, 0, 0); b.Observe(s, null);
            chart = new LifeShareChartData(b.Find(s.Id), 0); True(chart.HasCurrentValue); Near(chart.CurrentShare, 1); Equal(chart.X.Length, 2); True(!chart.Connected[1]);
            Equal(PredictionMod.Percent(chart.CurrentShare), "100.00%");
            chart = new LifeShareChartData(b.Find(s.Id), 7); Near(chart.CurrentShare, 0); Equal(PredictionMod.Percent(chart.CurrentShare), "0.00%");
        });
        Check("eliminated negative life counts as zero and shares remain inside 0 to 100 percent", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Market m = b.Find(s.Id);
            for (int i = 0; i < 8; i++) m.Life[i].Record(1, i == 0 ? 100 : -20);
            var chart = new LifeShareChartData(m, 0); Near(chart.CurrentShare, 1);
            chart = new LifeShareChartData(m, 1); Near(chart.CurrentShare, 0);
            for (int i = 0; i < 8; i++) m.Life[i].Record(1, -20);
            chart = new LifeShareChartData(m, 0); True(!chart.HasCurrentValue); Equal(chart.X.Length, 0);
            chart = new LifeShareChartData(null, -1); Equal(chart.X.Length, 0);
        });
        Check("odds show two decimals while locked payout keeps its original precision", delegate
        {
            Equal(PredictionMod.Rate(61408), "6.14x"); Equal(PredictionMod.Rate(12345), "1.23x"); Equal(PredictionMod.Rate(500000), "50.00x");
            Equal(PredictionMod.Percent(1.0 / 3), "33.33%");
            var ticket = new Ticket(0, 1, 5, 10000, 12345); Equal(ticket.RateBps, 12345); Equal(ticket.Payout, 12345L);
        });
    }
}
