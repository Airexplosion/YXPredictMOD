using System;
using System.Collections.Generic;

namespace YxPrediction
{
    public sealed class LifePoint
    {
        public readonly int Round;
        public readonly int Life;
        public LifePoint(int round, int life)
        {
            if (round < 1 || round > 100000 || life < -100000 || life > 100000) throw new ArgumentException("命元走势数值无效");
            Round = round; Life = life;
        }
    }

    public sealed class LifeHistory
    {
        public const int MaximumSamples = 128;
        public readonly List<LifePoint> Points = new List<LifePoint>();
        public int Revision;

        public bool Record(int round, int life)
        {
            var point = new LifePoint(round, life);
            if (Points.Count > 0)
            {
                LifePoint last = Points[Points.Count - 1];
                if (round < last.Round || (round == last.Round && life == last.Life)) return false;
                if (round == last.Round) { Points[Points.Count - 1] = point; Revision++; return true; }
            }
            if (Points.Count == MaximumSamples) Points.RemoveAt(0);
            Points.Add(point); Revision++; return true;
        }
    }

    public static class FocusSelection
    {
        public static string Uid(MatchSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Players == null || string.IsNullOrEmpty(snapshot.FocusedUid)) return null;
            for (int i = 0; i < snapshot.Players.Length; i++)
                if (snapshot.Players[i] != null && snapshot.Players[i].Uid == snapshot.FocusedUid) return snapshot.FocusedUid;
            return null;
        }
        public static int Index(Market market, MatchSnapshot snapshot)
        {
            string uid = Uid(snapshot);
            if (uid == null || market == null || snapshot.Id != market.Id) return -1;
            return market.FindPlayer(uid);
        }
    }

    // 与Unity分离的绘图坐标，单点居中、包含0轴、缺轮时不连线。
    public sealed class LifeChartData
    {
        public readonly double[] X;
        public readonly double[] Y;
        public readonly bool[] Connected;
        public readonly int Minimum;
        public readonly int Maximum;
        public readonly int FirstRound;
        public readonly int LastRound;
        public LifeChartData(LifeHistory history)
        {
            int count = history == null ? 0 : history.Points.Count;
            X = new double[count]; Y = new double[count]; Connected = new bool[count];
            int min = 0, max = 0;
            if (count > 0)
            {
                FirstRound = history.Points[0].Round; LastRound = history.Points[count - 1].Round;
                for (int i = 0; i < count; i++) { min = Math.Min(min, history.Points[i].Life); max = Math.Max(max, history.Points[i].Life); }
            }
            Minimum = (int)Math.Floor(min / 10.0) * 10;
            Maximum = Math.Max(10, (int)Math.Ceiling(max / 10.0) * 10);
            for (int i = 0; i < count; i++)
            {
                LifePoint point = history.Points[i];
                X[i] = FirstRound == LastRound ? 0.5 : (point.Round - FirstRound) / (double)(LastRound - FirstRound);
                Y[i] = (point.Life - Minimum) / (double)(Maximum - Minimum);
                Connected[i] = i > 0 && point.Round == history.Points[i - 1].Round + 1;
            }
        }
    }
}
