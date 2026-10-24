using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Yx.ModSdk.Unity
{
    /// <summary>Original character prediction strip with its own dark inset, badge, rank, rate and stake controls.</summary>
    public sealed class NativePredictionBar
    {
        readonly UiKit _ui;
        public readonly GameObject Root;
        public readonly Image Badge;
        public readonly TextMeshProUGUI BadgeText, PlayerName, Rank, Rate, Holding;
        public readonly UiInput Stake;
        public readonly NativePanelButton Minus, Plus, Buy, Half, Sell, Overview, Collapse;
        static void Ignore(string text) { }
        public NativePredictionBar(ModContext ctx, NativePanelTheme theme, Action minus, Action plus, Action buy, Action half, Action sell, Action overview, Action collapse)
        {
            _ui=new UiKit(ctx);
            Root=_ui.Panel("PredictionCharacterBetBar",new Vector2(0.5f,0f),new Vector2(0.5f,0f),new Vector2(0f,76f),new Vector2(990f,66f),NativePanelTheme.LacquerBlack);
            if(Root==null)return;
            theme.ApplyWindowFrame(Root.GetComponent<Image>());
            Root.GetComponent<Image>().color=new Color(0.22f,0.25f,0.29f,1f);
                // 暗墨底色衬板，确保文字与按钮在任何背景下均具备极致高对比度
                var darkBack = new GameObject("Backing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                darkBack.transform.SetParent(Root.transform, false);
                var dbRect = darkBack.GetComponent<RectTransform>();
                dbRect.anchorMin = Vector2.zero;
                dbRect.anchorMax = Vector2.one;
                dbRect.sizeDelta = new Vector2(-12f, -12f);
                var dbImg = darkBack.GetComponent<Image>();
                dbImg.color = new Color(0.06f, 0.08f, 0.10f, 1f);
                dbImg.raycastTarget = false;

                Transform bar = Root.transform;

                // 1. 田忌称号与玩家名
                var iconGo = new GameObject("HorseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(bar, false);
                var iconRect = iconGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(iconRect, 14f, -14f, 38f, 38f);
                Badge = iconGo.GetComponent<Image>();
                Badge.raycastTarget = false;
                Badge.preserveAspect = true;

                var horseTitleGo = new GameObject("HorseTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                horseTitleGo.transform.SetParent(bar, false);
                var titleRect = horseTitleGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(titleRect, 58f, -12f, 105f, 20f);
                BadgeText = horseTitleGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(BadgeText, 15, true, TextAlignmentOptions.Left);
                BadgeText.raycastTarget = false;

                var playerNameGo = new GameObject("PlayerName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                playerNameGo.transform.SetParent(bar, false);
                var nameRect = playerNameGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(nameRect, 58f, -36f, 105f, 20f);
                PlayerName = playerNameGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(PlayerName, 14, false, TextAlignmentOptions.Left);
                PlayerName.color = NativePanelTheme.TextParchment;
                PlayerName.raycastTarget = false;

                // 2. 名次选择器与赔率
                Minus = theme.CreateButtonTopLeft(bar, "BtnMinus", "−", 168f, -16f, 30f, 34f, NativePanelButtonStyle.Pill, minus, 18);

                var rankGo = new GameObject("RankLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rankGo.transform.SetParent(bar, false);
                var rankRect = rankGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(rankRect, 202f, -16f, 48f, 34f);
                Rank = rankGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(Rank, 18, true, TextAlignmentOptions.Center);
                Rank.color = NativePanelTheme.AntiqueGold;

                Plus = theme.CreateButtonTopLeft(bar, "BtnPlus", "+", 254f, -16f, 30f, 34f, NativePanelButtonStyle.Pill, plus, 18);

                var oddsGo = new GameObject("OddsLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                oddsGo.transform.SetParent(bar, false);
                var oddsRect = oddsGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(oddsRect, 288f, -16f, 58f, 34f);
                Rate = oddsGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(Rate, 17, true, TextAlignmentOptions.Center);
                Rate.color = new Color(0.35f, 0.95f, 0.82f, 1f);

                // 3. 积分输入
                var stakeLblGo = new GameObject("StakePre", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                stakeLblGo.transform.SetParent(bar, false);
                var stakeLblRect = stakeLblGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(stakeLblRect, 350f, -20f, 18f, 26f);
                var stakePre = stakeLblGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(stakePre, 14, false, TextAlignmentOptions.Center);
                stakePre.text = "投";
                stakePre.color = NativePanelTheme.TextParchment;

                Stake = _ui.NumberInput(bar, "StakeInput", new Vector2(372f, -16f), new Vector2(52f, 34f), 5, Ignore);
                Transform inputTransform = bar.Find("StakeInput");
                if (inputTransform != null)
                {
                    var inputBg = inputTransform.GetComponent<Image>();
                    if (inputBg != null) theme.ApplyInset(inputBg);
                    theme.StyleInput(inputTransform,18);
                }

                var stakePostGo = new GameObject("StakePost", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                stakePostGo.transform.SetParent(bar, false);
                var stakePostRect = stakePostGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(stakePostRect, 428f, -20f, 18f, 26f);
                var stakePost = stakePostGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(stakePost, 14, false, TextAlignmentOptions.Center);
                stakePost.text = "分";
                stakePost.color = NativePanelTheme.TextParchment;

                // 4. 操作按钮
                Buy = theme.CreateButtonTopLeft(bar, "BtnBuy", "下注", 454f, -16f, 64f, 34f, NativePanelButtonStyle.Primary, buy, 15);
                Half = theme.CreateButtonTopLeft(bar, "BtnSellHalf", "退半", 522f, -16f, 52f, 34f, NativePanelButtonStyle.Caution, half, 14);
                Sell = theme.CreateButtonTopLeft(bar, "BtnSellAll", "清仓", 578f, -16f, 52f, 34f, NativePanelButtonStyle.Danger, sell, 14);

                // 5. 实时权益与预测预览
                var prevGo = new GameObject("PreviewLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                prevGo.transform.SetParent(bar, false);
                var prevRect = prevGo.GetComponent<RectTransform>();
                NativePanelTheme.SetTopLeft(prevRect, 638f, -16f, 172f, 34f);
                Holding = prevGo.GetComponent<TextMeshProUGUI>();
                theme.ApplyTextStyle(Holding, 14, false, TextAlignmentOptions.Left);
                Holding.color = NativePanelTheme.TextParchment;

                // 6. 快捷按钮
                Overview = theme.CreateButtonTopLeft(bar, "BtnOverview", "总览", 816f, -14f, 72f, 38f, NativePanelButtonStyle.Secondary, overview, 18);
                Collapse = theme.CreateButtonTopLeft(bar, "BtnCollapse", "收起", 900f, -16f, 76f, 34f, NativePanelButtonStyle.Pill, collapse, 16);
            Stake.SetText("10");
        }
        /// <summary>Hide content without allowing invisible children to receive clicks. Existing field values remain intact.</summary>
        public void SetCollapsed(bool collapsed)
        {
            if(Root==null)return;
            for(int i=0;i<Root.transform.childCount;i++)
            {
                Transform child=Root.transform.GetChild(i);
                if(child.gameObject==Collapse.GameObject)continue;
                if(collapsed)child.gameObject.SetActive(false);
                else if(child.name!="HorseIcon"&&child.name!="BtnSellHalf"&&child.name!="BtnSellAll")child.gameObject.SetActive(true);
            }
            Root.GetComponent<RectTransform>().sizeDelta=new Vector2(collapsed?120f:990f,collapsed?42f:66f);
            NativePanelTheme.SetTopLeft(Collapse.GameObject.GetComponent<RectTransform>(),collapsed?8f:900f,collapsed?-4f:-16f,collapsed?104f:76f,34f);
            Collapse.SetText(collapsed?"展开":"收起");
        }
        public void Destroy(){_ui.DestroyAll();}
    }
}
