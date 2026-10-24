#if YX_GAME
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Unity;

namespace YxPrediction
{
    public sealed class PredictionPanel
    {
        enum Tab { Overview, Trend }

        readonly PredictionMod _mod;
        readonly ModContext _ctx;
        readonly NativeTheme _theme;
        readonly PanelLayout _layout;
        readonly UiKit _ui;

        GameObject _root;
        RectTransform _rect;
        RectTransform _parent;
        CanvasGroup _canvasGroup;

        // 顶栏横幅手柄
        GameObject _bannerGo;
        RectTransform _bannerRect;
        TextMeshProUGUI _titleLabel;
        TextMeshProUGUI _statusLabel;
        NativeButton _btnTab;
        NativeButton _btnDetail;
        NativeButton _btnHistory;
        NativeButton _btnHorses;
        NativeButton _btnPeek;
        NativeButton _btnMinimize;
        NativeButton _btnClose;
        NativeButton _btnPrevHistory;
        NativeButton _btnNextHistory;

        // 窗口主体内容
        GameObject _bodyContainer;
        CanvasGroup _bodyCanvasGroup;
        GameObject _overviewTabContainer;
        GameObject _trendTabContainer;

        // 表头
        GameObject _detailedHeader;
        GameObject _simpleHeader;

        // 八人总览行
        readonly BetRow[] _rows = new BetRow[8];

        // 走势 Tab
        readonly NativeButton[] _trendPlayerBtns = new NativeButton[8];
        NativeButton _btnFollowFocus;
        Image _trendHorseIcon;
        TextMeshProUGUI _trendHorseTitle;
        TextMeshProUGUI _trendHorseDetail;
        TextMeshProUGUI _trendPlayerName;
        LifeChart _lifeChart;
        LifeChart _lifeShareChart;

        // 底栏
        TextMeshProUGUI _footerLabel;

        // 交互与状态
        bool _visible;
        bool _detailed;
        bool _history;
        int _historyIndex;
        Tab _currentTab = Tab.Overview;
        int _trendPlayer;
        bool _followFocusInTrend = true;
        float _nextRefresh;
        string _lastFocusUid;
        string _lastMarketId;

        // 拖动状态
        bool _pressed;
        bool _dragged;
        Vector2 _downScreen;
        Vector2 _downLocal;
        double _startX;
        double _startY;
        int _enableAfterFrame = -1;

        public PredictionPanel(PredictionMod mod, ModContext ctx, NativeTheme theme, PanelLayout layout)
        {
            _mod = mod;
            _ctx = ctx;
            _theme = theme;
            _layout = layout;
            _ui = new UiKit(ctx);
        }

        public void Toggle()
        {
            if (!PredictionVisibility.CanShow()) { Close(); return; }
            if (_visible) { Close(); return; }
            if (_root == null) Build();
            if (_root != null)
            {
                _visible = true;
                _history = false;
                _currentTab = Tab.Overview;
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
                Refresh();
            }
        }

        public void ShowTrend(int playerIndex)
        {
            if (!_visible) Toggle();
            _currentTab = Tab.Trend;
            _followFocusInTrend = false;
            _trendPlayer = playerIndex >= 0 && playerIndex < 8 ? playerIndex : 0;
            Refresh();
        }

        public void Close()
        {
            _visible = false;
            FinishGesture();
            if (_root != null) _root.SetActive(false);
        }

        public void Hide()
        {
            Close();
        }

        public void Update()
        {
            if (!PredictionVisibility.CanShow())
            {
                if (_visible) Close();
                return;
            }

            if (!_visible || _root == null) return;

            string focusUid = FocusSelection.Uid(_mod.Snapshot);
            string marketId = _mod.Snapshot == null ? null : _mod.Snapshot.Id;

            HandleBannerDrag();

            if (Time.unscaledTime >= _nextRefresh || focusUid != _lastFocusUid || marketId != _lastMarketId)
            {
                _nextRefresh = Time.unscaledTime + 0.35f;
                Refresh();
            }
        }

