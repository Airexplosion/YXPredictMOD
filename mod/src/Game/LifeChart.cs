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
    // 原生UGUI线段与数据点，复用对象；三类图表使用八人共同坐标轴，搭配修仙水墨深潭与青金配线。
    public sealed class LifeChart
    {
        readonly UiKit _ui;
        readonly GameObject _root;
        readonly TextMeshProUGUI _title, _minimum, _middle, _maximum, _first, _last, _empty;
        readonly List<RectTransform> _marks = new List<RectTransform>();
        readonly int _metric;
        readonly int[] _revisions = new int[8];
        readonly float _plotWidth;
        readonly float _plotHeight;
        Market _market;
        int _player = -2;
        const float Left = 68, Top = -34;

        public LifeChart(UiKit ui, Transform parent, int metric, float x, float y, float width, float height = 198f, NativeTheme theme = null)
        {
            _ui = ui;
            _metric = metric;
            _plotWidth = width - Left - 18;
            _plotHeight = height - 74f;

            _root = ui.Row(parent, "TrendChart" + PredictionMod.N(metric), new Vector2(x, y), new Vector2(width, height), new Color(0.05f, 0.07f, 0.09f, 0.94f));
            if (theme != null)
            {
                var bg = _root.GetComponent(typeof(Image)) as Image;
                if (bg != null) theme.ApplyInset(bg);
            }

            _title = Text("title", "", 14, -6, width - 28, 26, 15, theme);
            _maximum = Text("maximum", "", 4, Top + 8, 60, 24, 12, theme);
            _middle = Text("middle", "", 4, Top - _plotHeight / 2 + 12, 60, 24, 12, theme);
            _minimum = Text("minimum", "", 4, Top - _plotHeight + 12, 60, 24, 12, theme);
            _first = Text("firstRound", "", Left, -(height - 30f), 100, 23, 12, theme);
            _last = Text("lastRound", "", Left + _plotWidth - 86, -(height - 30f), 100, 23, 12, theme);
            _empty = Text("empty", "", Left, -_plotHeight / 2 - 14, _plotWidth, 44, 15, theme);

            if (_title != null) _title.color = NativeTheme.AntiqueGold;
        }

        TextMeshProUGUI Text(string name, string text, float x, float y, float width, float height, float size, NativeTheme theme)
        {
            var label = _ui.Label(_root.transform, name, text, new Vector2(x, y), new Vector2(width, height), size);
            label.richText = false;
            if (theme != null) theme.ApplyTextStyle(label, (int)size, false, TextAlignmentOptions.Left);
            return label;
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
            rect.gameObject.SetActive(true);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = Quaternion.Euler(0, 0, angle);
            (rect.GetComponent(typeof(Image)) as Image).color = color;
        }

        string Axis(double value) { return _metric == TrendChartData.ShareMetric ? PredictionMod.Percent(value) : value.ToString("0.#", CultureInfo.InvariantCulture); }

        public void ShowWaiting(string message)
        {
            _market=null;_player=-2;
            _title.text=_metric==TrendChartData.LifeMetric?"命元走势":_metric==TrendChartData.ShareMetric?"命元占比走势":"剩余换牌次数走势";
            _empty.text=message;_minimum.text="";_middle.text="";_maximum.text="";_first.text="";_last.text="";
            for(int i=0;i<_marks.Count;i++)_marks[i].gameObject.SetActive(false);
        }
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

            Color line = _metric == TrendChartData.LifeMetric ? new Color(0.30f, 0.88f, 0.75f, 1)
                : (_metric == TrendChartData.ShareMetric ? new Color(0.96f, 0.74f, 0.30f, 1) : new Color(0.70f, 0.65f, 1, 1));
            Color grid = new Color(0.38f, 0.35f, 0.28f, 0.45f);

            Mark(used++, Left, Top - _plotHeight / 2, 1, _plotHeight, 0, grid);
            for (int i = 0; i < 3; i++) Mark(used++, Left + _plotWidth / 2, Top - _plotHeight * i / 2, _plotWidth, 1, 0, grid);
            if (data.Minimum < 0 && data.Maximum > 0)
            {
                float zero = Top - _plotHeight + (float)((0.0 - data.Minimum) / (data.Maximum - data.Minimum)) * _plotHeight;
                Mark(used++, Left + _plotWidth / 2, zero, _plotWidth, 1, 0, grid);
            }
            for (int i = 0; i < data.X.Length; i++)
            {
                float x = Left + (float)data.X[i] * _plotWidth;
                float y = Top - _plotHeight + (float)data.Y[i] * _plotHeight;
                if (data.Connected[i])
                {
                    float px = Left + (float)data.X[i - 1] * _plotWidth;
                    float py = Top - _plotHeight + (float)data.Y[i - 1] * _plotHeight;
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
