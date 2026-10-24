#if YX_GAME
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Game;
using Yx.ModSdk.Unity;

namespace YxPrediction
{
    // 角色手牌区域原生投注条：自动停靠在当前观战/选中角色的手牌上方，不遮挡卡牌与游戏UI。
    public sealed class CharacterBetBar
    {
        readonly PredictionMod _mod;
        readonly ModContext _ctx;
        readonly UiKit _ui;
        readonly NativeTheme _theme;
        readonly PanelLayout _layout;
        readonly PredictionPanel _panel;

        GameObject _root;
        RectTransform _rect;
        RectTransform _parent;
        CanvasGroup _canvasGroup;

        // 玩家与田忌称号
        Image _horseIcon;
        TextMeshProUGUI _horseTitle;
        TextMeshProUGUI _playerName;
        TextMeshProUGUI _playerElo;

        // 名次与倍率
        NativeButton _btnMinus;
        NativeButton _btnPlus;
        TextMeshProUGUI _rankLabel;
        TextMeshProUGUI _oddsLabel;

        // 投入与调仓
        UiInput _stakeInput;
        NativeButton _btnBuy;
        NativeButton _btnSellHalf;
        NativeButton _btnSellAll;

        // 权益预览与快捷按钮
        TextMeshProUGUI _previewLabel;
        NativeButton _btnTrend;
        NativeButton _btnOverview;
        NativeButton _btnCollapse;

        // 拖动与状态管理
        bool _collapsed;
        bool _pressed;
        bool _dragged;
        Vector2 _downScreen;
        Vector2 _downLocal;
        double _startX;
        double _startY;
        int _enableAfterFrame = -1;
        float _nextRefresh;
        int _lastPlayerIndex = -1;
        string _lastFocusUid;
        string _lastMarketId;

        const float DragThreshold = 6f;

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
            bool inMatch = BattleManager.Instance != null && SceneLoader.currentSceneName == "Battle";
            if (!inMatch || GameBridge.IsPlayingBattle())
            {
                FinishGesture();
                if (_root != null) _root.SetActive(false);
                return;
            }

