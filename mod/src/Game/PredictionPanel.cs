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
    // 独立原生总览面板：墨玉鎏金仙侠画风，八人总览与局势走势，零全屏遮挡，支持拖动、透视与折起。
    public sealed class PredictionPanel
    {
        enum Tab { Overview, Trend }

        readonly PredictionMod _mod;
        readonly ModContext _ctx;
        readonly UiKit _ui;
        readonly NativeTheme _theme;
        readonly PanelLayout _layout;

        GameObject _root;
        RectTransform _rect;
        RectTransform _parent;
        CanvasGroup _canvasGroup;

        // 窗口部件
        GameObject _bannerGo;
        RectTransform _bannerRect;
        GameObject _bodyContainer;
        CanvasGroup _bodyCanvasGroup;
        GameObject _overviewTabContainer;
        GameObject _trendTabContainer;

        // 顶栏控件
        TextMeshProUGUI _titleLabel;
        TextMeshProUGUI _statusLabel;
        NativeButton _btnTab;
        NativeButton _btnDetail;
        NativeButton _btnHistory;
        NativeButton _btnHorses;
        NativeButton _btnPeek;
        NativeButton _btnMinimize;
        NativeButton _btnClose;

        // 历史切换
        NativeButton _btnPrevHistory;
        NativeButton _btnNextHistory;

        // 八人总览行
        readonly BetRow[] _rows = new BetRow[8];
        GameObject _simpleHeader, _detailedHeader;

        // 局势走势控件
        readonly NativeButton[] _trendPlayerBtns = new NativeButton[8];
        NativeButton _btnFollowFocus;
        Image _trendHorseIcon;
        TextMeshProUGUI _trendHorseTitle;
        TextMeshProUGUI _trendHorseDetail;
        TextMeshProUGUI _trendPlayerName;
        LifeChart _lifeChart;
        LifeChart _lifeShareChart;
        int _trendPlayer = 0;
        bool _followFocusInTrend = true;

        // 底栏状态
        TextMeshProUGUI _footerLabel;

        // 状态变量
        Tab _currentTab = Tab.Overview;
        bool _visible;
        bool _detailed;
        bool _history;
        int _historyIndex;
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
        const float DragThreshold = 6f;

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
            if (_visible) { Close(); return; }
            if (_root == null) Build();
            if (_root != null)
            {
                _visible = true;
                _history = false;
                _currentTab = Tab.Overview;
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
            winBg.raycastTarget = true; // 仅拦截窗口内部的鼠标点击

            Transform win = _root.transform;

            // 1. 顶栏卷轴横幅（作为拖动手柄）
            _bannerGo = new GameObject("TitleBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _bannerGo.transform.SetParent(win, false);
            _bannerRect = _bannerGo.GetComponent<RectTransform>();
            _bannerRect.anchorMin = new Vector2(0f, 1f);
            _bannerRect.anchorMax = new Vector2(1f, 1f);
            _bannerRect.pivot = new Vector2(0.5f, 1f);
            _bannerRect.anchoredPosition = Vector2.zero;
            _bannerRect.sizeDelta = new Vector2(0f, 54f);

            var bannerImg = _bannerGo.GetComponent<Image>();
            _theme.ApplyBanner(bannerImg);
            bannerImg.raycastTarget = true;

            Transform banner = _bannerGo.transform;

            // 标题与状态
            var titleGo = new GameObject("TitleLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(banner, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(24f, -27f);
            titleRect.sizeDelta = new Vector2(160f, 32f);
            _titleLabel = titleGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_titleLabel, 20, true, TextAlignmentOptions.Left);
            _titleLabel.text = "弈仙预测总览";
            _titleLabel.color = NativeTheme.AntiqueGold;
            _titleLabel.raycastTarget = false;

            var statusGo = new GameObject("StatusLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            statusGo.transform.SetParent(banner, false);
            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchoredPosition = new Vector2(200f, -27f);
            statusRect.sizeDelta = new Vector2(400f, 28f);
            _statusLabel = statusGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_statusLabel, 14, false, TextAlignmentOptions.Left);
            _statusLabel.color = NativeTheme.TextParchment;
            _statusLabel.raycastTarget = false;

            // 顶栏功能按钮
            _btnTab = _theme.CreateButton(banner, "BtnTab", "局势走势", new Vector2(620f - 570f, 0f), new Vector2(74f, 32f), NativeBtnStyle.Secondary, OnTabClicked, 13);
            _btnDetail = _theme.CreateButton(banner, "BtnDetail", "详细", new Vector2(700f - 570f, 0f), new Vector2(52f, 32f), NativeBtnStyle.Secondary, OnDetailClicked, 13);
            _btnHistory = _theme.CreateButton(banner, "BtnHistory", "历史", new Vector2(760f - 570f, 0f), new Vector2(52f, 32f), NativeBtnStyle.Secondary, OnHistoryClicked, 13);
            _btnHorses = _theme.CreateButton(banner, "BtnHorses", "顶栏马牌:开", new Vector2(850f - 570f, 0f), new Vector2(92f, 32f), NativeBtnStyle.Secondary, OnHorsesClicked, 13);
            _btnPeek = _theme.CreateButton(banner, "BtnPeek", "透视", new Vector2(935f - 570f, 0f), new Vector2(52f, 32f), NativeBtnStyle.Secondary, OnPeekClicked, 13);
            _btnMinimize = _theme.CreateButton(banner, "BtnMinimize", "折起", new Vector2(995f - 570f, 0f), new Vector2(52f, 32f), NativeBtnStyle.Secondary, OnMinimizeClicked, 13);
            _btnClose = _theme.CreateButton(banner, "BtnClose", "×", new Vector2(1060f - 570f, 0f), new Vector2(34f, 32f), NativeBtnStyle.Close, Close, 20);

            // 历史前后翻页
            _btnPrevHistory = _theme.CreateButton(banner, "BtnPrevHist", "◀ 上局", new Vector2(440f - 570f, 0f), new Vector2(60f, 28f), NativeBtnStyle.Pill, OnPrevHistory, 12);
            _btnNextHistory = _theme.CreateButton(banner, "BtnNextHist", "下局 ▶", new Vector2(505f - 570f, 0f), new Vector2(60f, 28f), NativeBtnStyle.Pill, OnNextHistory, 12);
            _btnPrevHistory.SetActive(false);
            _btnNextHistory.SetActive(false);

            // 2. 窗口主体内容容器
            _bodyContainer = new GameObject("BodyContainer", typeof(RectTransform), typeof(CanvasGroup));
            _bodyContainer.transform.SetParent(win, false);
            var bodyRect = _bodyContainer.GetComponent<RectTransform>();
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.sizeDelta = new Vector2(0f, -54f);
            bodyRect.anchoredPosition = new Vector2(0f, -27f);
            _bodyCanvasGroup = _bodyContainer.GetComponent<CanvasGroup>();

            // 3. Tab 1: 八人总览容器
            _overviewTabContainer = new GameObject("OverviewTab", typeof(RectTransform));
            _overviewTabContainer.transform.SetParent(_bodyContainer.transform, false);
            var overviewRect = _overviewTabContainer.GetComponent<RectTransform>();
            overviewRect.anchorMin = Vector2.zero;
            overviewRect.anchorMax = Vector2.one;
            overviewRect.sizeDelta = Vector2.zero;
            overviewRect.anchoredPosition = Vector2.zero;

            BuildOverviewColumns(_overviewTabContainer.transform);
            for (int i = 0; i < 8; i++)
            {
                _rows[i] = new BetRow(this, _overviewTabContainer.transform, i);
            }

            // 4. Tab 2: 局势走势容器
            _trendTabContainer = new GameObject("TrendTab", typeof(RectTransform));
            _trendTabContainer.transform.SetParent(_bodyContainer.transform, false);
            var trendRect = _trendTabContainer.GetComponent<RectTransform>();
            trendRect.anchorMin = Vector2.zero;
            trendRect.anchorMax = Vector2.one;
            trendRect.sizeDelta = Vector2.zero;
            trendRect.anchoredPosition = Vector2.zero;

            BuildTrendTab(_trendTabContainer.transform);

            // 5. 底栏消息
            var footerGo = new GameObject("FooterLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            footerGo.transform.SetParent(_bodyContainer.transform, false);
            var footerRect = footerGo.GetComponent<RectTransform>();
            footerRect.anchorMin = new Vector2(0f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.anchoredPosition = new Vector2(0f, 20f);
            footerRect.sizeDelta = new Vector2(-40f, 32f);
            _footerLabel = footerGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_footerLabel, 14, false, TextAlignmentOptions.Center);
            _footerLabel.color = NativeTheme.TextMuted;
            _footerLabel.raycastTarget = false;

            _trendTabContainer.SetActive(false);
        }

        void BuildOverviewColumns(Transform parent)
        {
            _detailedHeader = new GameObject("DetailedHeader", typeof(RectTransform));
            _detailedHeader.transform.SetParent(parent, false);
            var dRect = _detailedHeader.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0f, 1f);
            dRect.anchorMax = new Vector2(1f, 1f);
            dRect.anchoredPosition = new Vector2(0f, -22f);
            dRect.sizeDelta = new Vector2(0f, 26f);

            CreateColHeader(_detailedHeader.transform, "玩家 / 命元", 20f, 170f);
            CreateColHeader(_detailedHeader.transform, "田忌称号 / Elo", 200f, 150f);
            CreateColHeader(_detailedHeader.transform, "目标名次", 360f, 110f);
            CreateColHeader(_detailedHeader.transform, "买入倍率", 475f, 75f);
            CreateColHeader(_detailedHeader.transform, "投入", 555f, 65f);
            CreateColHeader(_detailedHeader.transform, "调仓操作", 630f, 210f);
            CreateColHeader(_detailedHeader.transform, "权益 / 参考报价 / 结果", 850f, 270f);

            _simpleHeader = new GameObject("SimpleHeader", typeof(RectTransform));
            _simpleHeader.transform.SetParent(parent, false);
            var sRect = _simpleHeader.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 1f);
            sRect.anchorMax = new Vector2(1f, 1f);
            sRect.anchoredPosition = new Vector2(0f, -22f);
            sRect.sizeDelta = new Vector2(0f, 26f);

            CreateColHeader(_simpleHeader.transform, "玩家", 20f, 180f);
            CreateColHeader(_simpleHeader.transform, "田忌马牌", 210f, 140f);
            CreateColHeader(_simpleHeader.transform, "猜第几名", 360f, 120f);
            CreateColHeader(_simpleHeader.transform, "投入积分", 490f, 80f);
            CreateColHeader(_simpleHeader.transform, "操作", 580f, 200f);
            CreateColHeader(_simpleHeader.transform, "我的预测与返还", 790f, 330f);

            _detailedHeader.SetActive(false);
        }

        void CreateColHeader(Transform parent, string text, float x, float width)
        {
            var go = new GameObject("Col_" + text, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 24f);

            var label = go.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(label, 14, true, TextAlignmentOptions.Left);
            label.text = text;
            label.color = NativeTheme.AntiqueGold;
            label.raycastTarget = false;
        }

        void BuildTrendTab(Transform parent)
        {
            // 顶部 8 玩家选择卡片 + 跟随当前
            float btnW = 90f;
            float startX = 25f;
            for (int i = 0; i < 8; i++)
            {
                int pIndex = i;
                _trendPlayerBtns[i] = _theme.CreateButton(parent, "TrendP_" + PredictionMod.N(i), "玩家" + PredictionMod.N(i + 1),
                    new Vector2(startX + i * (btnW + 6f) + btnW / 2f - 570f, 230f), new Vector2(btnW, 30f), NativeBtnStyle.Secondary,
                    delegate { _followFocusInTrend = false; _trendPlayer = pIndex; Refresh(); }, 13);
            }

            _btnFollowFocus = _theme.CreateButton(parent, "BtnFollowFocus", "跟随观战:开",
                new Vector2(startX + 8 * (btnW + 6f) + 60f - 570f, 230f), new Vector2(110f, 30f), NativeBtnStyle.Primary,
                delegate { _followFocusInTrend = !_followFocusInTrend; Refresh(); }, 13);

            // 田忌个人档案信息条
            var cardGo = new GameObject("TrendProfileCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardGo.transform.SetParent(parent, false);
            var cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.anchoredPosition = new Vector2(0f, 178f);
            cardRect.sizeDelta = new Vector2(1100f, 54f);
            var cardImg = cardGo.GetComponent<Image>();
            _theme.ApplyInset(cardImg);

            Transform card = cardGo.transform;

            var iconGo = new GameObject("CardHorseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(card, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(-510f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            _trendHorseIcon = iconGo.GetComponent<Image>();
            _trendHorseIcon.preserveAspect = true;
            _trendHorseIcon.raycastTarget = false;

            var titleGo = new GameObject("CardHorseTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(card, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(-360f, 10f);
            titleRect.sizeDelta = new Vector2(240f, 24f);
            _trendHorseTitle = titleGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_trendHorseTitle, 16, true, TextAlignmentOptions.Left);
            _trendHorseTitle.raycastTarget = false;

            var detailGo = new GameObject("CardHorseDetail", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            detailGo.transform.SetParent(card, false);
            var detailRect = detailGo.GetComponent<RectTransform>();
            detailRect.anchoredPosition = new Vector2(40f, 0f);
            detailRect.sizeDelta = new Vector2(560f, 42f);
            _trendHorseDetail = detailGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_trendHorseDetail, 13, false, TextAlignmentOptions.Left);
            _trendHorseDetail.color = NativeTheme.TextParchment;
            _trendHorseDetail.raycastTarget = false;

            var pNameGo = new GameObject("CardPlayerName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            pNameGo.transform.SetParent(card, false);
            var pNameRect = pNameGo.GetComponent<RectTransform>();
            pNameRect.anchoredPosition = new Vector2(-360f, -12f);
            pNameRect.sizeDelta = new Vector2(240f, 20f);
            _trendPlayerName = pNameGo.GetComponent<TextMeshProUGUI>();
            _theme.ApplyTextStyle(_trendPlayerName, 13, false, TextAlignmentOptions.Left);
            _trendPlayerName.color = NativeTheme.TextMuted;
            _trendPlayerName.raycastTarget = false;

            // 双图表并排展示（左：命元走势，右：命元占比走势）
            _lifeChart = new LifeChart(_ui, parent, TrendChartData.LifeMetric, -280f, -60f, 540f, 290f, _theme);
            _lifeShareChart = new LifeChart(_ui, parent, TrendChartData.ShareMetric, 280f, -60f, 540f, 290f, _theme);
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
                    _layout.MoveWindow(nx, ny, PanelLayout.WindowWidth / bounds.width, PanelLayout.WindowHeight / bounds.height);
                    var anchor = new Vector2((float)_layout.WindowX, (float)_layout.WindowY);
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
            if (_root == null) return;
            _root.SetActive(_visible);
            if (!_visible) return;

            MatchSnapshot snapshot = _mod.Snapshot;
            _lastFocusUid = FocusSelection.Uid(snapshot);
            _lastMarketId = snapshot == null ? null : snapshot.Id;

            Market market = Shown();
            _mod.Drafts.Observe(market);

            // 1. 刷新顶栏标题与状态
            int curRound = snapshot != null ? snapshot.Round : (market != null ? market.QuoteRound : 1);
            string roundText = "第 " + PredictionMod.N(curRound) + " 轮";
            string statusDesc = market != null && market.Settled ? "已结算" : (market != null && market.TradingWindowOpen ? "交易开放" : "交易暂停");
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
            _footerLabel.text = _mod.Message ?? (_mod.QueryStatus ?? "每人独立预测，可在右侧随时加注、换预测或退出。");
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
                int fIndex = FocusSelection.Index(market, snapshot);
                if (fIndex >= 0) _trendPlayer = fIndex;
            }

            _btnFollowFocus.SetText(_followFocusInTrend ? "跟随观战:开" : "跟随观战:关");

            for (int i = 0; i < 8; i++)
            {
                _trendPlayerBtns[i].SetText(snapshot.Players[i].Name);
                _trendPlayerBtns[i].Button.image.color = i == _trendPlayer ? NativeTheme.AntiqueGold : new Color(0.2f, 0.26f, 0.28f, 0.95f);
            }

            Player p = snapshot.Players[_trendPlayer];
            _trendPlayerName.text = p.Name;

            HorseMember member = _mod.Horses != null ? _mod.Horses.FindMember(p.Uid) : null;
            if (member != null && !string.IsNullOrEmpty(member.Title))
            {
                Sprite sp = _mod.Horses.GetSprite(member.Title);
                _trendHorseIcon.sprite = sp;
                _trendHorseIcon.gameObject.SetActive(sp != null);
                _trendHorseTitle.text = member.Caption;
                _trendHorseTitle.color = TianJiFeature.ColorOf(member.Title);
                _trendHorseDetail.text = _mod.Horses.DescribeFull(p.Uid);
            }
            else
            {
                _trendHorseIcon.gameObject.SetActive(false);
                _trendHorseTitle.text = "暂无称号";
                _trendHorseTitle.color = NativeTheme.TextMuted;
                _trendHorseDetail.text = "暂无公开数据";
            }

            _lifeChart.Refresh(market, _trendPlayer);
            _lifeShareChart.Refresh(market, _trendPlayer);
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
            if (_history && _mod.Book != null && _mod.Book.Markets != null && _mod.Book.Markets.Count > 0)
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
            if (_bodyCanvasGroup != null)
            {
                _bodyCanvasGroup.alpha = _layout.IsPeek ? PanelLayout.PeekAlpha : 1f;
                _bodyCanvasGroup.blocksRaycasts = !_layout.IsPeek;
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
                _rowRect.anchorMin = new Vector2(0f, 1f);
                _rowRect.anchorMax = new Vector2(1f, 1f);
                _rowRect.pivot = new Vector2(0.5f, 1f);
                _rowRect.sizeDelta = new Vector2(0f, 54f);

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
                nameRect.anchoredPosition = new Vector2(20f, -15f);
                nameRect.sizeDelta = new Vector2(160f, 24f);
                _playerName = nameGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_playerName, 15, true, TextAlignmentOptions.Left);
                _playerName.raycastTarget = false;

                var lifeGo = new GameObject("PlayerLife", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                lifeGo.transform.SetParent(row, false);
                var lifeRect = lifeGo.GetComponent<RectTransform>();
                lifeRect.anchoredPosition = new Vector2(20f, -36f);
                lifeRect.sizeDelta = new Vector2(160f, 20f);
                _playerLife = lifeGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_playerLife, 13, false, TextAlignmentOptions.Left);
                _playerLife.color = NativeTheme.TextMuted;
                _playerLife.raycastTarget = false;

                // 2. 田忌称号
                var iconGo = new GameObject("HorseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(row, false);
                var iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchoredPosition = new Vector2(215f, -27f);
                iconRect.sizeDelta = new Vector2(34f, 34f);
                _horseIcon = iconGo.GetComponent<Image>();
                _horseIcon.preserveAspect = true;
                _horseIcon.raycastTarget = false;

                var titleGo = new GameObject("HorseTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(row, false);
                var titleRect = titleGo.GetComponent<RectTransform>();
                titleRect.anchoredPosition = new Vector2(295f, -27f);
                titleRect.sizeDelta = new Vector2(120f, 30f);
                _horseTitle = titleGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_horseTitle, 14, true, TextAlignmentOptions.Left);
                _horseTitle.raycastTarget = false;

                // 3. 目标名次与倍率
                _btnMinus = panel._theme.CreateButton(row, "Minus", "−", new Vector2(370f, -27f), new Vector2(26f, 32f), NativeBtnStyle.Pill, OnMinus, 16);

                var rankGo = new GameObject("RankLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rankGo.transform.SetParent(row, false);
                var rRect = rankGo.GetComponent<RectTransform>();
                rRect.anchoredPosition = new Vector2(410f, -27f);
                rRect.sizeDelta = new Vector2(50f, 30f);
                _rankLabel = rankGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_rankLabel, 15, true, TextAlignmentOptions.Center);
                _rankLabel.color = NativeTheme.AntiqueGold;

                _btnPlus = panel._theme.CreateButton(row, "Plus", "+", new Vector2(450f, -27f), new Vector2(26f, 32f), NativeBtnStyle.Pill, OnPlus, 16);

                var oddsGo = new GameObject("OddsLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                oddsGo.transform.SetParent(row, false);
                var oRect = oddsGo.GetComponent<RectTransform>();
                oRect.anchoredPosition = new Vector2(505f, -27f);
                oRect.sizeDelta = new Vector2(65f, 30f);
                _oddsLabel = oddsGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_oddsLabel, 15, true, TextAlignmentOptions.Center);
                _oddsLabel.color = new Color(0.4f, 0.95f, 0.85f, 1f);

                // 4. 积分输入
                _stakeInput = panel._ui.NumberInput(row, "Stake", new Vector2(585f, -27f), new Vector2(56f, 32f), 14, OnStake);
                Transform stakeT = row.Find("Stake");
                if (stakeT != null)
                {
                    var bg = stakeT.GetComponent<Image>();
                    if (bg != null) panel._theme.ApplyInset(bg);
                }

                // 5. 调仓操作按钮
                _btnBuy = panel._theme.CreateButton(row, "BtnBuy", "下注", new Vector2(675f, -27f), new Vector2(68f, 34f), NativeBtnStyle.Primary, OnBuy, 14);
                _btnSellHalf = panel._theme.CreateButton(row, "BtnHalf", "退半", new Vector2(745f, -27f), new Vector2(52f, 34f), NativeBtnStyle.Caution, OnHalf, 13);
                _btnSellAll = panel._theme.CreateButton(row, "BtnSell", "清仓", new Vector2(802f, -27f), new Vector2(52f, 34f), NativeBtnStyle.Danger, OnSell, 13);

                // 6. 权益与结果
                var holdingGo = new GameObject("HoldingLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                holdingGo.transform.SetParent(row, false);
                var hRect = holdingGo.GetComponent<RectTransform>();
                hRect.anchoredPosition = new Vector2(980f, -16f);
                hRect.sizeDelta = new Vector2(240f, 22f);
                _holdingLabel = holdingGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_holdingLabel, 13, false, TextAlignmentOptions.Left);
                _holdingLabel.color = NativeTheme.TextParchment;
                _holdingLabel.raycastTarget = false;

                var resultGo = new GameObject("ResultLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                resultGo.transform.SetParent(row, false);
                var resRect = resultGo.GetComponent<RectTransform>();
                resRect.anchoredPosition = new Vector2(980f, -36f);
                resRect.sizeDelta = new Vector2(240f, 20f);
                _resultLabel = resultGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_resultLabel, 13, false, TextAlignmentOptions.Left);
                _resultLabel.color = NativeTheme.AntiqueGold;
                _resultLabel.raycastTarget = false;

                // 7. 分隔线
                var lineGo = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                lineGo.transform.SetParent(row, false);
                var lineRect = lineGo.GetComponent<RectTransform>();
                lineRect.anchorMin = new Vector2(0f, 0f);
                lineRect.anchorMax = new Vector2(1f, 0f);
                lineRect.anchoredPosition = new Vector2(0f, 1f);
                lineRect.sizeDelta = new Vector2(-20f, 2f);
                _dividerLine = lineGo.GetComponent<Image>();
                panel._theme.ApplyLine(_dividerLine);
                _dividerLine.raycastTarget = false;
            }

            public void SetSlot(int slot)
            {
                if (_rowRect != null)
                {
                    _rowRect.anchoredPosition = new Vector2(0f, -50f - slot * 56f);
                }
            }

            public void Refresh(Market market, bool detailed, bool history)
            {
                MatchSnapshot snapshot = _panel._mod.Snapshot;
                if (snapshot == null || _playerIndex >= snapshot.Players.Length) return;

                Player player = snapshot.Players[_playerIndex];
                _playerName.text = player.Name;

                PlayerState state = snapshot.States != null && _playerIndex < snapshot.States.Length ? snapshot.States[_playerIndex] : default(PlayerState);
                _playerLife.text = "命元 " + PredictionMod.N(state.Life) + " · " + PredictionMod.N(state.Wins) + "胜" + PredictionMod.N(state.Losses) + "负";

                // 高亮关注中
                bool isFocus = FocusSelection.Uid(snapshot) == player.Uid;
                _selfFlag.gameObject.SetActive(isFocus);

                // 田忌称号
                HorseMember member = _panel._mod.Horses != null ? _panel._mod.Horses.FindMember(player.Uid) : null;
                if (member != null && !string.IsNullOrEmpty(member.Title))
                {
                    Sprite sp = _panel._mod.Horses.GetSprite(member.Title);
                    _horseIcon.sprite = sp;
                    _horseIcon.gameObject.SetActive(sp != null);
                    _horseTitle.text = member.Caption + (detailed ? "\nElo " + PredictionMod.N((long)member.Profile.Rating.Score) : "");
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

            void OnStake(string val)
            {
                _panel._mod.Drafts.SetStake(_playerIndex, val);
            }

            void OnBuy()
            {
                MatchSnapshot snapshot = _panel._mod.Snapshot;
                Market market = _panel.Shown();
                if (snapshot == null || market == null) return;

                string uid = snapshot.Players[_playerIndex].Uid;
                int rank = _panel._mod.Drafts.Selected(_playerIndex);
                long stake;
                if (!long.TryParse(_panel._mod.Drafts.Stake(_playerIndex), NumberStyles.None, CultureInfo.InvariantCulture, out stake) || stake <= 0)
                {
                    _panel._mod.SetStatus("请输入有效投注积分");
                    return;
                }

                Position pos = market.Positions[_playerIndex];
                int action = pos.Claim > 0 ? (pos.Rank == rank ? 0 : 3) : 0;
                _panel._mod.Trade(action, uid, rank, stake, market.TradeVersion, null, snapshot.Id);
                _panel.Refresh();
            }

            void OnHalf()
            {
                MatchSnapshot snapshot = _panel._mod.Snapshot;
                Market market = _panel.Shown();
                if (snapshot == null || market == null) return;
                string uid = snapshot.Players[_playerIndex].Uid;
                _panel._mod.Trade(1, uid, 0, 0, market.TradeVersion, null, snapshot.Id);
                _panel.Refresh();
            }

            void OnSell()
            {
                MatchSnapshot snapshot = _panel._mod.Snapshot;
                Market market = _panel.Shown();
                if (snapshot == null || market == null) return;
                string uid = snapshot.Players[_playerIndex].Uid;
                _panel._mod.Trade(2, uid, 0, 0, market.TradeVersion, null, snapshot.Id);
                _panel.Refresh();
            }
        }
    }
}
#endif
