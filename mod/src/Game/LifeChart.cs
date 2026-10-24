#if YX_GAME
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk.Unity;

namespace YxPrediction
{
    // 原生UGUI线段与数据点，复用对象；三类图表使用八人共同坐标轴。
    public sealed class LifeChart
    {
        readonly UiKit _ui;
        readonly GameObject _root;
        readonly TextMeshProUGUI _title, _minimum, _middle, _maximum, _first, _last, _empty;
        readonly List<RectTransform> _marks = new List<RectTransform>();
        readonly int _metric;
        readonly int[] _revisions = new int[8];
        readonly float _plotWidth;
        Market _market;
        int _player = -2;
        const float Left = 68, Top = -34, PlotHeight = 124;

        public LifeChart(UiKit ui, Transform parent, int metric, float x, float y, float width)
        {
            _ui = ui; _metric = metric; _plotWidth = width - Left - 18;
            _root = ui.Row(parent, "TrendChart" + PredictionMod.N(metric), new Vector2(x, y), new Vector2(width, 198), new Color(0.06f, 0.09f, 0.14f, 1));
            _title = Text("title", "", 12, -5, width - 24, 26, 16);
            _maximum = Text("maximum", "", 4, Top + 8, 60, 24, 12);
            _middle = Text("middle", "", 4, Top - PlotHeight / 2 + 12, 60, 24, 12);
            _minimum = Text("minimum", "", 4, Top - PlotHeight + 12, 60, 24, 12);
            _first = Text("firstRound", "", Left, -166, 100, 23, 12);
            _last = Text("lastRound", "", Left + _plotWidth - 86, -166, 100, 23, 12);
            _empty = Text("empty", "", Left, -76, _plotWidth, 44, 15);
        }
        TextMeshProUGUI Text(string name, string text, float x, float y, float width, float height, float size)
        {
            var label = _ui.Label(_root.transform, name, text, new Vector2(x, y), new Vector2(width, height), size);
            label.richText = false; return label;
        }
        void Mark(int index, float x, float y, float width, float height, float angle, Color color)
        {
            while (_marks.Count <= index)
            {
                GameObject obj = _ui.Row(_root.transform, "chartMark", Vector2.zero, Vector2.one, color);
                (obj.GetComponent(typeof(Image)) as Image).raycastTarget = false;
                _marks.Add(obj.GetComponent(typeof(RectTransform)) as RectTransform);
            }
            RectTransform rect = _marks[index];
            rect.gameObject.SetActive(true); rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = Quaternion.Euler(0, 0, angle);
            (rect.GetComponent(typeof(Image)) as Image).color = color;
        }
        string Axis(double value) { return _metric == TrendChartData.ShareMetric ? PredictionMod.Percent(value) : value.ToString("0.#", CultureInfo.InvariantCulture); }
        public void Refresh(Market market, int player)
        {
            if (market == null || player < 0 || player >= 8) { market = null; player = -1; }
            bool changed = _market != market || _player != player;
            if (market != null)
                for (int i = 0; i < 8; i++)
                {
                    int revision = _metric == TrendChartData.SwapsMetric ? market.Swaps[i].Revision : market.Life[i].Revision;
                    if (_revisions[i] != revision) changed = true;
                }
            if (!changed) return;
            _market = market; _player = player;
            if (market != null)
                for (int i = 0; i < 8; i++) _revisions[i] = _metric == TrendChartData.SwapsMetric ? market.Swaps[i].Revision : market.Life[i].Revision;
            var data = new TrendChartData(market, player, _metric);
            _minimum.text = Axis(data.Minimum); _maximum.text = Axis(data.Maximum); _middle.text = Axis((data.Minimum + data.Maximum) / 2);
            string title = _metric == TrendChartData.LifeMetric ? "命元走势" : (_metric == TrendChartData.ShareMetric ? "命元占比走势" : "剩余换牌次数走势");
            _title.text = title + (data.HasCurrentValue ? (_metric == TrendChartData.SwapsMetric ? "   最新同步 " : "   最新 ") + Axis(data.CurrentValue) : "   —");
            if (_metric == TrendChartData.SwapsMetric && data.HasCurrentValue) _title.text += "（第" + PredictionMod.N(data.LatestRound) + "轮）";
            _first.text = data.FirstRound == 0 ? "回合" : "第" + PredictionMod.N(data.FirstRound) + "轮";
            _last.text = data.FirstRound == data.LastRound ? "" : "第" + PredictionMod.N(data.LastRound) + "轮";
            _empty.text = data.X.Length == 0 ? "暂无数据" : "";
            int used = 0;
            Color line = _metric == TrendChartData.LifeMetric ? new Color(0.28f, 0.85f, 0.81f, 1)
                : (_metric == TrendChartData.ShareMetric ? new Color(0.96f, 0.69f, 0.29f, 1) : new Color(0.66f, 0.61f, 1, 1));
            Color grid = new Color(0.32f, 0.38f, 0.45f, 0.65f);
            Mark(used++, Left, Top - PlotHeight / 2, 1, PlotHeight, 0, grid);
            for (int i = 0; i < 3; i++) Mark(used++, Left + _plotWidth / 2, Top - PlotHeight * i / 2, _plotWidth, 1, 0, grid);
            if (data.Minimum < 0 && data.Maximum > 0)
            {
                float zero = Top - PlotHeight + (float)((0.0 - data.Minimum) / (data.Maximum - data.Minimum)) * PlotHeight;
                Mark(used++, Left + _plotWidth / 2, zero, _plotWidth, 1, 0, grid);
            }
            for (int i = 0; i < data.X.Length; i++)
            {
                float x = Left + (float)data.X[i] * _plotWidth;
                float y = Top - PlotHeight + (float)data.Y[i] * PlotHeight;
                if (data.Connected[i])
                {
                    float px = Left + (float)data.X[i - 1] * _plotWidth;
                    float py = Top - PlotHeight + (float)data.Y[i - 1] * PlotHeight;
                    float dx = x - px, dy = y - py;
                    Mark(used++, (x + px) / 2, (y + py) / 2, (float)Math.Sqrt(dx * dx + dy * dy), 2, (float)(Math.Atan2(dy, dx) * 180 / Math.PI), line);
                }
                Mark(used++, x, y, 5, 5, 0, line);
            }
            for (int i = used; i < _marks.Count; i++) _marks[i].gameObject.SetActive(false);
            _title.transform.SetAsLastSibling(); _empty.transform.SetAsLastSibling();
        }
    }
}
#endif
