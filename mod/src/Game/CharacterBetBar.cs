#if YX_GAME
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Unity;

namespace YxPrediction
{
    public sealed class CharacterBetBar
    {
        readonly PredictionMod _mod;
        readonly ModContext _ctx;
        readonly PredictionPanel _panel;
        readonly NativeTheme _theme;
        readonly PanelLayout _layout;
        readonly UiKit _ui;

        GameObject _root;
        RectTransform _rect;
        RectTransform _parent;
        CanvasGroup _canvasGroup;

        // 控件列表
        Image _horseIcon;
        TextMeshProUGUI _horseTitle;
        TextMeshProUGUI _playerName;

        NativeButton _btnMinus;
        NativeButton _btnPlus;
        TextMeshProUGUI _rankLabel;
        TextMeshProUGUI _oddsLabel;

        UiInput _stakeInput;
        NativeButton _btnBuy;
        NativeButton _btnSellHalf;
        NativeButton _btnSellAll;

        TextMeshProUGUI _previewLabel;
        NativeButton _btnTrend;
        NativeButton _btnOverview;
        NativeButton _btnCollapse;

        // 交互与手势
        bool _collapsed;
        bool _pressed;
        bool _dragged;
        Vector2 _downScreen;
        Vector2 _downLocal;
        double _startX;
        double _startY;
        int _enableAfterFrame = -1;

        float _nextRefresh;
        string _lastFocusUid;
        string _lastMarketId;

        public CharacterBetBar(PredictionMod mod, ModContext ctx, PredictionPanel panel, NativeTheme theme, PanelLayout layout)
        {
            _mod = mod;
            _ctx = ctx;
            _panel = panel;
            _theme = theme;
            _layout = layout;
            _ui = new UiKit(ctx);
        }

        public void Update()
        {
            if (!PredictionVisibility.CanShow())
            {
                FinishGesture();
                if (_root != null) _root.SetActive(false);
                return;
            }

            if (GameBridge.IsPlayingBattle())
            {
                FinishGesture();
                if (_root != null) _root.SetActive(false);
                return;
            }

            string focusUid = FocusSelection.Uid(_mod.Snapshot);
            if (string.IsNullOrEmpty(focusUid))
            {
                if (_root != null) _root.SetActive(false);
                return;
            }

            if (_root == null)
            {
                Build();
            }

            if (_root == null || _rect == null) return;

            _root.SetActive(true);
            UpdatePosition();
            HandlePointerGesture();

            if (Time.unscaledTime >= _nextRefresh || focusUid != _lastFocusUid || (_mod.Snapshot != null && _mod.Snapshot.Id != _lastMarketId))
            {
                _nextRefresh = Time.unscaledTime + 0.3f;
                Refresh(focusUid);
            }
        }