            string focusUid = FocusSelection.Uid(_mod.Snapshot);
            if (string.IsNullOrEmpty(focusUid))
            {
                FinishGesture();
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

            Transform bar = _root.transform;

            // 1. 田忌称号与玩家名（左侧）
            var iconGo = new GameObject("HorseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(bar, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(-430f, 33f);
            iconRect.sizeDelta = new Vector2(38f, 38f);
            _horseIcon = iconGo.GetComponent<Image>();
            _horseIcon.raycastTarget = false;
            _horseIcon.preserveAspect = true;

            var horseTitleGo = new GameObject("HorseTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            horseTitleGo.transform.SetParent(bar, false);
            var titleRect = horseTitleGo.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(-360f, 44f);
            titleRect.sizeDelta = new Vector2(100f, 22f);
            _horseTitle = horseTitleGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_horseTitle, 15, true, TextAlignmentOptions.Left);
            _horseTitle.raycastTarget = false;

            var playerEloGo = new GameObject("PlayerElo", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            playerEloGo.transform.SetParent(bar, false);
            var eloRect = playerEloGo.GetComponent<RectTransform>();
            eloRect.anchoredPosition = new Vector2(-360f, 22f);
            eloRect.sizeDelta = new Vector2(100f, 20f);
            _playerElo = playerEloGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_playerElo, 13, false, TextAlignmentOptions.Left);
            _playerElo.color = NativeTheme.TextMuted;
            _playerElo.raycastTarget = false;

            var playerNameGo = new GameObject("PlayerName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            playerNameGo.transform.SetParent(bar, false);
            var nameRect = playerNameGo.GetComponent<RectTransform>();
            nameRect.anchoredPosition = new Vector2(-255f, 33f);
            nameRect.sizeDelta = new Vector2(100f, 30f);
            _playerName = playerNameGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_playerName, 16, true, TextAlignmentOptions.Left);
            _playerName.raycastTarget = false;

            // 2. 名次与赔率选择器
            _btnMinus = _theme.CreateButton(bar, "BtnMinus", "−", new Vector2(-180f, 33f), new Vector2(28f, 34f), NativeBtnStyle.Pill, OnMinusClicked, 18);

            var rankGo = new GameObject("RankLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            rankGo.transform.SetParent(bar, false);
            var rankRect = rankGo.GetComponent<RectTransform>();
            rankRect.anchoredPosition = new Vector2(-130f, 33f);
            rankRect.sizeDelta = new Vector2(70f, 32f);
            _rankLabel = rankGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_rankLabel, 15, true, TextAlignmentOptions.Center);
            _rankLabel.color = NativeTheme.AntiqueGold;

            _btnPlus = _theme.CreateButton(bar, "BtnPlus", "+", new Vector2(-80f, 33f), new Vector2(28f, 34f), NativeBtnStyle.Pill, OnPlusClicked, 18);

            var oddsGo = new GameObject("OddsLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            oddsGo.transform.SetParent(bar, false);
            var oddsRect = oddsGo.GetComponent<RectTransform>();
            oddsRect.anchoredPosition = new Vector2(-35f, 33f);
            oddsRect.sizeDelta = new Vector2(60f, 32f);
            _oddsLabel = oddsGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_oddsLabel, 16, true, TextAlignmentOptions.Center);
            _oddsLabel.color = new Color(0.4f, 0.95f, 0.85f, 1f);

            // 3. 积分输入框
            var stakeLblGo = new GameObject("StakePre", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            stakeLblGo.transform.SetParent(bar, false);
            var stakeLblRect = stakeLblGo.GetComponent<RectTransform>();
            stakeLblRect.anchoredPosition = new Vector2(10f, 33f);
            stakeLblRect.sizeDelta = new Vector2(24f, 30f);
            var stakePre = stakeLblGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(stakePre, 14, false, TextAlignmentOptions.Center);
            stakePre.text = "投";
            stakePre.color = NativeTheme.TextMuted;

            _stakeInput = _ui.NumberInput(bar, "StakeInput", new Vector2(50f, 33f), new Vector2(52f, 34f), 15, OnStakeChanged);
            Transform inputTransform = bar.Find("StakeInput");
            if (inputTransform != null)
            {
                var inputBg = inputTransform.GetComponent<Image>();
                if (inputBg != null) _theme.ApplyInset(inputBg);
            }

            var stakePostGo = new GameObject("StakePost", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            stakePostGo.transform.SetParent(bar, false);
            var stakePostRect = stakePostGo.GetComponent<RectTransform>();
            stakePostRect.anchoredPosition = new Vector2(90f, 33f);
            stakePostRect.sizeDelta = new Vector2(24f, 30f);
            var stakePost = stakePostGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(stakePost, 14, false, TextAlignmentOptions.Center);
            stakePost.text = "分";
            stakePost.color = NativeTheme.TextMuted;

            // 4. 操作按钮（下注 / 加注 / 退半 / 清仓）
            _btnBuy = _theme.CreateButton(bar, "BtnBuy", "下注", new Vector2(155f, 33f), new Vector2(90f, 38f), NativeBtnStyle.Primary, OnBuyClicked, 16);
            _btnSellHalf = _theme.CreateButton(bar, "BtnSellHalf", "退半", new Vector2(220f, 33f), new Vector2(58f, 38f), NativeBtnStyle.Caution, OnSellHalfClicked, 14);
            _btnSellAll = _theme.CreateButton(bar, "BtnSellAll", "清仓", new Vector2(285f, 33f), new Vector2(58f, 38f), NativeBtnStyle.Danger, OnSellAllClicked, 14);

            // 5. 实时权益与预测预览
            var prevGo = new GameObject("PreviewLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            prevGo.transform.SetParent(bar, false);
            var prevRect = prevGo.GetComponent<RectTransform>();
            prevRect.anchoredPosition = new Vector2(350f, 33f);
            prevRect.sizeDelta = new Vector2(100f, 34f);
            _previewLabel = prevGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_previewLabel, 13, false, TextAlignmentOptions.Left);
            _previewLabel.color = NativeTheme.TextParchment;

            // 6. 快捷按钮（走势、总览、折叠）
            _btnTrend = _theme.CreateButton(bar, "BtnTrend", "走势", new Vector2(415f, 33f), new Vector2(42f, 32f), NativeBtnStyle.Secondary, OnTrendClicked, 13);
            _btnOverview = _theme.CreateButton(bar, "BtnOverview", "总览", new Vector2(450f, 33f), new Vector2(42f, 32f), NativeBtnStyle.Secondary, OnOverviewClicked, 13);
            _btnCollapse = _theme.CreateButton(bar, "BtnCollapse", "▾", new Vector2(480f, 33f), new Vector2(22f, 24f), NativeBtnStyle.Pill, OnCollapseClicked, 14);
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

            // 智能自动停靠手牌区域
            bool hasHandCards;
            bool hasCenteredTalents;
            GameBridge.InspectBottomHandZone(out hasHandCards, out hasCenteredTalents);

            float targetY = PanelLayout.BetBarAutoY(hasHandCards, hasCenteredTalents);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0f);
            _rect.anchoredPosition = new Vector2(PanelLayout.BetBarDefaultXOffset, targetY);
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

            if ((screen - _downScreen).sqrMagnitude >= DragThreshold * DragThreshold)
            {
                _dragged = true;
            }

            if (_dragged && _parent != null)
            {
                Rect bounds = _parent.rect;
                if (bounds.width > 0 && bounds.height > 0)
                {
                    double nx = _startX + (local.x - _downLocal.x) / bounds.width;
                    double ny = _startY + (local.y - _downLocal.y) / bounds.height;
                    _layout.MoveBetBar(nx, ny, PanelLayout.BetBarWidth / bounds.width, PanelLayout.BetBarHeight / bounds.height);
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
            _lastFocusUid = focusUid;
            MatchSnapshot snapshot = _mod.Snapshot;
            _lastMarketId = snapshot == null ? null : snapshot.Id;

            if (snapshot == null || !snapshot.IsEightPlayerMatch())
            {
                if (_root != null) _root.SetActive(false);
                return;
            }

            int playerIndex = -1;
            for (int i = 0; i < 8; i++)
            {
                if (snapshot.Players[i].Uid == focusUid)
                {
                    playerIndex = i;
                    break;
                }
            }

            if (playerIndex < 0)
            {
                if (_root != null) _root.SetActive(false);
                return;
            }

            _lastPlayerIndex = playerIndex;
            Player player = snapshot.Players[playerIndex];
            Market market = _mod.Current;
            _mod.Drafts.Observe(market);

            // 1. 刷新田忌称号与头像
            HorseMember member = _mod.Horses != null ? _mod.Horses.FindMember(player.Uid) : null;
            if (member != null && !string.IsNullOrEmpty(member.Title))
            {
                _horseTitle.text = member.Caption;
                _horseTitle.color = TianJiFeature.ColorOf(member.Title);
                Sprite horseSp = _mod.Horses.GetSprite(member.Title);
                _horseIcon.sprite = horseSp;
                _horseIcon.gameObject.SetActive(horseSp != null);
                _playerElo.text = "Elo " + PredictionMod.N((long)member.Profile.Rating.Score);
            }
            else
            {
                _horseTitle.text = "";
                _horseIcon.gameObject.SetActive(false);
                _playerElo.text = "";
            }

            _playerName.text = player.Name;

            // 2. 刷新目标名次与实时倍率
            int chosenRank = _mod.Drafts.Selected(playerIndex);
            _rankLabel.text = chosenRank > 0 ? "猜第 " + chosenRank.ToString(CultureInfo.InvariantCulture) + " 名" : "未选名次";

            bool open = market != null && market.TradingWindowOpen;
            int rateBps = 0;
            if (market != null && chosenRank >= 1 && chosenRank <= 8)
            {
                rateBps = market.IsTrading ? market.QuoteRates[playerIndex * 8 + chosenRank - 1] : Odds.EffectiveRate(market.BaseRates[playerIndex * 8 + chosenRank - 1], Math.Max(1, Math.Min(5, market.HighestRound)));
                _oddsLabel.text = PredictionMod.Rate(rateBps);
            }
            else
            {
                _oddsLabel.text = "—";
            }

            // 3. 刷新积分输入框
            string draftStake = _mod.Drafts.Stake(playerIndex);
            if (_stakeInput != null && _stakeInput.Text != draftStake)
            {
                _stakeInput.SetText(draftStake);
            }

            // 4. 刷新持仓与操作按钮
            Position pos = market != null && market.IsTrading ? market.Positions[playerIndex] : default(Position);
            bool hasPosition = pos.Claim > 0;

            if (!hasPosition)
            {
                _btnBuy.SetText("下注");
                _btnBuy.GameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(175f, 33f);
                _btnBuy.GameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(90f, 38f);
                _btnSellHalf.SetActive(false);
                _btnSellAll.SetActive(false);

                if (chosenRank >= 1 && chosenRank <= 8 && market != null)
                {
                    long stakeVal;
                    if (long.TryParse(draftStake, NumberStyles.None, CultureInfo.InvariantCulture, out stakeVal) && stakeVal > 0 && rateBps > 0)
                    {
                        long win = stakeVal * rateBps / 10000;
                        _previewLabel.text = "正中 " + PredictionMod.N(win) + "分\n差1名 " + PredictionMod.N(win / 4) + "分";
                    }
                    else
                    {
                        _previewLabel.text = "输入积分预览";
                    }
                }
                else
                {
                    _previewLabel.text = "选择名次下注";
                }
            }
            else
            {
                bool isSameRank = chosenRank == pos.Rank;
                _btnBuy.SetText(isSameRank ? "加注" : "改猜");
                _btnBuy.GameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(150f, 33f);
                _btnBuy.GameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(76f, 38f);
                _btnSellHalf.SetActive(true);
                _btnSellAll.SetActive(true);

                long sale = TradingOdds.Sale(market, playerIndex, false);
                _previewLabel.text = "持仓: 猜" + pos.Rank.ToString(CultureInfo.InvariantCulture) + "名 (" + PredictionMod.N(pos.Claim) + "分)\n参考清仓: " + PredictionMod.N(sale) + "分";
            }

            _btnBuy.SetInteractable(open && chosenRank >= 1 && chosenRank <= 8);
            _btnSellHalf.SetInteractable(open && hasPosition);
            _btnSellAll.SetInteractable(open && hasPosition);
        }

        void OnMinusClicked()
        {
            if (_lastPlayerIndex < 0) return;
            _mod.Drafts.Step(_lastPlayerIndex, false);
            Refresh(_lastFocusUid);
        }

        void OnPlusClicked()
        {
            if (_lastPlayerIndex < 0) return;
            _mod.Drafts.Step(_lastPlayerIndex, true);
            Refresh(_lastFocusUid);
        }

        void OnStakeChanged(string text)
        {
            if (_lastPlayerIndex >= 0)
            {
                _mod.Drafts.SetStake(_lastPlayerIndex, text);
            }
        }

        void OnBuyClicked()
        {
            if (_lastPlayerIndex < 0) return;
            MatchSnapshot snapshot = _mod.Snapshot;
            Market market = _mod.Current;
            if (snapshot == null || market == null) return;

            string uid = snapshot.Players[_lastPlayerIndex].Uid;
            int rank = _mod.Drafts.Selected(_lastPlayerIndex);
            long stake;
            if (!long.TryParse(_mod.Drafts.Stake(_lastPlayerIndex), NumberStyles.None, CultureInfo.InvariantCulture, out stake) || stake <= 0)
            {
                _mod.SetStatus("请输入有效投注积分");
                return;
            }

            Position pos = market.Positions[_lastPlayerIndex];
            int action = pos.Claim > 0 ? (pos.Rank == rank ? 0 : 3) : 0;
            _mod.Trade(action, uid, rank, stake, market.TradeVersion, uid, snapshot.Id);
            Refresh(_lastFocusUid);
        }

        void OnSellHalfClicked()
        {
            if (_lastPlayerIndex < 0) return;
            MatchSnapshot snapshot = _mod.Snapshot;
            Market market = _mod.Current;
            if (snapshot == null || market == null) return;
            string uid = snapshot.Players[_lastPlayerIndex].Uid;
            _mod.Trade(1, uid, 0, 0, market.TradeVersion, uid, snapshot.Id);
            Refresh(_lastFocusUid);
        }

        void OnSellAllClicked()
        {
            if (_lastPlayerIndex < 0) return;
            MatchSnapshot snapshot = _mod.Snapshot;
            Market market = _mod.Current;
            if (snapshot == null || market == null) return;
            string uid = snapshot.Players[_lastPlayerIndex].Uid;
            _mod.Trade(2, uid, 0, 0, market.TradeVersion, uid, snapshot.Id);
            Refresh(_lastFocusUid);
        }

        void OnTrendClicked()
        {
            if (_lastPlayerIndex >= 0)
            {
                _panel.ShowTrend(_lastPlayerIndex);
            }
        }

        void OnOverviewClicked()
        {
            _panel.Toggle();
        }

        void OnCollapseClicked()
        {
            _collapsed = !_collapsed;
            _btnCollapse.SetText(_collapsed ? "▴" : "▾");
            if (_rect != null)
            {
                _rect.sizeDelta = new Vector2(PanelLayout.BetBarWidth, _collapsed ? 32f : PanelLayout.BetBarHeight);
            }
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
