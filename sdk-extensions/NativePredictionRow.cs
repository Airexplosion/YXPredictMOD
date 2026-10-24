using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Yx.ModSdk.Unity
{
    /// <summary>Classic seven-column prediction row. Rendering only; caller owns all data and actions.</summary>
    public sealed class NativePredictionRow
    {
        readonly UiKit _ui;
        public readonly GameObject Root;
        public readonly RectTransform Rect;
        public readonly Image Highlight, Badge, Divider;
        public readonly TextMeshProUGUI PlayerName, PlayerDetail, BadgeText, Rank, Rate, Holding, Result;
        public readonly NativePanelButton Minus, Plus, Buy, Half, Sell;
        public readonly UiInput Stake;
        static void Ignore(string value) { }
            public NativePredictionRow(ModContext ctx, NativePanelTheme theme, Transform parent, int index, Action minus, Action plus, Action buy, Action half, Action sell)
            {
                _ui = new UiKit(ctx);

                Root = new GameObject("Row_" + index.ToString(CultureInfo.InvariantCulture), typeof(RectTransform));
                Root.transform.SetParent(parent, false);

                Rect = Root.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(Rect, 0f, -32f-index*58f, 1140f - 24f, 54f);

                Transform row = Root.transform;

                // 关注高亮光晕
                var flagGo = new GameObject("SelfFlag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                flagGo.transform.SetParent(row, false);
                var flagRect = flagGo.GetComponent<RectTransform>();
                flagRect.anchorMin = Vector2.zero;
                flagRect.anchorMax = Vector2.one;
                flagRect.sizeDelta = Vector2.zero;
                Highlight = flagGo.GetComponent<Image>();
                theme.ApplySelfFlag(Highlight);
                Highlight.raycastTarget = false;
                Highlight.gameObject.SetActive(false);

                // 1. 玩家信息
                var nameGo = new GameObject("PlayerName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                nameGo.transform.SetParent(row, false);
                var nameRect = nameGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(nameRect, 16f, -6f, 160f, 24f);
                PlayerName = nameGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(PlayerName, 16, true, TextAlignmentOptions.Left);
                PlayerName.raycastTarget = false;

                var lifeGo = new GameObject("PlayerLife", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                lifeGo.transform.SetParent(row, false);
                var lifeRect = lifeGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(lifeRect, 16f, -30f, 160f, 20f);
                PlayerDetail = lifeGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(PlayerDetail, 13, false, TextAlignmentOptions.Left);
                PlayerDetail.color = NativePanelTheme.TextMuted;
                PlayerDetail.raycastTarget = false;

                // 2. 田忌称号
                var iconGo = new GameObject("HorseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(row, false);
                var iconRect = iconGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(iconRect, 186f, -10f, 36f, 36f);
                Badge = iconGo.GetComponent<Image>();
                Badge.preserveAspect = true;
                Badge.raycastTarget = false;

                var titleGo = new GameObject("HorseTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(row, false);
                var titleRect = titleGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(titleRect, 226f, -10f, 105f, 36f);
                BadgeText = titleGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(BadgeText, 15, true, TextAlignmentOptions.Left);
                BadgeText.raycastTarget = false;

                // 3. 目标名次与倍率
                Minus = theme.CreateButtonTopLeft(row, "Minus", "−", 336f, -11f, 30f, 34f, NativePanelButtonStyle.Pill, minus, 18);

                var rankGo = new GameObject("RankLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rankGo.transform.SetParent(row, false);
                var rRect = rankGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(rRect, 370f, -11f, 52f, 34f);
                Rank = rankGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(Rank, 18, true, TextAlignmentOptions.Center);
                Rank.color = NativePanelTheme.AntiqueGold;

                Plus = theme.CreateButtonTopLeft(row, "Plus", "+", 426f, -11f, 30f, 34f, NativePanelButtonStyle.Pill, plus, 18);

                var oddsGo = new GameObject("OddsLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                oddsGo.transform.SetParent(row, false);
                var oRect = oddsGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(oRect, 464f, -11f, 76f, 34f);
                Rate = oddsGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(Rate, 17, true, TextAlignmentOptions.Center);
                Rate.color = new Color(0.35f, 0.95f, 0.82f, 1f);

                // 4. 积分输入
                Stake = _ui.NumberInput(row, "Stake", new Vector2(548f, -11f), new Vector2(68f, 34f), 5, Ignore);
                Transform stakeT = row.Find("Stake");
                if (stakeT != null)
                {
                    var bg = stakeT.GetComponent<Image>();
                    if (bg != null) theme.ApplyInset(bg);
                    theme.StyleInput(stakeT,20);
                }

                Stake.SetText("10");

                // 5. 调仓操作按钮
                Buy = theme.CreateButtonTopLeft(row, "BtnBuy", "下注", 624f, -11f, 68f, 34f, NativePanelButtonStyle.Primary, buy, 15);
                Half = theme.CreateButtonTopLeft(row, "BtnHalf", "退半", 698f, -11f, 56f, 34f, NativePanelButtonStyle.Caution, half, 14);
                Sell = theme.CreateButtonTopLeft(row, "BtnSell", "清仓", 760f, -11f, 56f, 34f, NativePanelButtonStyle.Danger, sell, 14);

                // 6. 权益与结果
                var holdingGo = new GameObject("HoldingLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                holdingGo.transform.SetParent(row, false);
                var hRect = holdingGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(hRect, 830f, -8f, 200f, 24f);
                Holding = holdingGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(Holding, 15, true, TextAlignmentOptions.Left);
                Holding.color = NativePanelTheme.TextParchment;
                Holding.raycastTarget = false;

                var resultGo = new GameObject("ResultLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                resultGo.transform.SetParent(row, false);
                var resRect = resultGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(resRect, 830f, -31f, 200f, 22f);
                Result = resultGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(Result, 13, false, TextAlignmentOptions.Left);
                Result.color = NativePanelTheme.AntiqueGold;
                Result.raycastTarget = false;

                // 7. 分隔线
                var lineGo = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                lineGo.transform.SetParent(row, false);
                var lineRect = lineGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(lineRect, 10f, -54f, 1140f - 44f, 1.5f);
                Divider = lineGo.GetComponent<Image>();
                theme.ApplyLine(Divider);
                Divider.raycastTarget = false;
            }


        /// <summary>Move only the row, preserving all control dimensions and callbacks.</summary>
        public void SetSlot(int index) { Rect.anchoredPosition = new Vector2(0f, -32f-index*58f); }
    }
}
