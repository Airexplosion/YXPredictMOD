using System;
using System.Collections.Generic;
using Yx.Shared;

namespace YxPrediction
{
    // 纯逻辑布局与几何计算，不依赖UnityEngine类型；可直接由单元测试与ILRuntime解释器探测执行。
    public sealed class PanelLayout
    {
        public const string WindowSaveKey = "overviewPosition.v1";
        public const string BetBarSaveKey = "betBarPosition.v1";

        public const double DefaultWindowX = 0.5;
        public const double DefaultWindowY = 0.5;

        public const float WindowWidth = 1140f;
        public const float WindowHeight = 624f;
        public const float MinimizedHeight = 56f;
        public const float PeekAlpha = 0.18f;

        public const float BetBarWidth = 940f;
        public const float BetBarHeight = 66f;
        public const float BetBarHandY = 326f;
        public const float BetBarTalentsY = 48f;
        public const float BetBarEmptyY = 76f;
        public const float BetBarDefaultXOffset = 28f;

        public double WindowX { get; private set; }
        public double WindowY { get; private set; }
        public bool IsPeek { get; set; }
        public bool IsMinimized { get; set; }

        public bool BetBarCustom { get; private set; }
        public double BetBarX { get; private set; }
        public double BetBarY { get; private set; }

        public PanelLayout(Dictionary<string, object> savedWindow, Dictionary<string, object> savedBetBar)
        {
            WindowX = savedWindow == null ? DefaultWindowX : Coordinate(savedWindow, "x", DefaultWindowX);
            WindowY = savedWindow == null ? DefaultWindowY : Coordinate(savedWindow, "y", DefaultWindowY);

            if (savedBetBar != null && savedBetBar.ContainsKey("custom") && savedBetBar["custom"] is bool && (bool)savedBetBar["custom"])
            {
                BetBarCustom = true;
                BetBarX = Coordinate(savedBetBar, "x", 0.5);
                BetBarY = Coordinate(savedBetBar, "y", 0.1);
            }
            else
            {
                BetBarCustom = false;
                BetBarX = 0.5;
                BetBarY = 0.1;
            }
        }

        static double Coordinate(Dictionary<string, object> saved, string key, double fallback)
        {
            double value = Json.GetNumber(saved, key, fallback);
            return double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(0, Math.Min(1, value));
        }

        public void MoveWindow(double x, double y, double widthFraction, double heightFraction)
        {
            double halfW = Math.Min(0.5, Math.Max(0, widthFraction / 2));
            double halfH = Math.Min(0.5, Math.Max(0, heightFraction / 2));
            WindowX = Math.Max(halfW, Math.Min(1 - halfW, x));
            WindowY = Math.Max(halfH, Math.Min(1 - halfH, y));
        }

        public void MoveBetBar(double x, double y, double widthFraction, double heightFraction)
        {
            BetBarCustom = true;
            double halfW = Math.Min(0.5, Math.Max(0, widthFraction / 2));
            double halfH = Math.Min(0.5, Math.Max(0, heightFraction / 2));
            BetBarX = Math.Max(halfW, Math.Min(1 - halfW, x));
            BetBarY = Math.Max(halfH, Math.Min(1 - halfH, y));
        }

        public void ResetBetBarPosition()
        {
            BetBarCustom = false;
        }

        public static float BetBarAutoY(bool hasHandCards, bool hasCenteredTalents)
        {
            if (hasHandCards) return BetBarHandY;
            if (hasCenteredTalents) return BetBarTalentsY;
            return BetBarEmptyY;
        }

        public Dictionary<string, object> WriteWindow()
        {
            var data = new Dictionary<string, object>();
            data["x"] = WindowX;
            data["y"] = WindowY;
            return data;
        }

        public Dictionary<string, object> WriteBetBar()
        {
            var data = new Dictionary<string, object>();
            data["custom"] = BetBarCustom;
            data["x"] = BetBarX;
            data["y"] = BetBarY;
            return data;
        }
    }
}