        void Build()
        {
            try
            {
                Transform parentTransform = Ui.FindRootCanvas() != null ? Ui.FindRootCanvas().transform : null;
                if (parentTransform == null) return;
                _parent = parentTransform as RectTransform;

                // 根窗口：无全屏遮罩，仅窗口自身接收点击
                _root = new GameObject("PredictionOverviewWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                _root.transform.SetParent(_parent, false);

                _rect = _root.GetComponent<RectTransform>();
                var anchor = new Vector2((float)_layout.WindowX, (float)_layout.WindowY);
                _rect.anchorMin = _rect.anchorMax = anchor;
                _rect.pivot = new Vector2(0.5f, 0.5f);
                _rect.anchoredPosition = Vector2.zero;
                _rect.sizeDelta = new Vector2(PanelLayout.WindowWidth, PanelLayout.WindowHeight);

                _canvasGroup = _root.GetComponent<CanvasGroup>();
                var winBg = _root.GetComponent<Image>();
                _theme.ApplyWindowFrame(winBg);
                winBg.raycastTarget = true;

                Transform win = _root.transform;

                // 1. 顶栏卷轴横幅（作为拖动手柄）
                _bannerGo = new GameObject("TitleBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                _bannerGo.transform.SetParent(win, false);
                _bannerRect = _bannerGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(_bannerRect, 0f, 0f, PanelLayout.WindowWidth, 54f);

                var bannerImg = _bannerGo.GetComponent<Image>();
                _theme.ApplyBanner(bannerImg);
                bannerImg.raycastTarget = true;

                Transform banner = _bannerGo.transform;

                // 标题
                var titleGo = new GameObject("TitleLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(banner, false);
                var titleRect = titleGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(titleRect, 20f, -12f, 180f, 32f);
                _titleLabel = titleGo.GetComponent<TextMeshProUGUI>();
                _theme.ApplyTextStyle(_titleLabel, 18, true, TextAlignmentOptions.Left);
                _titleLabel.text = "弈仙预测总览";
                _titleLabel.color = NativeTheme.AntiqueGold;
                _titleLabel.raycastTarget = false;

                // 状态
                var statusGo = new GameObject("StatusLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                statusGo.transform.SetParent(banner, false);
                var statusRect = statusGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(statusRect, 205f, -15f, 320f, 26f);
                _statusLabel = statusGo.GetComponent<TextMeshProUGUI>();
                _theme.ApplyTextStyle(_statusLabel, 13, false, TextAlignmentOptions.Left);
                _statusLabel.color = NativeTheme.TextParchment;
                _statusLabel.raycastTarget = false;

                // 顶栏功能按钮（全部使用绝对TopLeft精确定位，保证零重叠）
                _btnTab = _theme.CreateButtonTopLeft(banner, "BtnTab", "局势走势", 560f, -11f, 76f, 32f, NativeBtnStyle.Secondary, OnTabClicked, 13);
                _btnDetail = _theme.CreateButtonTopLeft(banner, "BtnDetail", "详细", 644f, -11f, 56f, 32f, NativeBtnStyle.Secondary, OnDetailClicked, 13);
                _btnHistory = _theme.CreateButtonTopLeft(banner, "BtnHistory", "历史", 708f, -11f, 56f, 32f, NativeBtnStyle.Secondary, OnHistoryClicked, 13);
                _btnHorses = _theme.CreateButtonTopLeft(banner, "BtnHorses", "顶栏马牌:开", 772f, -11f, 96f, 32f, NativeBtnStyle.Secondary, OnHorsesClicked, 13);
                _btnPeek = _theme.CreateButtonTopLeft(banner, "BtnPeek", "透视", 876f, -11f, 54f, 32f, NativeBtnStyle.Secondary, OnPeekClicked, 13);
                _btnMinimize = _theme.CreateButtonTopLeft(banner, "BtnMinimize", "折起", 938f, -11f, 54f, 32f, NativeBtnStyle.Secondary, OnMinimizeClicked, 13);
                _btnClose = _theme.CreateButtonTopLeft(banner, "BtnClose", "×", 1000f, -11f, 44f, 32f, NativeBtnStyle.Close, Close, 20);

                // 历史前后翻页
                _btnPrevHistory = _theme.CreateButtonTopLeft(banner, "BtnPrevHist", "◀ 上局", 420f, -12f, 62f, 28f, NativeBtnStyle.Pill, OnPrevHistory, 12);
                _btnNextHistory = _theme.CreateButtonTopLeft(banner, "BtnNextHist", "下局 ▶", 488f, -12f, 62f, 28f, NativeBtnStyle.Pill, OnNextHistory, 12);
                _btnPrevHistory.SetActive(false);
                _btnNextHistory.SetActive(false);

                // 2. 窗口主体内容容器（沉浸式暗墨衬底，大幅提升文字与行的高对比度）
                _bodyContainer = new GameObject("BodyContainer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                _bodyContainer.transform.SetParent(win, false);
                var bodyRect = _bodyContainer.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(bodyRect, 12f, -54f, PanelLayout.WindowWidth - 24f, PanelLayout.WindowHeight - 64f);

                var bodyBg = _bodyContainer.GetComponent<Image>();
                bodyBg.color = new Color(0.06f, 0.08f, 0.10f, 0.95f);
                bodyBg.raycastTarget = true;
                _bodyCanvasGroup = _bodyContainer.GetComponent<CanvasGroup>();

                // 3. Tab 1: 八人总览容器
                _overviewTabContainer = new GameObject("OverviewTab", typeof(RectTransform));
                _overviewTabContainer.transform.SetParent(_bodyContainer.transform, false);
                var overviewRect = _overviewTabContainer.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(overviewRect, 0f, 0f, PanelLayout.WindowWidth - 24f, PanelLayout.WindowHeight - 96f);

                BuildOverviewColumns(_overviewTabContainer.transform);
                for (int i = 0; i < 8; i++)
                {
                    _rows[i] = new BetRow(this, _overviewTabContainer.transform, i);
                    _rows[i].SetSlot(i);
                }

                // 4. Tab 2: 局势走势容器
                _trendTabContainer = new GameObject("TrendTab", typeof(RectTransform));
                _trendTabContainer.transform.SetParent(_bodyContainer.transform, false);
                var trendRect = _trendTabContainer.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(trendRect, 0f, 0f, PanelLayout.WindowWidth - 24f, PanelLayout.WindowHeight - 96f);

                BuildTrendTab(_trendTabContainer.transform);

                // 5. 底栏消息
                var footerGo = new GameObject("FooterLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                footerGo.transform.SetParent(_bodyContainer.transform, false);
                var footerRect = footerGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(footerRect, 10f, -(PanelLayout.WindowHeight - 90f), PanelLayout.WindowWidth - 44f, 24f);
                _footerLabel = footerGo.GetComponent<TextMeshProUGUI>();
                _theme.ApplyTextStyle(_footerLabel, 13, false, TextAlignmentOptions.Center);
                _footerLabel.color = NativeTheme.TextMuted;
                _footerLabel.raycastTarget = false;

                _trendTabContainer.SetActive(false);
            }
            catch (Exception ex)
            {
                _ctx.Log.Error("PredictionPanel.Build error: " + ex);
            }
        }

        void BuildOverviewColumns(Transform parent)
        {
            _detailedHeader = new GameObject("DetailedHeader", typeof(RectTransform));
            _detailedHeader.transform.SetParent(parent, false);
            var dRect = _detailedHeader.GetComponent<RectTransform>();
            NativeTheme.SetTopLeft(dRect, 0f, -4f, PanelLayout.WindowWidth - 24f, 26f);

            CreateColHeader(_detailedHeader.transform, "玩家 / 命元", 16f, 160f);
            CreateColHeader(_detailedHeader.transform, "田忌马牌 / 称号", 186f, 140f);
            CreateColHeader(_detailedHeader.transform, "目标名次", 336f, 120f);
            CreateColHeader(_detailedHeader.transform, "倍率", 466f, 74f);
            CreateColHeader(_detailedHeader.transform, "投入", 550f, 64f);
            CreateColHeader(_detailedHeader.transform, "调仓操作", 624f, 200f);
            CreateColHeader(_detailedHeader.transform, "持仓与结算预估", 830f, 200f);

            _simpleHeader = new GameObject("SimpleHeader", typeof(RectTransform));
            _simpleHeader.transform.SetParent(parent, false);
            var sRect = _simpleHeader.GetComponent<RectTransform>();
            NativeTheme.SetTopLeft(sRect, 0f, -4f, PanelLayout.WindowWidth - 24f, 26f);

            CreateColHeader(_simpleHeader.transform, "玩家", 16f, 160f);
            CreateColHeader(_simpleHeader.transform, "田忌马牌", 186f, 140f);
            CreateColHeader(_simpleHeader.transform, "猜第几名", 336f, 120f);
            CreateColHeader(_simpleHeader.transform, "倍率", 466f, 74f);
            CreateColHeader(_simpleHeader.transform, "投入积分", 550f, 64f);
            CreateColHeader(_simpleHeader.transform, "操作", 624f, 200f);
            CreateColHeader(_simpleHeader.transform, "我的预测与返还", 830f, 200f);

            _detailedHeader.SetActive(false);
        }

        void CreateColHeader(Transform parent, string text, float x, float width)
        {
            var go = new GameObject("Col_" + text, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            NativeTheme.SetTopLeft(rect, x, 0f, width, 24f);

            var label = go.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(label, 13, true, TextAlignmentOptions.Left);
            label.text = text;
            label.color = NativeTheme.AntiqueGold;
            label.raycastTarget = false;
        }

        void BuildTrendTab(Transform parent)
        {
            // 顶部 8 玩家选择卡片 + 跟随当前
            float btnW = 86f;
            float startX = 14f;
            for (int i = 0; i < 8; i++)
            {
                int pIndex = i;
                _trendPlayerBtns[i] = _theme.CreateButtonTopLeft(parent, "TrendP_" + PredictionMod.N(i), "玩家" + PredictionMod.N(i + 1),
                    startX + i * (btnW + 6f), -6f, btnW, 28f, NativeBtnStyle.Secondary,
                    delegate { _followFocusInTrend = false; _trendPlayer = pIndex; Refresh(); }, 12);
            }

            _btnFollowFocus = _theme.CreateButtonTopLeft(parent, "BtnFollowFocus", "跟随观战:开",
                startX + 8 * (btnW + 6f) + 6f, -6f, 100f, 28f, NativeBtnStyle.Primary,
                delegate { _followFocusInTrend = !_followFocusInTrend; Refresh(); }, 12);

            // 田忌个人档案信息条
            var cardGo = new GameObject("TrendProfileCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardGo.transform.SetParent(parent, false);
            var cardRect = cardGo.GetComponent<RectTransform>();
            NativeTheme.SetTopLeft(cardRect, 14f, -40f, PanelLayout.WindowWidth - 52f, 48f);
            var cardImg = cardGo.GetComponent<Image>();
            _theme.ApplyInset(cardImg);

            Transform card = cardGo.transform;

            var iconGo = new GameObject("CardHorseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(card, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            NativeTheme.SetTopLeft(iconRect, 10f, -6f, 36f, 36f);
            _trendHorseIcon = iconGo.GetComponent<Image>();
            _trendHorseIcon.preserveAspect = true;
            _trendHorseIcon.raycastTarget = false;

            var titleGo = new GameObject("CardHorseTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(card, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            NativeTheme.SetTopLeft(titleRect, 56f, -4f, 200f, 22f);
            _trendHorseTitle = titleGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_trendHorseTitle, 15, true, TextAlignmentOptions.Left);
            _trendHorseTitle.raycastTarget = false;

            var pNameGo = new GameObject("CardPlayerName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            pNameGo.transform.SetParent(card, false);
            var pNameRect = pNameGo.GetComponent<RectTransform>();
            NativeTheme.SetTopLeft(pNameRect, 56f, -24f, 200f, 20f);
            _trendPlayerName = pNameGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_trendPlayerName, 12, false, TextAlignmentOptions.Left);
            _trendPlayerName.color = NativeTheme.TextMuted;
            _trendPlayerName.raycastTarget = false;

            var detailGo = new GameObject("CardHorseDetail", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            detailGo.transform.SetParent(card, false);
            var detailRect = detailGo.GetComponent<RectTransform>();
            NativeTheme.SetTopLeft(detailRect, 270f, -4f, 750f, 38f);
            _trendHorseDetail = detailGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_trendHorseDetail, 13, false, TextAlignmentOptions.Left);
            _trendHorseDetail.color = NativeTheme.TextParchment;
            _trendHorseDetail.raycastTarget = false;

            // 双图表并排展示
            _lifeChart = new LifeChart(_ui, parent, TrendChartData.LifeMetric, 14f, -96f, 520f, 380f, _theme);
            _lifeShareChart = new LifeChart(_ui, parent, TrendChartData.ShareMetric, 550f, -96f, 520f, 380f, _theme);
        }

        void HandleBannerDrag()
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

            if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(_bannerRect, screen, camera))
            {
                _pressed = true;
                _dragged = false;
                _downScreen = screen;
                _downLocal = local;
                _startX = _layout.WindowX;
                _startY = _layout.WindowY;
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
                    _layout.MoveWindow(newX, newY, bounds.width, bounds.height);
                    if (_rect != null)
                    {
                        var anchor = new Vector2((float)_layout.WindowX, (float)_layout.WindowY);
                        _rect.anchorMin = _rect.anchorMax = anchor;
                        _rect.anchoredPosition = Vector2.zero;
                    }
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
                _ctx.Data.Set(PanelLayout.WindowSaveKey, _layout.WriteWindow());
            }
        }

        public Market Shown()
        {
            if (_history && _mod.Book != null)
            {
                var list = _mod.Book.Markets;
                if (list != null && _historyIndex >= 0 && _historyIndex < list.Count)
                {
                    return list[_historyIndex];
                }
            }
            return _mod.Current;
        }

        public void Refresh()
        {
            try
            {
                if (_root == null) return;
                _root.SetActive(_visible);
                if (!_visible) return;

                MatchSnapshot snapshot = _mod.Snapshot;
                _lastFocusUid = FocusSelection.Uid(snapshot);
                _lastMarketId = snapshot == null ? null : snapshot.Id;

                Market market = Shown();
                if (market != null) _mod.Drafts.Observe(market);

                // 1. 刷新顶栏标题与状态
                int curRound = snapshot != null ? snapshot.Round : (market != null ? market.QuoteRound : 1);
                string roundText = "第 " + PredictionMod.N(curRound) + " 轮";
                string statusDesc = market != null && market.Settled ? "已结算" : (market != null && market.TradingWindowOpen ? "交易开放" : (market != null ? "交易暂停" : "准备中"));
                _statusLabel.text = roundText + " · " + statusDesc + " · 余额: " + PredictionMod.N(_mod.Book != null ? _mod.Book.Balance : 1000) + " 分";

                _btnTab.SetText(_currentTab == Tab.Overview ? "局势走势" : "八人总览");
                _btnDetail.SetText(_detailed ? "简洁模式" : "详细数据");
                _btnHistory.SetText(_history ? "当前局" : "历史对局");
                _btnHorses.SetText(_mod.Horses != null && _mod.Horses.Enabled ? "顶栏马牌:开" : "顶栏马牌:关");

                // 历史翻页
                _btnPrevHistory.SetActive(_history);
                _btnNextHistory.SetActive(_history);
                if (_history && _mod.Book != null)
                {
                    int count = _mod.Book.Markets != null ? _mod.Book.Markets.Count : 0;
                    _btnPrevHistory.SetInteractable(_historyIndex > 0);
                    _btnNextHistory.SetInteractable(_historyIndex < count - 1);
                }

                // 2. 切换 Tab 容器
                _overviewTabContainer.SetActive(_currentTab == Tab.Overview);
                _trendTabContainer.SetActive(_currentTab == Tab.Trend);

                if (_currentTab == Tab.Overview)
                {
                    RefreshOverview(market);
                }
                else
                {
                    RefreshTrend(market);
                }

                // 3. 底栏信息
                _footerLabel.text = _mod.Message ?? (_mod.QueryStatus ?? "每人独立预测，可在右侧随时下注、退半、清仓或换预测。");
            }
            catch (Exception ex)
            {
                _ctx.Log.Error("PredictionPanel.Refresh error: " + ex);
            }
        }

        void RefreshOverview(Market market)
        {
            _detailedHeader.SetActive(_detailed);
            _simpleHeader.SetActive(!_detailed);

            int[] order = RankSelection.DisplayOrder(market);
            for (int slot = 0; slot < 8; slot++)
            {
                int playerIndex = order[slot];
                _rows[playerIndex].SetSlot(slot);
                _rows[playerIndex].Refresh(market, _detailed, _history);
            }
        }

        void RefreshTrend(Market market)
        {
            MatchSnapshot snapshot = _mod.Snapshot;
            if (snapshot == null || !snapshot.IsEightPlayerMatch()) return;

            if (_followFocusInTrend)
            {
                string focusUid = FocusSelection.Uid(snapshot);
                for (int i = 0; i < 8; i++)
                {
                    if (snapshot.Players[i].Uid == focusUid) _trendPlayer = i;
                }
            }
            _btnFollowFocus.SetText(_followFocusInTrend ? "跟随观战:开" : "跟随观战:关");

            for (int i = 0; i < 8; i++)
            {
                string pName = snapshot.Players[i].Name;
                if (pName.Length > 4) pName = pName.Substring(0, 4);
                _trendPlayerBtns[i].SetText(pName);
                _trendPlayerBtns[i].SetInteractable(i != _trendPlayer);
            }

            // 档案卡
            Player curP = snapshot.Players[_trendPlayer];
            _trendPlayerName.text = curP.Name + " (UID: " + curP.Uid + ")";

            HorseMember member = _mod.Horses != null ? _mod.Horses.FindMember(curP.Uid) : null;
            if (member != null && !string.IsNullOrEmpty(member.Title))
            {
                Sprite sp = _mod.Horses.GetSprite(member.Title);
                _trendHorseIcon.sprite = sp;
                _trendHorseIcon.gameObject.SetActive(sp != null);
                _trendHorseTitle.text = member.Caption;
                _trendHorseTitle.color = TianJiFeature.ColorOf(member.Title);
                _trendHorseDetail.text = _mod.Horses.DescribeFull(curP.Uid);
            }
            else
            {
                _trendHorseIcon.gameObject.SetActive(false);
                _trendHorseTitle.text = "暂无称号";
                _trendHorseDetail.text = "无公开Elo数据";
            }

            if (market != null)
            {
                _lifeChart.Refresh(market, _trendPlayer);
                _lifeShareChart.Refresh(market, _trendPlayer);
            }
        }

        void OnTabClicked()
        {
            _currentTab = _currentTab == Tab.Overview ? Tab.Trend : Tab.Overview;
            Refresh();
        }

        void OnDetailClicked()
        {
            _detailed = !_detailed;
            Refresh();
        }

        void OnHistoryClicked()
        {
            _history = !_history;
            if (_history && _mod.Book != null && _mod.Book.Markets != null)
            {
                _historyIndex = _mod.Book.Markets.Count - 1;
            }
            Refresh();
        }

        void OnPrevHistory()
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                Refresh();
            }
        }

        void OnNextHistory()
        {
            if (_mod.Book != null && _mod.Book.Markets != null && _historyIndex < _mod.Book.Markets.Count - 1)
            {
                _historyIndex++;
                Refresh();
            }
        }

        void OnHorsesClicked()
        {
            if (_mod.Horses != null)
            {
                _mod.Horses.Toggle();
                Refresh();
            }
        }

        void OnPeekClicked()
        {
            _layout.IsPeek = !_layout.IsPeek;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _layout.IsPeek ? PanelLayout.PeekAlpha : 1f;
                _canvasGroup.blocksRaycasts = !_layout.IsPeek;
            }
            _btnPeek.SetText(_layout.IsPeek ? "退出透视" : "透视");
        }

        void OnMinimizeClicked()
        {
            _layout.IsMinimized = !_layout.IsMinimized;
            if (_rect != null)
            {
                _rect.sizeDelta = new Vector2(PanelLayout.WindowWidth, _layout.IsMinimized ? PanelLayout.MinimizedHeight : PanelLayout.WindowHeight);
            }
            if (_bodyContainer != null)
            {
                _bodyContainer.SetActive(!_layout.IsMinimized);
            }
            _btnMinimize.SetText(_layout.IsMinimized ? "展开" : "折起");
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

        // 八人总览行
        sealed class BetRow
        {
            readonly PredictionPanel _panel;
            readonly int _playerIndex;
            readonly GameObject _rowGo;
            readonly RectTransform _rowRect;

            readonly Image _selfFlag;
            readonly Image _horseIcon;
            readonly TextMeshProUGUI _horseTitle;
            readonly TextMeshProUGUI _playerName;
            readonly TextMeshProUGUI _playerLife;

            readonly NativeButton _btnMinus;
            readonly NativeButton _btnPlus;
            readonly TextMeshProUGUI _rankLabel;
            readonly TextMeshProUGUI _oddsLabel;

            readonly UiInput _stakeInput;
            readonly NativeButton _btnBuy;
            readonly NativeButton _btnSellHalf;
            readonly NativeButton _btnSellAll;

            readonly TextMeshProUGUI _holdingLabel;
            readonly TextMeshProUGUI _resultLabel;
            readonly Image _dividerLine;

            public BetRow(PredictionPanel panel, Transform parent, int index)
            {
                _panel = panel;
                _playerIndex = index;

                _rowGo = new GameObject("Row_" + PredictionMod.N(index), typeof(RectTransform));
                _rowGo.transform.SetParent(parent, false);

                _rowRect = _rowGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(_rowRect, 0f, 0f, PanelLayout.WindowWidth - 24f, 54f);

                Transform row = _rowGo.transform;

                // 关注高亮光晕
                var flagGo = new GameObject("SelfFlag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                flagGo.transform.SetParent(row, false);
                var flagRect = flagGo.GetComponent<RectTransform>();
                flagRect.anchorMin = Vector2.zero;
                flagRect.anchorMax = Vector2.one;
                flagRect.sizeDelta = Vector2.zero;
                _selfFlag = flagGo.GetComponent<Image>();
                panel._theme.ApplySelfFlag(_selfFlag);
                _selfFlag.raycastTarget = false;
                _selfFlag.gameObject.SetActive(false);

                // 1. 玩家信息
                var nameGo = new GameObject("PlayerName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                nameGo.transform.SetParent(row, false);
                var nameRect = nameGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(nameRect, 16f, -8f, 160f, 22f);
                _playerName = nameGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_playerName, 14, true, TextAlignmentOptions.Left);
                _playerName.raycastTarget = false;

                var lifeGo = new GameObject("PlayerLife", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                lifeGo.transform.SetParent(row, false);
                var lifeRect = lifeGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(lifeRect, 16f, -30f, 160f, 20f);
                _playerLife = lifeGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_playerLife, 12, false, TextAlignmentOptions.Left);
                _playerLife.color = NativeTheme.TextMuted;
                _playerLife.raycastTarget = false;

                // 2. 田忌称号
                var iconGo = new GameObject("HorseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(row, false);
                var iconRect = iconGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(iconRect, 186f, -8f, 36f, 36f);
                _horseIcon = iconGo.GetComponent<Image>();
                _horseIcon.preserveAspect = true;
                _horseIcon.raycastTarget = false;

                var titleGo = new GameObject("HorseTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(row, false);
                var titleRect = titleGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(titleRect, 226f, -8f, 105f, 36f);
                _horseTitle = titleGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_horseTitle, 13, true, TextAlignmentOptions.Left);
                _horseTitle.raycastTarget = false;

                // 3. 目标名次与倍率
                _btnMinus = panel._theme.CreateButtonTopLeft(row, "Minus", "−", 336f, -11f, 28f, 32f, NativeBtnStyle.Pill, OnMinus, 16);

                var rankGo = new GameObject("RankLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rankGo.transform.SetParent(row, false);
                var rRect = rankGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(rRect, 368f, -11f, 56f, 32f);
                _rankLabel = rankGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_rankLabel, 14, true, TextAlignmentOptions.Center);
                _rankLabel.color = NativeTheme.AntiqueGold;

                _btnPlus = panel._theme.CreateButtonTopLeft(row, "Plus", "+", 428f, -11f, 28f, 32f, NativeBtnStyle.Pill, OnPlus, 16);

                var oddsGo = new GameObject("OddsLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                oddsGo.transform.SetParent(row, false);
                var oRect = oddsGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(oRect, 466f, -11f, 74f, 32f);
                _oddsLabel = oddsGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_oddsLabel, 15, true, TextAlignmentOptions.Center);
                _oddsLabel.color = new Color(0.4f, 0.95f, 0.85f, 1f);

                // 4. 积分输入
                _stakeInput = panel._ui.NumberInput(row, "Stake", new Vector2(550f, -11f), new Vector2(64f, 32f), 14, OnStake);
                Transform stakeT = row.Find("Stake");
                if (stakeT != null)
                {
                    var bg = stakeT.GetComponent<Image>();
                    if (bg != null) panel._theme.ApplyInset(bg);
                }

                // 5. 调仓操作按钮
                _btnBuy = panel._theme.CreateButtonTopLeft(row, "BtnBuy", "下注", 624f, -10f, 68f, 34f, NativeBtnStyle.Primary, OnBuy, 14);
                _btnSellHalf = panel._theme.CreateButtonTopLeft(row, "BtnHalf", "退半", 698f, -10f, 56f, 34f, NativeBtnStyle.Caution, OnHalf, 13);
                _btnSellAll = panel._theme.CreateButtonTopLeft(row, "BtnSell", "清仓", 760f, -10f, 56f, 34f, NativeBtnStyle.Danger, OnSell, 13);

                // 6. 权益与结果
                var holdingGo = new GameObject("HoldingLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                holdingGo.transform.SetParent(row, false);
                var hRect = holdingGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(hRect, 830f, -8f, 200f, 22f);
                _holdingLabel = holdingGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_holdingLabel, 13, false, TextAlignmentOptions.Left);
                _holdingLabel.color = NativeTheme.TextParchment;
                _holdingLabel.raycastTarget = false;

                var resultGo = new GameObject("ResultLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                resultGo.transform.SetParent(row, false);
                var resRect = resultGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(resRect, 830f, -30f, 200f, 20f);
                _resultLabel = resultGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_resultLabel, 12, false, TextAlignmentOptions.Left);
                _resultLabel.color = NativeTheme.AntiqueGold;
                _resultLabel.raycastTarget = false;

                // 7. 分隔线
                var lineGo = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                lineGo.transform.SetParent(row, false);
                var lineRect = lineGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(lineRect, 10f, -54f, PanelLayout.WindowWidth - 44f, 1.5f);
                _dividerLine = lineGo.GetComponent<Image>();
                panel._theme.ApplyLine(_dividerLine);
                _dividerLine.raycastTarget = false;
            }

            public void SetSlot(int slot)
            {
                if (_rowRect != null)
                {
                    _rowRect.anchoredPosition = new Vector2(0f, -32f - slot * 58f);
                }
            }

            public void Refresh(Market market, bool detailed, bool history)
            {
                MatchSnapshot snapshot = _panel._mod.Snapshot;
                if (snapshot == null || _playerIndex >= snapshot.Players.Length)
                {
                    _rowGo.SetActive(false);
                    return;
                }
                _rowGo.SetActive(true);

                Player player = snapshot.Players[_playerIndex];
                _playerName.text = player.Name;

                PlayerState state = snapshot.States != null && _playerIndex < snapshot.States.Length ? snapshot.States[_playerIndex] : default(PlayerState);
                _playerLife.text = "命元 " + PredictionMod.N(state.Life) + " · " + PredictionMod.N(state.Wins) + "胜" + PredictionMod.N(state.Losses) + "负";

                // 高亮关注中
                bool isFocus = FocusSelection.Uid(snapshot) == player.Uid;
                _selfFlag.gameObject.SetActive(isFocus);

                // 田忌称号与Elo
                HorseMember member = _panel._mod.Horses != null ? _panel._mod.Horses.FindMember(player.Uid) : null;
                if (member != null && !string.IsNullOrEmpty(member.Title))
                {
                    Sprite sp = _panel._mod.Horses.GetSprite(member.Title);
                    _horseIcon.sprite = sp;
                    _horseIcon.gameObject.SetActive(sp != null);

                    string eloText = "";
                    if (detailed && member.Profile != null && member.Profile.Rating.Found)
                    {
                        eloText = "\nElo " + PredictionMod.N((long)member.Profile.Rating.Score);
                    }
                    _horseTitle.text = member.Caption + eloText;
                    _horseTitle.color = TianJiFeature.ColorOf(member.Title);
                }
                else
                {
                    _horseIcon.gameObject.SetActive(false);
                    _horseTitle.text = "";
                }

                // 目标名次与倍率
                int chosenRank = _panel._mod.Drafts.Selected(_playerIndex);
                _rankLabel.text = chosenRank > 0 ? "第" + chosenRank.ToString(CultureInfo.InvariantCulture) + "名" : "—";

                bool open = market != null && market.TradingWindowOpen && !history;
                if (market != null && chosenRank >= 1 && chosenRank <= 8)
                {
                    int bps = market.IsTrading ? market.QuoteRates[_playerIndex * 8 + chosenRank - 1] : Odds.EffectiveRate(market.BaseRates[_playerIndex * 8 + chosenRank - 1], Math.Max(1, Math.Min(5, market.HighestRound)));
                    _oddsLabel.text = PredictionMod.Rate(bps);
                }
                else
                {
                    _oddsLabel.text = "—";
                }

                // 积分输入
                string draftStake = _panel._mod.Drafts.Stake(_playerIndex);
                if (_stakeInput != null && _stakeInput.Text != draftStake)
                {
                    _stakeInput.SetText(draftStake);
                }

                // 持仓与调仓
                Position pos = market != null && market.IsTrading ? market.Positions[_playerIndex] : default(Position);
                bool hasPosition = pos.Claim > 0;

                if (!hasPosition)
                {
                    _btnBuy.SetText("下注");
                    _btnSellHalf.SetActive(false);
                    _btnSellAll.SetActive(false);
                    _holdingLabel.text = "未持仓";
                    _resultLabel.text = "";
                }
                else
                {
                    bool same = chosenRank == pos.Rank;
                    _btnBuy.SetText(same ? "加注" : "改猜");
                    _btnSellHalf.SetActive(true);
                    _btnSellAll.SetActive(true);

                    long sale = TradingOdds.Sale(market, _playerIndex, false);
                    _holdingLabel.text = "猜" + PredictionMod.N(pos.Rank) + "名 · 权益: " + PredictionMod.N(pos.Claim) + "分";
                    _resultLabel.text = "参考清仓: " + PredictionMod.N(sale) + "分 (差1名返" + PredictionMod.N(pos.Claim / 4) + "分)";
                }

                if (market != null && market.Settled)
                {
                    int finalR = market.FinalRanks != null && _playerIndex < market.FinalRanks.Length ? market.FinalRanks[_playerIndex] : 0;
                    _resultLabel.text = "最终第" + PredictionMod.N(finalR) + "名 · 返还 " + PredictionMod.N(pos.FinalPayout) + "分";
                }

                _btnBuy.SetInteractable(open && chosenRank >= 1 && chosenRank <= 8);
                _btnSellHalf.SetInteractable(open && hasPosition);
                _btnSellAll.SetInteractable(open && hasPosition);
            }

            void OnMinus()
            {
                _panel._mod.Drafts.Step(_playerIndex, false);
                _panel.Refresh();
            }

            void OnPlus()
            {
                _panel._mod.Drafts.Step(_playerIndex, true);
                _panel.Refresh();
            }

            void OnStake(string text)
            {
                _panel._mod.Drafts.SetStake(_playerIndex, text);
            }

            void OnBuy()
            {
                MatchSnapshot snapshot = _panel._mod.Snapshot;
                Market market = _panel.Shown();
                if (snapshot == null || market == null) return;

                int rank = _panel._mod.Drafts.Selected(_playerIndex);
                if (rank < 1 || rank > 8) return;

                long stake = 10;
                long parsed;
                if (long.TryParse(_panel._mod.Drafts.Stake(_playerIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
                {
                    stake = parsed;
                }

                string uid = snapshot.Players[_playerIndex].Uid;
                Position pos = market.Positions[_playerIndex];
                int action = pos.Claim > 0 ? (pos.Rank == rank ? 0 : 3) : 0;
                string result = _panel._mod.Trade(action, uid, rank, stake, market.TradeVersion);
                _panel.Refresh();
            }

            void OnHalf()
            {
                MatchSnapshot snapshot = _panel._mod.Snapshot;
                Market market = _panel.Shown();
                if (snapshot == null || market == null) return;
                string uid = snapshot.Players[_playerIndex].Uid;
                _panel._mod.Trade(1, uid, 0, 0, market.TradeVersion);
                _panel.Refresh();
            }

            void OnSell()
            {
                MatchSnapshot snapshot = _panel._mod.Snapshot;
                Market market = _panel.Shown();
                if (snapshot == null || market == null) return;
                string uid = snapshot.Players[_playerIndex].Uid;
                _panel._mod.Trade(2, uid, 0, 0, market.TradeVersion);
                _panel.Refresh();
            }
        }
    }
}
#endif