        void Build()
        {
            try
            {
                Transform parentTransform = Ui.FindRootCanvas() != null ? Ui.FindRootCanvas().transform : null;
                if (parentTransform == null) return;

                _parent = parentTransform as RectTransform;

                _root = new GameObject("PredictionCharacterBetBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                _root.transform.SetParent(_parent, false);

                _rect = _root.GetComponent<RectTransform>();
                _rect.anchorMin = new Vector2(0.5f, 0f);
                _rect.anchorMax = new Vector2(0.5f, 0f);
                _rect.pivot = new Vector2(0.5f, 0f);
                _rect.sizeDelta = new Vector2(PanelLayout.BetBarWidth, PanelLayout.BetBarHeight);

                _canvasGroup = _root.GetComponent<CanvasGroup>();
                var bg = _root.GetComponent<Image>();
                _theme.ApplyWindowFrame(bg);
                bg.raycastTarget = true;

                // 暗墨底色衬板，确保文字与按钮在任何背景下均具备极致高对比度
                var darkBack = new GameObject("Backing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                darkBack.transform.SetParent(_root.transform, false);
                var dbRect = darkBack.GetComponent<RectTransform>();
                dbRect.anchorMin = Vector2.zero;
                dbRect.anchorMax = Vector2.one;
                dbRect.sizeDelta = new Vector2(-12f, -12f);
                var dbImg = darkBack.GetComponent<Image>();
                dbImg.color = new Color(0.06f, 0.08f, 0.10f, 0.94f);
                dbImg.raycastTarget = false;

                Transform bar = _root.transform;

                // 1. 田忌称号与玩家名
                var iconGo = new GameObject("HorseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(bar, false);
                var iconRect = iconGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(iconRect, 14f, -14f, 38f, 38f);
                _horseIcon = iconGo.GetComponent<Image>();
                _horseIcon.raycastTarget = false;
                _horseIcon.preserveAspect = true;

                var horseTitleGo = new GameObject("HorseTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                horseTitleGo.transform.SetParent(bar, false);
                var titleRect = horseTitleGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(titleRect, 58f, -12f, 105f, 20f);
                _horseTitle = horseTitleGo.GetComponent<TextMeshProUGUI>();
                _theme.ApplyTextStyle(_horseTitle, 15, true, TextAlignmentOptions.Left);
                _horseTitle.raycastTarget = false;

                var playerNameGo = new GameObject("PlayerName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                playerNameGo.transform.SetParent(bar, false);
                var nameRect = playerNameGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(nameRect, 58f, -36f, 105f, 20f);
                _playerName = playerNameGo.GetComponent<TextMeshProUGUI>();
                _theme.ApplyTextStyle(_playerName, 14, false, TextAlignmentOptions.Left);
                _playerName.color = NativeTheme.TextParchment;
                _playerName.raycastTarget = false;

                // 2. 名次选择器与赔率
                _btnMinus = _theme.CreateButtonTopLeft(bar, "BtnMinus", "−", 168f, -16f, 30f, 34f, NativeBtnStyle.Pill, OnMinusClicked, 18);

                var rankGo = new GameObject("RankLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rankGo.transform.SetParent(bar, false);
                var rankRect = rankGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(rankRect, 202f, -16f, 48f, 34f);
                _rankLabel = rankGo.GetComponent<TextMeshProUGUI>();
                _theme.ApplyTextStyle(_rankLabel, 18, true, TextAlignmentOptions.Center);
                _rankLabel.color = NativeTheme.AntiqueGold;

                _btnPlus = _theme.CreateButtonTopLeft(bar, "BtnPlus", "+", 254f, -16f, 30f, 34f, NativeBtnStyle.Pill, OnPlusClicked, 18);

                var oddsGo = new GameObject("OddsLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                oddsGo.transform.SetParent(bar, false);
                var oddsRect = oddsGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(oddsRect, 288f, -16f, 58f, 34f);
                _oddsLabel = oddsGo.GetComponent<TextMeshProUGUI>();
                _theme.ApplyTextStyle(_oddsLabel, 17, true, TextAlignmentOptions.Center);
                _oddsLabel.color = new Color(0.35f, 0.95f, 0.82f, 1f);

                // 3. 积分输入
                var stakeLblGo = new GameObject("StakePre", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                stakeLblGo.transform.SetParent(bar, false);
                var stakeLblRect = stakeLblGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(stakeLblRect, 350f, -20f, 18f, 26f);
                var stakePre = stakeLblGo.GetComponent<TextMeshProUGUI>();
                _theme.ApplyTextStyle(stakePre, 14, false, TextAlignmentOptions.Center);
                stakePre.text = "投";
                stakePre.color = NativeTheme.TextParchment;

                _stakeInput = _ui.NumberInput(bar, "StakeInput", new Vector2(372f, -16f), new Vector2(52f, 34f), 16, OnStakeChanged);
                Transform inputTransform = bar.Find("StakeInput");
                if (inputTransform != null)
                {
                    var inputBg = inputTransform.GetComponent<Image>();
                    if (inputBg != null) _theme.ApplyInset(inputBg);
                }

                var stakePostGo = new GameObject("StakePost", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                stakePostGo.transform.SetParent(bar, false);
                var stakePostRect = stakePostGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(stakePostRect, 428f, -20f, 18f, 26f);
                var stakePost = stakePostGo.GetComponent<TextMeshProUGUI>();
                _theme.ApplyTextStyle(stakePost, 14, false, TextAlignmentOptions.Center);
                stakePost.text = "分";
                stakePost.color = NativeTheme.TextParchment;

                // 4. 操作按钮
                _btnBuy = _theme.CreateButtonTopLeft(bar, "BtnBuy", "下注", 454f, -16f, 64f, 34f, NativeBtnStyle.Primary, OnBuyClicked, 15);
                _btnSellHalf = _theme.CreateButtonTopLeft(bar, "BtnSellHalf", "退半", 522f, -16f, 52f, 34f, NativeBtnStyle.Caution, OnSellHalfClicked, 14);
                _btnSellAll = _theme.CreateButtonTopLeft(bar, "BtnSellAll", "清仓", 578f, -16f, 52f, 34f, NativeBtnStyle.Danger, OnSellAllClicked, 14);

                // 5. 实时权益与预测预览
                var prevGo = new GameObject("PreviewLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                prevGo.transform.SetParent(bar, false);
                var prevRect = prevGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(prevRect, 638f, -16f, 172f, 34f);
                _previewLabel = prevGo.GetComponent<TextMeshProUGUI>();
                _theme.ApplyTextStyle(_previewLabel, 14, false, TextAlignmentOptions.Left);
                _previewLabel.color = NativeTheme.TextParchment;

                // 6. 快捷按钮
                _btnTrend = _theme.CreateButtonTopLeft(bar, "BtnTrend", "走势", 816f, -16f, 38f, 34f, NativeBtnStyle.Secondary, OnTrendClicked, 14);
                _btnOverview = _theme.CreateButtonTopLeft(bar, "BtnOverview", "总览", 858f, -16f, 38f, 34f, NativeBtnStyle.Secondary, OnOverviewClicked, 14);
                _btnCollapse = _theme.CreateButtonTopLeft(bar, "BtnCollapse", "▾", 900f, -17f, 26f, 32f, NativeBtnStyle.Pill, OnCollapseClicked, 16);
            }
            catch (Exception ex)
            {
                _ctx.Log.Error("CharacterBetBar.Build error: " + ex);
            }
        }

        void UpdatePosition()
        {
            if (_dragged) return;

            if (_layout.BetBarCustom)
            {
                if (_parent != null)
                {
                    Rect bounds = _parent.rect;
                    if (bounds.width > 0 && bounds.height > 0)
                    {
                        var anchor = new Vector2((float)_layout.BetBarX, (float)_layout.BetBarY);
                        _rect.anchorMin = _rect.anchorMax = anchor;
                        _rect.anchoredPosition = Vector2.zero;
                        return;
                    }
                }
            }

            // 智能避让与停靠（手牌区上方）
            bool hasHand, hasTalents;
            GameBridge.InspectBottomHandZone(out hasHand, out hasTalents);
            float targetY = PanelLayout.BetBarAutoY(hasHand, hasTalents);

            _rect.anchorMin = new Vector2(0.5f, 0f);
            _rect.anchorMax = new Vector2(0.5f, 0f);
            _rect.pivot = new Vector2(0.5f, 0f);
            _rect.anchoredPosition = new Vector2(0f, targetY);
        }

        void HandlePointerGesture()
        {
            if (_enableAfterFrame >= 0 && Time.frameCount > _enableAfterFrame)
            {
                _enableAfterFrame = -1;
            }

            Vector3 mouse = Input.mousePosition;
            var screen = new Vector2(mouse.x, mouse.y);
            Camera camera = null;
            Canvas rootCanvas = Ui.FindRootCanvas();
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) camera = rootCanvas.worldCamera;

            Vector2 local;
            if (_parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, screen, camera, out local)) return;

            if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(_rect, screen, camera))
            {
                _pressed = true;
                _dragged = false;
                _downScreen = screen;
                _downLocal = local;
                _startX = _layout.BetBarX;
                _startY = _layout.BetBarY;
            }

            if (!_pressed) return;

            if ((screen - _downScreen).sqrMagnitude >= 36f)
            {
                _dragged = true;
            }

            if (_dragged)
            {
                Rect bounds = _parent.rect;
                if (bounds.width > 0 && bounds.height > 0)
                {
                    double newX = _startX + (local.x - _downLocal.x) / bounds.width;
                    double newY = _startY + (local.y - _downLocal.y) / bounds.height;
                    _layout.MoveBetBar(newX, newY, bounds.width, bounds.height);
                    var anchor = new Vector2((float)_layout.BetBarX, (float)_layout.BetBarY);
                    _rect.anchorMin = _rect.anchorMax = anchor;
                    _rect.anchoredPosition = Vector2.zero;
                }
            }

            if (!Input.GetMouseButton(0))
            {
                FinishGesture();
            }
        }

        void FinishGesture()
        {
            if (!_pressed) return;
            bool wasDragged = _dragged;
            _pressed = false;
            _dragged = false;
            if (wasDragged)
            {
                _enableAfterFrame = Time.frameCount;
                _ctx.Data.Set(PanelLayout.BetBarSaveKey, _layout.WriteBetBar());
            }
        }

        public void Refresh(string focusUid)
        {
            try
            {
                _lastFocusUid = focusUid;
                MatchSnapshot snapshot = _mod.Snapshot;
                _lastMarketId = snapshot == null ? null : snapshot.Id;

                int pIndex = -1;
                if (snapshot != null)
                {
                    for (int i = 0; i < snapshot.Players.Length; i++)
                    {
                        if (snapshot.Players[i].Uid == focusUid) { pIndex = i; break; }
                    }
                }

                if (pIndex < 0)
                {
                    _root.SetActive(false);
                    return;
                }

                Player player = snapshot.Players[pIndex];
                _playerName.text = player.Name;

                HorseMember member = _mod.Horses != null ? _mod.Horses.FindMember(focusUid) : null;
                if (member != null && !string.IsNullOrEmpty(member.Title))
                {
                    Sprite sp = _mod.Horses.GetSprite(member.Title);
                    _horseIcon.sprite = sp;
                    _horseIcon.gameObject.SetActive(sp != null);
                    _horseTitle.text = member.Caption;
                    _horseTitle.color = TianJiFeature.ColorOf(member.Title);
                }
                else
                {
                    _horseIcon.gameObject.SetActive(false);
                    _horseTitle.text = "暂无称号";
                }

                Market market = _panel.Shown();
                if (market != null) _mod.Drafts.Observe(market);

                int chosenRank = _mod.Drafts.Selected(pIndex);
                _rankLabel.text = chosenRank > 0 ? "第" + chosenRank.ToString(CultureInfo.InvariantCulture) + "名" : "—";

                bool open = market != null && market.TradingWindowOpen;
                if (market != null && chosenRank >= 1 && chosenRank <= 8)
                {
                    int bps = market.IsTrading ? market.QuoteRates[pIndex * 8 + chosenRank - 1] : Odds.EffectiveRate(market.BaseRates[pIndex * 8 + chosenRank - 1], Math.Max(1, Math.Min(5, market.HighestRound)));
                    _oddsLabel.text = PredictionMod.Rate(bps);
                }
                else
                {
                    _oddsLabel.text = "—";
                }

                string draftStake = _mod.Drafts.Stake(pIndex);
                if (_stakeInput != null && _stakeInput.Text != draftStake)
                {
                    _stakeInput.SetText(draftStake);
                }

                Position pos = market != null && market.IsTrading ? market.Positions[pIndex] : default(Position);
                bool hasPosition = pos.Claim > 0;

                if (!hasPosition)
                {
                    _btnBuy.SetText("下注");
                    _btnSellHalf.SetActive(false);
                    _btnSellAll.SetActive(false);
                    _previewLabel.text = "未持仓 · 余额: " + PredictionMod.N(_mod.Book != null ? _mod.Book.Balance : 1000) + "分";
                }
                else
                {
                    bool same = chosenRank == pos.Rank;
                    _btnBuy.SetText(same ? "加注" : "改猜");
                    _btnSellHalf.SetActive(true);
                    _btnSellAll.SetActive(true);

                    long sale = TradingOdds.Sale(market, pIndex, false);
                    _previewLabel.text = "猜" + PredictionMod.N(pos.Rank) + "名 · 权益 " + PredictionMod.N(pos.Claim) + "分 (参考清仓 " + PredictionMod.N(sale) + "分)";
                }

                _btnBuy.SetInteractable(open && chosenRank >= 1 && chosenRank <= 8);
                _btnSellHalf.SetInteractable(open && hasPosition);
                _btnSellAll.SetInteractable(open && hasPosition);
            }
            catch (Exception ex)
            {
                _ctx.Log.Error("CharacterBetBar.Refresh error: " + ex);
            }
        }

        void OnMinusClicked()
        {
            int pIndex = GetFocusedPlayerIndex();
            if (pIndex >= 0)
            {
                _mod.Drafts.Step(pIndex, false);
                Refresh(_lastFocusUid);
            }
        }

        void OnPlusClicked()
        {
            int pIndex = GetFocusedPlayerIndex();
            if (pIndex >= 0)
            {
                _mod.Drafts.Step(pIndex, true);
                Refresh(_lastFocusUid);
            }
        }

        void OnStakeChanged(string text)
        {
            int pIndex = GetFocusedPlayerIndex();
            if (pIndex >= 0)
            {
                _mod.Drafts.SetStake(pIndex, text);
            }
        }

        void OnBuyClicked()
        {
            int pIndex = GetFocusedPlayerIndex();
            if (pIndex < 0) return;

            MatchSnapshot snapshot = _mod.Snapshot;
            Market market = _panel.Shown();
            if (snapshot == null || market == null) return;

            int rank = _mod.Drafts.Selected(pIndex);
            if (rank < 1 || rank > 8) return;

            long stake = 10;
            long parsed;
            if (long.TryParse(_mod.Drafts.Stake(pIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
            {
                stake = parsed;
            }

            string uid = snapshot.Players[pIndex].Uid;
            Position pos = market.Positions[pIndex];
            int action = pos.Claim > 0 ? (pos.Rank == rank ? 0 : 3) : 0;
            _mod.Trade(action, uid, rank, stake, market.TradeVersion);
            Refresh(uid);
        }

        void OnSellHalfClicked()
        {
            int pIndex = GetFocusedPlayerIndex();
            if (pIndex < 0) return;
            MatchSnapshot snapshot = _mod.Snapshot;
            Market market = _panel.Shown();
            if (snapshot == null || market == null) return;
            string uid = snapshot.Players[pIndex].Uid;
            _mod.Trade(1, uid, 0, 0, market.TradeVersion);
            Refresh(uid);
        }

        void OnSellAllClicked()
        {
            int pIndex = GetFocusedPlayerIndex();
            if (pIndex < 0) return;
            MatchSnapshot snapshot = _mod.Snapshot;
            Market market = _panel.Shown();
            if (snapshot == null || market == null) return;
            string uid = snapshot.Players[pIndex].Uid;
            _mod.Trade(2, uid, 0, 0, market.TradeVersion);
            Refresh(uid);
        }

        void OnTrendClicked()
        {
            int pIndex = GetFocusedPlayerIndex();
            if (pIndex >= 0)
            {
                _panel.ShowTrend(pIndex);
            }
        }

        void OnOverviewClicked()
        {
            _panel.Toggle();
        }

        void OnCollapseClicked()
        {
            _collapsed = !_collapsed;
            if (_rect != null)
            {
                _rect.sizeDelta = new Vector2(PanelLayout.BetBarWidth, _collapsed ? 36f : PanelLayout.BetBarHeight);
            }
            if (_btnCollapse != null)
            {
                _btnCollapse.SetText(_collapsed ? "▴" : "▾");
            }
        }

        int GetFocusedPlayerIndex()
        {
            MatchSnapshot snapshot = _mod.Snapshot;
            if (snapshot == null || string.IsNullOrEmpty(_lastFocusUid)) return -1;
            for (int i = 0; i < snapshot.Players.Length; i++)
            {
                if (snapshot.Players[i].Uid == _lastFocusUid) return i;
            }
            return -1;
        }

        public void Destroy()
        {
            FinishGesture();
            _ui.DestroyAll();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
        }
    }
}
#endif
