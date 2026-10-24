using System;

namespace YxPrediction
{
    // 每一类图表都从八人数据整体范围生成坐标轴，切换观战目标不会改变刻度。
    public sealed class TrendChartData
    {
        public const int LifeMetric = 0, ShareMetric = 1, SwapsMetric = 2;
        public readonly double[] X, Y;
        public readonly bool[] Connected;
        public readonly double Minimum, Maximum;
        public readonly int FirstRound, LastRound, LatestRound;
        public readonly bool HasCurrentValue;
        public readonly double CurrentValue;

        sealed class Series
        {
            public readonly int[] Rounds;
            public readonly double[] Values;
            public int Count, RecordedRound;
            public Series(int capacity) { Rounds = new int[capacity]; Values = new double[capacity]; }
        }

        public TrendChartData(Market market, int player, int metric)
        {
            if (metric < LifeMetric || metric > SwapsMetric) throw new ArgumentException("图表类型无效");
            Series selected = new Series(0);
            double low = double.PositiveInfinity, high = double.NegativeInfinity;
            int first = int.MaxValue, last = 0;
            if (market != null)
                for (int i = 0; i < 8; i++)
                {
                    Series series = ReadSeries(market, i, metric);
                    if (i == player) selected = series;
                    for (int j = 0; j < series.Count; j++)
                    {
                        low = Math.Min(low, series.Values[j]); high = Math.Max(high, series.Values[j]);
                        first = Math.Min(first, series.Rounds[j]); last = Math.Max(last, series.Rounds[j]);
                    }
                }
            FirstRound = last == 0 ? 0 : first; LastRound = last;
            double minimum = 0, maximum = 1;
            if (last > 0)
            {
                double padding = Math.Max((high - low) * 0.1, metric == ShareMetric ? 0.001 : 1);
                double desired = (high - low + 2 * padding) / 5;
                double power = Math.Pow(10, Math.Floor(Math.Log10(desired)));
                double multiple = desired / power;
                double step = power * (multiple <= 1 ? 1 : (multiple <= 2 ? 2 : (multiple <= 5 ? 5 : 10)));
                step = Math.Max(step, metric == ShareMetric ? 0.0001 : 1);
                minimum = Math.Floor((low - padding) / step) * step;
                maximum = Math.Ceiling((high + padding) / step) * step;
                if (low >= 0) minimum = Math.Max(0, minimum);
                if (metric == ShareMetric) maximum = Math.Min(1, maximum);
                if (maximum <= minimum) maximum = minimum + step;
            }
            Minimum = minimum; Maximum = maximum;
            int count = selected.Count;
            X = new double[count]; Y = new double[count]; Connected = new bool[count];
            if (count > 0)
            {
                LatestRound = selected.Rounds[count - 1];
                HasCurrentValue = LatestRound == selected.RecordedRound;
                CurrentValue = selected.Values[count - 1];
            }
            for (int i = 0; i < count; i++)
            {
                X[i] = FirstRound == LastRound ? 0.5 : (selected.Rounds[i] - FirstRound) / (double)(LastRound - FirstRound);
                Y[i] = (selected.Values[i] - Minimum) / (Maximum - Minimum);
                Connected[i] = i > 0 && selected.Rounds[i] == selected.Rounds[i - 1] + 1;
            }
        }

        static Series ReadSeries(Market market, int player, int metric)
        {
            if (metric == SwapsMetric)
            {
                var points = market.Swaps[player].Points;
                var series = new Series(points.Count); series.Count = points.Count;
                if (points.Count > 0) series.RecordedRound = points[points.Count - 1].Round;
                for (int i = 0; i < points.Count; i++) { series.Rounds[i] = points[i].Round; series.Values[i] = points[i].Remaining; }
                return series;
            }
            var life = market.Life[player].Points;
            var result = new Series(life.Count);
            if (life.Count > 0) result.RecordedRound = life[life.Count - 1].Round;
            var cursors = new int[8];
            for (int pIndex = 0; pIndex < life.Count; pIndex++)
            {
                LifePoint point = life[pIndex];
                double value = point.Life;
                if (metric == ShareMetric)
                {
                    long total = 0; bool complete = true;
                    for (int i = 0; i < 8; i++)
                    {
                        var points = market.Life[i].Points;
                        while (cursors[i] < points.Count && points[cursors[i]].Round < point.Round) cursors[i]++;
                        if (cursors[i] >= points.Count || points[cursors[i]].Round != point.Round) { complete = false; break; }
                        total += Math.Max(0, points[cursors[i]].Life);
                    }
                    if (!complete || total <= 0) continue;
                    value = Math.Max(0, point.Life) / (double)total;
                }
                result.Rounds[result.Count] = point.Round; result.Values[result.Count] = value; result.Count++;
            }
            return result;
        }
    }
}
