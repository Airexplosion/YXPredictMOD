using System;
using System.Collections.Generic;
using Yx.ModSdk.Testing;
using Yx.Shared;
using YxPrediction;

internal static partial class Program
{
    static void LifeChecks()
    {
        Check("life samples replace the same round and ignore older status", delegate
        {
            var h = new LifeHistory(); True(h.Record(1, 100)); True(!h.Record(1, 100)); True(h.Record(1, 95));
            True(h.Record(2, 81)); True(!h.Record(1, 50)); Equal(h.Points.Count, 2); Equal(h.Points[0].Life, 95); Equal(h.Points[1].Life, 81);
            Throws(delegate { h.Record(0, 10); }); Throws(delegate { h.Record(3, int.MaxValue); });
        });
        Check("life collection covers all players independently of focus without moving points", delegate
        {
            MatchSnapshot s = Snapshot(1); s.FocusedUid = "player0"; PredictionBook b = TradeBook(s);
            s.Round = 2; s.States[0] = new PlayerState(88, 0, 1, 0); s.States[1] = new PlayerState(91, 1, 0, 0); b.Observe(s, null);
            s.FocusedUid = "player1"; b.Observe(s, null); Market m = b.Find(s.Id);
            for (int i = 0; i < 8; i++) Equal(m.Life[i].Points.Count, 2);
            Equal(m.Life[0].Points[1].Life, 88); Equal(m.Life[1].Points[1].Life, 91); Equal(b.Balance, 1000L);
            s.Round = 1; s.States[0] = new PlayerState(50, 0, 1, 0); b.Observe(s, null); Equal(m.Life[0].Points[0].Life, 100);
        });
        Check("focus and life samples follow UID across server roster reorder", delegate
        {
            MatchSnapshot s = Snapshot(1); s.FocusedUid = "player0"; PredictionBook b = TradeBook(s); Market m = b.Find(s.Id);
            Array.Reverse(s.Players); Array.Reverse(s.States); s.Round = 2; s.States[7] = new PlayerState(73, 0, 1, 0); b.Observe(s, null);
            Equal(FocusSelection.Index(m, s), 0); Equal(m.Life[0].Points[1].Life, 73); Equal(m.Life[7].Points[1].Life, 100);
            s.FocusedUid = "player7"; Equal(FocusSelection.Index(m, s), 7);
        });
        Check("chart coordinates use real round spacing and break missing rounds", delegate
        {
            var h = new LifeHistory(); h.Record(4, 100); h.Record(5, 80); h.Record(8, 40);
            var data = new LifeChartData(h); Equal(data.FirstRound, 4); Equal(data.LastRound, 8);
            Near(data.X[0], 0); Near(data.X[1], 0.25); Near(data.X[2], 1); Near(data.Y[0], 1); Near(data.Y[2], 0.4);
            True(!data.Connected[0] && data.Connected[1] && !data.Connected[2]);
        });
        Check("empty single point flat and negative life charts stay finite", delegate
        {
            var empty = new LifeChartData(null); Equal(empty.X.Length, 0);
            var h = new LifeHistory(); h.Record(10, 0); var data = new LifeChartData(h);
            Near(data.X[0], 0.5); Near(data.Y[0], 0); True(data.Maximum > data.Minimum);
            h.Record(11, 0); data = new LifeChartData(h); Near(data.Y[1], 0); True(data.Connected[1]);
            h.Record(12, -7); data = new LifeChartData(h); Equal(data.Minimum, -10);
            foreach (double value in data.Y) True(value >= 0 && value <= 1 && !double.IsNaN(value));
        });
        Check("format four keeps life across reload and format three upgrades without invented history", delegate
        {
            MatchSnapshot s = Snapshot(5); PredictionBook b = TradeBook(s); Buy(b, s, 0, 1, 100);
            s.Round = 6; s.States[0] = new PlayerState(90, 1, 0, 0); b.Observe(s, null); b = Reload(b);
            Equal(b.Find(s.Id).Life[0].Points.Count, 2); Equal(b.Find(s.Id).Life[0].Points[1].Life, 90); Equal(b.Balance, 900L);
            var old = BookCodec.Write(b); old["version"] = 3;
            foreach (Dictionary<string, object> row in Json.GetArray(old, "markets")) row.Remove("life");
            b = BookCodec.Read(Json.ParseObject(Json.Serialize(old))); Equal(b.Find(s.Id).Life[0].Points.Count, 0);
            b.Observe(s, null); Equal(b.Find(s.Id).Life[0].Points.Count, 1); Equal(b.Find(s.Id).Life[0].Points[0].Round, 6);
            Equal(b.Find(s.Id).Positions[0].Invested, 100L); Equal(b.Balance, 900L);
        });
        Check("life history is bounded and remains readable after trimming", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s);
            for (int round = 2; round <= 150; round++) { s.Round = round; b.Observe(s, null); }
            var h = b.Find(s.Id).Life[0]; Equal(h.Points.Count, LifeHistory.MaximumSamples); Equal(h.Points[0].Round, 23);
            b = Reload(b); Equal(b.Find(s.Id).Life[0].Points[127].Round, 150);
        });
        Check("malformed duplicate future and missing life histories are rejected", delegate
        {
            MatchSnapshot s = Snapshot(5); PredictionBook b = TradeBook(s);
            var data = BookCodec.Write(b); var row = (Dictionary<string, object>)Json.GetArray(data, "markets")[0]; row.Remove("life");
            Throws(delegate { BookCodec.Read(data); });
            data = BookCodec.Write(b); row = (Dictionary<string, object>)Json.GetArray(data, "markets")[0];
            var points = (List<object>)Json.GetArray(row, "life")[0]; points.Add(5); points.Add(90); Throws(delegate { BookCodec.Read(data); });
            data = BookCodec.Write(b); row = (Dictionary<string, object>)Json.GetArray(data, "markets")[0];
            ((List<object>)Json.GetArray(row, "life")[0])[0] = 6; Throws(delegate { BookCodec.Read(data); });
        });
        Check("unknown focus never defaults to another player's data", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s);
            Equal(FocusSelection.Index(b.Find(s.Id), s), -1); s.FocusedUid = "foreign"; Equal(FocusSelection.Uid(s), null);
            s.FocusedUid = "player1"; Equal(FocusSelection.Index(b.Find(s.Id), s), 1);
            MatchSnapshot other = Snapshot(1); other.Id = "other"; other.FocusedUid = "player1"; Equal(FocusSelection.Index(b.Find(s.Id), other), -1);
        });
        Check("focused trade callbacks cannot act on a previous player or match", delegate
        {
            using (TestHost host = TestHost.Start())
            {
                PredictionMod mod = host.Load<PredictionMod>(Id, "0.2.1"); MatchSnapshot s = Snapshot(6); s.FocusedUid = "player0";
                mod.AcceptSnapshot(s, Ratings(false)); int version = mod.Current.TradeVersion;
                mod.SetFocusedUid("player1");
                mod.Trade(0, "player0", 1, 100, version, "player0", s.Id); Equal(mod.Book.Balance, 1000L);
                mod.Trade(0, "player1", 1, 100, version, "player1", s.Id); Equal(mod.Book.Balance, 900L);
                mod.SetFocusedUid("player0");
                mod.Trade(2, "player1", 1, 0, mod.Current.TradeVersion, "player1", s.Id); Equal(mod.Book.Balance, 900L);
                mod.Trade(3, "player1", 2, 10, mod.Current.TradeVersion, "player1", s.Id); Equal(mod.Book.Balance, 900L);
                mod.Trade(0, "player0", 1, 10, mod.Current.TradeVersion, "player0", "previous-match"); Equal(mod.Book.Balance, 900L);
            }
        });
        Check("legacy focused bets use the same target guard and histories stay isolated", delegate
        {
            using (TestHost host = TestHost.Start())
            {
                MatchSnapshot s = Snapshot(2); s.FocusedUid = "player0"; PredictionBook b = FixedBook(s, Ratings(false), 2);
                var saved = new Dictionary<string, object>(); saved[PredictionMod.SaveKey] = BookCodec.Write(b);
                PredictionMod mod = host.Load<PredictionMod>(Id, "0.2.1", data: saved); mod.AcceptSnapshot(s, null);
                mod.SetFocusedUid("player1"); mod.PlaceBet("player0", 1, 10, "player0", s.Id); Equal(mod.Book.Balance, 1000L);
                mod.PlaceBet("player1", 1, 10, "player1", s.Id); Equal(mod.Book.Balance, 990L);
                MatchSnapshot other = Snapshot(10); other.Id = "other"; other.States[0] = new PlayerState(25, 0, 0, 0); mod.AcceptSnapshot(other, Ratings(false));
                Equal(mod.Book.Find(s.Id).Life[0].Points[0].Round, 2); Equal(mod.Current.Life[0].Points[0].Round, 10); Equal(mod.Current.Life[0].Points[0].Life, 25);
            }
        });
    }
}
