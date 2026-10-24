using System;
using System.Collections.Generic;
using Yx.Shared;
using YxPrediction;

internal static partial class Program
{
    static void TrendChecks()
    {
        Check("remaining swaps replace each round's last value and reject older values", delegate
        {
            var h = new SwapHistory(); True(h.Record(4, 5)); True(h.Record(4, 2)); True(!h.Record(4, 2)); True(h.Record(5, 7)); True(!h.Record(4, 10));
            Equal(h.Points.Count, 2); Equal(h.Points[0].Remaining, 2); Equal(h.Points[1].Remaining, 7);
            Throws(delegate { h.Record(5, -1); }); Throws(delegate { h.Record(0, 5); });
        });
        Check("swap histories follow UID after reorder and do not affect trading money", delegate
        {
            MatchSnapshot s = Snapshot(3); s.RemainingSwaps = new int[] { 8, 7, 6, 5, 4, 3, 2, 1 }; PredictionBook b = TradeBook(s);
            s.Round = 4; Array.Reverse(s.Players); Array.Reverse(s.States); Array.Reverse(s.RemainingSwaps); s.RemainingSwaps[7] = 4;
            b.Observe(s, null); Equal(b.Find(s.Id).Swaps[0].Points[1].Remaining, 4); Equal(b.Find(s.Id).Swaps[7].Points[1].Remaining, 1); Equal(b.Balance, 1000L);
            s.Round = 3; s.RemainingSwaps[7] = 9; b.Observe(s, null); Equal(b.Find(s.Id).Swaps[0].Points[0].Remaining, 8);
        });
        Check("swap samples survive reload and format four upgrades without fake history", delegate
        {
            MatchSnapshot s = Snapshot(6); s.RemainingSwaps = new int[] { 3, 4, 5, 6, 7, 8, 9, 10 }; PredictionBook b = TradeBook(s); Buy(b, s, 0, 1, 100);
            b = Reload(b); Equal(b.Find(s.Id).Swaps[0].Points[0].Remaining, 3); Equal(b.Balance, 900L);
            var data = BookCodec.Write(b); data["version"] = 4;
            foreach (Dictionary<string, object> row in Json.GetArray(data, "markets")) row.Remove("swaps");
            b = BookCodec.Read(Json.ParseObject(Json.Serialize(data))); Equal(b.Find(s.Id).Swaps[0].Points.Count, 0);
            Equal(b.Find(s.Id).Life[0].Points.Count, 1); Equal(b.Balance, 900L); b.Observe(s, null); Equal(b.Find(s.Id).Swaps[0].Points[0].Remaining, 3);
        });
        Check("missing and invalid swap counts remain missing with bounded persisted history", delegate
        {
            MatchSnapshot s = Snapshot(1); s.RemainingSwaps = new int[] { 5, -1, 0, 3, 4, 5, 6, 7 }; PredictionBook b = TradeBook(s);
            Equal(b.Find(s.Id).Swaps[1].Points.Count, 0); Equal(b.Find(s.Id).Swaps[2].Points[0].Remaining, 0);
            for (int round = 2; round <= 150; round++) { s.Round = round; b.Observe(s, null); }
            b = Reload(b); Equal(b.Find(s.Id).Swaps[0].Points.Count, LifeHistory.MaximumSamples); Equal(b.Find(s.Id).Swaps[0].Points[0].Round, 23);
            var chart = new TrendChartData(b.Find(s.Id), 1, TrendChartData.SwapsMetric); Equal(chart.X.Length, 0); True(!chart.HasCurrentValue);
        });
        Check("all eight life charts share a cropped axis covering the largest swing", delegate
        {
            MatchSnapshot s = Snapshot(3); PredictionBook b = TradeBook(s); s.States[7] = new PlayerState(110, 0, 0, 0); b.Observe(s, null);
            s.Round = 5; s.States[0] = new PlayerState(94, 0, 0, 0); s.States[7] = new PlayerState(80, 0, 1, 0); b.Observe(s, null);
            Market m = b.Find(s.Id); var first = new TrendChartData(m, 0, TrendChartData.LifeMetric);
            True(first.Minimum > 0 && first.Minimum <= 80 && first.Maximum >= 110 && first.Maximum - first.Minimum < 110);
            for (int i = 0; i < 8; i++)
            {
                var chart = new TrendChartData(m, i, TrendChartData.LifeMetric);
                Near(chart.Minimum, first.Minimum); Near(chart.Maximum, first.Maximum); Equal(chart.FirstRound, 3); Equal(chart.LastRound, 5);
                foreach (double y in chart.Y) True(y >= 0 && y <= 1);
            }
            var widest = new TrendChartData(m, 7, TrendChartData.LifeMetric); True(Math.Abs(widest.Y[1] - widest.Y[0]) > 0.5);
        });
        Check("share axes use all eight ratios and retain percentage precision", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); var uniform = new TrendChartData(b.Find(s.Id), 0, TrendChartData.ShareMetric);
            Near(uniform.CurrentValue, 0.125); True(uniform.Minimum > 0 && uniform.Minimum < 0.125 && uniform.Maximum > 0.125);
            Equal(PredictionMod.Percent(uniform.CurrentValue), "12.50%");
            s.Round = 2; s.States[7] = new PlayerState(50, 0, 1, 0); b.Observe(s, null);
            var first = new TrendChartData(b.Find(s.Id), 0, TrendChartData.ShareMetric); True(first.Minimum > 0);
            for (int i = 0; i < 8; i++)
            {
                var chart = new TrendChartData(b.Find(s.Id), i, TrendChartData.ShareMetric);
                Near(chart.Minimum, first.Minimum); Near(chart.Maximum, first.Maximum); foreach (double y in chart.Y) True(y >= 0 && y <= 1);
            }
            Near(first.CurrentValue, 100.0 / 750);
        });
        Check("remaining swap charts use one dynamic axis and one round range", delegate
        {
            MatchSnapshot s = Snapshot(4); s.RemainingSwaps = new int[] { 9, 9, 9, 9, 9, 9, 9, 9 }; PredictionBook b = TradeBook(s);
            s.Round = 6; s.RemainingSwaps[7] = 5; b.Observe(s, null); Market m = b.Find(s.Id);
            var first = new TrendChartData(m, 0, TrendChartData.SwapsMetric); True(first.Minimum > 0);
            for (int i = 0; i < 8; i++)
            {
                var chart = new TrendChartData(m, i, TrendChartData.SwapsMetric);
                Near(chart.Minimum, first.Minimum); Near(chart.Maximum, first.Maximum); Equal(chart.FirstRound, 4); Equal(chart.LastRound, 6);
            }
            var widest = new TrendChartData(m, 7, TrendChartData.SwapsMetric); True(Math.Abs(widest.Y[1] - widest.Y[0]) > 0.5); Equal(widest.CurrentValue, 5.0);
        });
        Check("common time axes retain missing rounds and late player's real position", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); s.Round = 8; b.Observe(s, null); Market m = b.Find(s.Id);
            m.Life[3].Points.Clear(); m.Life[3].Record(7, 88);
            var chart = new TrendChartData(m, 3, TrendChartData.LifeMetric); Equal(chart.FirstRound, 1); Equal(chart.LastRound, 8); Equal(chart.X.Length, 1); Near(chart.X[0], 6.0 / 7);
            chart = new TrendChartData(m, 0, TrendChartData.LifeMetric); True(!chart.Connected[1]);
        });
        Check("shared axes handle flat zero negative and 100 percent values without NaNs", delegate
        {
            MatchSnapshot s = Snapshot(1); PredictionBook b = TradeBook(s); Market m = b.Find(s.Id);
            var flat = new TrendChartData(m, 0, TrendChartData.LifeMetric); True(flat.Minimum < 100 && flat.Maximum > 100); Near(flat.Y[0], 0.5);
            s.Round = 2; for (int i = 0; i < 8; i++) s.States[i] = new PlayerState(-5, 0, 0, 0); b.Observe(s, null);
            var share = new TrendChartData(m, 0, TrendChartData.ShareMetric); True(!share.HasCurrentValue);
            var life = new TrendChartData(m, 0, TrendChartData.LifeMetric); True(life.Minimum <= -5); foreach (double y in life.Y) True(!double.IsNaN(y) && y >= 0 && y <= 1);
            s.Round = 3; s.States[0] = new PlayerState(100, 0, 0, 0); b.Observe(s, null); share = new TrendChartData(m, 0, TrendChartData.ShareMetric);
            Near(share.Maximum, 1); Near(share.CurrentValue, 1); True(!share.Connected[1]);
            var empty = new TrendChartData(null, -1, TrendChartData.SwapsMetric); Equal(empty.X.Length, 0); True(empty.Maximum > empty.Minimum);
        });
        Check("malformed swap history is rejected instead of altering the wallet", delegate
        {
            MatchSnapshot s = Snapshot(1); s.RemainingSwaps = new int[] { 5, 5, 5, 5, 5, 5, 5, 5 }; PredictionBook b = TradeBook(s);
            var data = BookCodec.Write(b); var row = (Dictionary<string, object>)Json.GetArray(data, "markets")[0]; row.Remove("swaps"); Throws(delegate { BookCodec.Read(data); });
            data = BookCodec.Write(b); row = (Dictionary<string, object>)Json.GetArray(data, "markets")[0];
            ((List<object>)Json.GetArray(row, "swaps")[0])[1] = -1; Throws(delegate { BookCodec.Read(data); });
            data = BookCodec.Write(b); row = (Dictionary<string, object>)Json.GetArray(data, "markets")[0]; var points = (List<object>)Json.GetArray(row, "swaps")[0];
            points.Add(1); points.Add(3); Throws(delegate { BookCodec.Read(data); }); Equal(b.Balance, 1000L);
        });
    }
}
