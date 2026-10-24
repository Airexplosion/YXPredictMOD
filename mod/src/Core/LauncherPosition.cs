using System;
using System.Collections.Generic;
using Yx.Shared;

namespace YxPrediction
{
    public sealed class LauncherPosition
    {
        // 新布局单独存储，升级后不再恢复旧版中央/底部的位置。
        public const string SaveKey = "launcherPosition.v2";
        public const double DefaultX = 0.853125;
        public const double DefaultY = 0.92;
        public double X { get; private set; }
        public double Y { get; private set; }

        public LauncherPosition(Dictionary<string, object> saved)
        {
            X = saved == null ? DefaultX : Coordinate(saved, "x", DefaultX);
            Y = saved == null ? DefaultY : Coordinate(saved, "y", DefaultY);
        }

        static double Coordinate(Dictionary<string, object> saved, string key, double fallback)
        {
            double value = Json.GetNumber(saved, key, fallback);
            return double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(0, Math.Min(1, value));
        }

        public void Move(double x, double y, double widthFraction, double heightFraction)
        {
            double halfWidth = Math.Min(0.5, Math.Max(0, widthFraction / 2));
            double halfHeight = Math.Min(0.5, Math.Max(0, heightFraction / 2));
            X = Math.Max(halfWidth, Math.Min(1 - halfWidth, x));
            Y = Math.Max(halfHeight, Math.Min(1 - halfHeight, y));
        }

        public Dictionary<string, object> Write()
        {
            var data = new Dictionary<string, object>();
            data["x"] = X; data["y"] = Y;
            return data;
        }
    }
}
