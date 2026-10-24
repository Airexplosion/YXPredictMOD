using System;
using System.Collections.Generic;

namespace YxPrediction
{
    public sealed class SwapPoint
    {
        public readonly int Round;
        public readonly int Remaining;
        public SwapPoint(int round, int remaining)
        {
            if (round < 1 || round > 100000 || remaining < 0 || remaining > 100000) throw new ArgumentException("换牌次数走势数值无效");
            Round = round; Remaining = remaining;
        }
    }

    public sealed class SwapHistory
    {
        public readonly List<SwapPoint> Points = new List<SwapPoint>();
        public int Revision;
        public bool Record(int round, int remaining)
        {
            var point = new SwapPoint(round, remaining);
            if (Points.Count > 0)
            {
                SwapPoint last = Points[Points.Count - 1];
                if (round < last.Round || (round == last.Round && remaining == last.Remaining)) return false;
                if (round == last.Round) { Points[Points.Count - 1] = point; Revision++; return true; }
            }
            if (Points.Count == LifeHistory.MaximumSamples) Points.RemoveAt(0);
            Points.Add(point); Revision++; return true;
        }
    }
}
