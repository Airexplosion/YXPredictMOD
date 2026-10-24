using System;

namespace YxPrediction
{
    // 从八份已有命元记录按回合对齐，保留计算精度；仅显示时格式化为xx.xx%。
    public sealed class LifeShareChartData
    {
        public readonly double[] X;
        public readonly double[] Y;
        public readonly bool[] Connected;
        public readonly double Maximum;
        public readonly int FirstRound;
        public readonly int LastRound;
        public readonly bool HasCurrentValue;
        public readonly double CurrentShare;

        public LifeShareChartData(Market market, int player)
        {
            int capacity = market != null && player >= 0 && player < 8 ? market.Life[player].Points.Count : 0;
            // 使用一维原生数组，避免热更AOT环境缺少List<int>/List<double>实例化。
            var rounds = new int[capacity];
            var shares = new double[capacity];
            int count = 0;
            double maximum = 0;
            if (market != null && player >= 0 && player < 8)
            {
                LifeHistory selected = market.Life[player];
                var cursors = new int[8];
                for (int pIndex = 0; pIndex < selected.Points.Count; pIndex++)
                {
                    LifePoint point = selected.Points[pIndex];
                    long total = 0;
                    bool complete = true;
                    for (int i = 0; i < 8; i++)
                    {
                        var points = market.Life[i].Points;
                        while (cursors[i] < points.Count && points[cursors[i]].Round < point.Round) cursors[i]++;
                        if (cursors[i] >= points.Count || points[cursors[i]].Round != point.Round) { complete = false; break; }
                        total += Math.Max(0, points[cursors[i]].Life);
                    }
                    if (!complete || total <= 0) continue;
                    double share = Math.Max(0, point.Life) / (double)total;
                    rounds[count] = point.Round; shares[count] = share; count++; maximum = Math.Max(maximum, share);
                    if (pIndex == selected.Points.Count - 1) { HasCurrentValue = true; CurrentShare = share; }
                }
            }
            X = new double[count]; Y = new double[count]; Connected = new bool[count];
            // 自动缩放到10个百分点的上界，空图采用0～100%；坐标计算不提前截断占比。
            Maximum = count == 0 ? 1 : Math.Max(0.1, Math.Min(1, Math.Ceiling(maximum * 10) / 10));
            if (count > 0) { FirstRound = rounds[0]; LastRound = rounds[count - 1]; }
            for (int i = 0; i < count; i++)
            {
                X[i] = FirstRound == LastRound ? 0.5 : (rounds[i] - FirstRound) / (double)(LastRound - FirstRound);
                Y[i] = shares[i] / Maximum;
                Connected[i] = i > 0 && rounds[i] == rounds[i - 1] + 1;
            }
        }
    }
}
