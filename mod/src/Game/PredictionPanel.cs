#if YX_GAME
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Unity;

namespace YxPrediction
{
    public sealed class PredictionPanel
    {
        readonly PredictionMod _mod;
        readonly UiKit _ui;
        GameObject _root, _singleRoot;
        TextMeshProUGUI _header, _message, _footer;
        TextMeshProUGUI _singleHeader, _singleMessage;
        UiButton _previousHistory, _nextHistory;
        BetRow _singleRow;
        LifeChart _lifeChart, _lifeShareChart;
        readonly BetRow[] _rows = new BetRow[8];
        readonly RankSelection _rankSelection = new RankSelection();
        string _marketId;
        float _nextRefresh;
        bool _history;
        int _historyIndex;
        bool _visible, _expanded;
        bool _waitForFocusRelease;
        int _singleEnableAfterFrame = -1;
        string _focusUid, _focusMatchId, _singleMarketId, _singleUid;
        const float Width = 1120f, Height = 730f;
        const float SingleWidth = 740f, SingleHeight = 544f;
        bool SingleReady { get { return !_waitForFocusRelease && Time.frameCount > _singleEnableAfterFrame; } }

        public PredictionPanel(PredictionMod mod, ModContext ctx) { _mod = mod; _ui = new UiKit(ctx); }
        public void Toggle()
        {
            if (_visible) { Close(); return; }
            if (_root == null) Build();
            if (_root != null && _singleRoot != null) { _visible = true; _expanded = false; _history = false; Refresh(); }
        }
        public void Update()
        {
            if (!_visible || _root == null) return;
            if (_waitForFocusRelease && !Input.GetMouseButton(0))
            { _waitForFocusRelease = false; _singleEnableAfterFrame = Time.frameCount; _nextRefresh = 0; }
            if (!_waitForFocusRelease && _singleEnableAfterFrame >= 0 && Time.frameCount > _singleEnableAfterFrame)
            { _singleEnableAfterFrame = -1; _nextRefresh = 0; }
            string uid = FocusSelection.Uid(_mod.Snapshot);
            string matchId = _mod.Snapshot == null ? null : _mod.Snapshot.Id;
            if (uid == _focusUid && matchId == _focusMatchId && Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f; Refresh();
        }
        void Build()
        {
            var center = new Vector2(0.5f, 0.5f);
            _root = _ui.Panel("PredictionPoints", center, center, Vector2.zero, new Vector2(Width, Height), new Color(0.04f, 0.06f, 0.1f, 0.97f), true);
            if (_root == null) return;
            _header = Label("header", "", 18, -14, 850, 38, 23);
            Button("collapse", "收起", 886, -15, 68, Collapse);
            Button("history", "历史", 962, -15, 65, ToggleHistory);
            Button("close", "关闭", 1035, -15, 67, Close);
            _message = Label("message", "", 18, -55, 1080, 45, 18);
            Label("playerColumn", "玩家 / 命元", 18, -104, 174, 28, 17);
            Label("eloColumn", "Elo", 198, -104, 66, 28, 17);
            Label("rankColumn", "目标名次", 270, -104, 110, 28, 17);
            Label("oddsColumn", "买入倍率", 390, -104, 86, 28, 17);
            Label("stakeColumn", "买入积分", 482, -104, 74, 28, 17);
            Label("actionsColumn", "调仓操作", 562, -104, 264, 28, 17);
            Label("resultColumn", "持仓 / 参考报价 / 结算", 840, -104, 265, 28, 17);
            for (int i = 0; i < 8; i++) _rows[i] = new BetRow(this, i, -138 - i * 63);
            _footer = Label("footer", "", 18, -650, 1080, 70, 16);
            _previousHistory = Button("previousHistory", "上一局", 948, -687, 72, PreviousHistory);
            _nextHistory = Button("nextHistory", "下一局", 1030, -687, 72, NextHistory);
            _previousHistory.GameObject.SetActive(false); _nextHistory.GameObject.SetActive(false);
            _singleRoot = _ui.Panel("PredictionSinglePlayer", center, center, Vector2.zero, new Vector2(SingleWidth, SingleHeight), new Color(0.04f, 0.06f, 0.1f, 0.97f), true);
            if (_singleRoot == null) return;
            Transform single = _singleRoot.transform;
            _singleHeader = Label("singleHeader", "", 18, -14, 492, 32, 21, single);
            Button("expandAll", "展开全部", 528, -14, 108, Expand, single);
            Button("closeSingle", "关闭", 644, -14, 78, Close, single);
            _singleMessage = Label("singleMessage", "", 18, -121, 704, 34, 16, single);
            Label("singleRankColumn", "目标名次", 18, -157, 110, 22, 15, single);
            Label("singleOddsColumn", "买入倍率", 140, -157, 92, 22, 15, single);
            Label("singleStakeColumn", "买入积分", 238, -157, 80, 22, 15, single);
            Label("singleActionsColumn", "调仓操作", 330, -157, 310, 22, 15, single);
            _singleRow = new BetRow(this, -1, -183, true);
            _lifeChart = new LifeChart(_ui, single, TrendChartData.LifeMetric, 18, -332, 346);
            _lifeShareChart = new LifeChart(_ui, single, TrendChartData.ShareMetric, 376, -332, 346);
            _root.SetActive(false); _singleRoot.SetActive(false);
        }
        UiButton Button(string name, string text, float x, float y, float width, Action click, Transform parent = null)
        {
            UiButton button = _ui.TextButton(parent == null ? _root.transform : parent, name, text, new Vector2(x, y), new Vector2(width, 34), click);
            var label = button.GameObject.GetComponentInChildren(typeof(TextMeshProUGUI)) as TextMeshProUGUI;
            if (label != null) label.fontSize = 18;
            return button;
        }
        TextMeshProUGUI Label(string name, string text, float x, float y, float w, float h, float size, Transform parent = null)
        {
            var label = _ui.Label(parent == null ? _root.transform : parent, name, text, new Vector2(x, y), new Vector2(w, h), size);
            label.richText = false; return label;
        }
        Market Shown()
        {
            if (!_history) return _mod.Current;
            int count = _mod.Book.Markets.Count;
            if (count == 0) return null;
            _historyIndex = Math.Max(0, Math.Min(count - 1, _historyIndex));
            return _mod.Book.Markets[_historyIndex];
        }
        void Refresh()
        {
            if (!_visible) return;
            string uid = FocusSelection.Uid(_mod.Snapshot);
            string matchId = _mod.Snapshot == null ? null : _mod.Snapshot.Id;
            if (uid != _focusUid || matchId != _focusMatchId)
            {
                _focusUid = uid; _focusMatchId = matchId; _expanded = false; _history = false;
                // 切换时若鼠标仍按住，先等释放，防止旧玩家的按下事件在新玩家面板上成交。
                _waitForFocusRelease = Input.GetMouseButton(0); _singleEnableAfterFrame = Time.frameCount;
            }
            _root.SetActive(_expanded); _singleRoot.SetActive(!_expanded);
            _previousHistory.GameObject.SetActive(_expanded && _history);
            _nextHistory.GameObject.SetActive(_expanded && _history);
            Fit(_expanded ? _root : _singleRoot, _expanded ? Width : SingleWidth, _expanded ? Height : SingleHeight);
            if (!_expanded) { RefreshSingle(); return; }
            Market market = Shown();
            _rankSelection.Observe(market);
            string title = _history ? "观战预测 · 历史" : "观战预测 · 本地积分";
            long value = 0;
            if (market != null && market.IsTrading)
                for (int i = 0; i < 8; i++) value += TradingOdds.Sale(market, i, false);
            _header.text = title + "    余额 " + PredictionMod.N(_mod.Book.Balance)
                + (market != null && market.IsTrading ? "    本局参考清仓值 " + PredictionMod.N(value) : "");
            _message.text = _history ? "历史只读；再次点击「历史」返回当前观战。报价为保存时的参考值。" : _mod.Message;
            bool changed = _marketId != (market == null ? null : market.Id);
            _marketId = market == null ? null : market.Id;
            for (int i = 0; i < 8; i++) _rows[i].Refresh(market, changed);
            int[] order = RankSelection.DisplayOrder(market);
            for (int slot = 0; slot < 8; slot++) _rows[order[slot]].SetDisplaySlot(slot);
            if (market == null) _footer.text = (_mod.QueryStatus ?? "查分使用UID，不依赖昵称。") + "\n本地积分模式。认证时长未接入。";
            else if (market.IsTrading)
            {
                _footer.text = "差1名返25% | 出售折价3% | 前5轮利润加成8/6/4/2/0%";
            }
            else _footer.text = "旧盘口：每人固定一票，第5轮结束封盘，保留原赔率及结算规则。\n"
                + (market.HasAdjacentReward ? "差1名返正中总额25%。" : "仅正中返还。") + " 认证时长未接入。";
        }
        static void Fit(GameObject root, float width, float height)
        {
            var parent = root.transform.parent as RectTransform;
            if (parent == null) return;
            float scale = Math.Min(1f, Math.Min(parent.rect.width / (width + 24), parent.rect.height / (height + 24)));
            if (scale > 0) root.transform.localScale = new Vector3(scale, scale, 1);
        }
        void RefreshSingle()
        {
            Market market = _mod.Current;
            _rankSelection.Observe(market);
            int index = FocusSelection.Index(market, _mod.Snapshot);
            if (market == null && _focusUid != null && _mod.Snapshot != null)
                for (int i = 0; i < _mod.Snapshot.Players.Length; i++) if (_mod.Snapshot.Players[i].Uid == _focusUid) index = i;
            bool changed = _singleMarketId != (market == null ? null : market.Id) || _singleUid != _focusUid || _singleRow.PlayerIndex != index;
            _singleMarketId = market == null ? null : market.Id; _singleUid = _focusUid;
            _singleRow.PlayerIndex = index;
            _singleRow.Refresh(index < 0 ? null : market, changed);
            _singleHeader.text = "观战预测    余额 " + PredictionMod.N(_mod.Book.Balance);
            _singleMessage.text = _focusUid == null ? "等待当前观战对象，点击游戏中的玩家头像进行切换。" : _mod.Message;
            if (market != null && index >= 0 && (market.Settled || (market.IsTrading && market.Positions[index].Resolved)))
                _singleMessage.text = "当前玩家名次已确定，结算结果见下方。";
            _lifeChart.Refresh(market, index);
            _lifeShareChart.Refresh(market, index);
        }
        void Expand() { _expanded = true; _history = false; Refresh(); }
        void Collapse() { _expanded = false; _history = false; Refresh(); }
        void Close() { _visible = false; if (_root != null) _root.SetActive(false); if (_singleRoot != null) _singleRoot.SetActive(false); }
        public void Hide() { Close(); }
        void ToggleHistory() { _history = !_history; _historyIndex = _mod.Book.Markets.Count - 1; Refresh(); }
        void PreviousHistory() { if (_history) { _historyIndex--; Refresh(); } }
        void NextHistory() { if (_history) { _historyIndex++; Refresh(); } }
        public void Destroy() { _visible = false; _ui.DestroyAll(); _root = null; _singleRoot = null; }

        sealed class BetRow
        {
            readonly PredictionPanel _panel;
            int _index;
            readonly bool _single;
            public int PlayerIndex { get { return _index; } set { _index = value; } }
            readonly TextMeshProUGUI _name, _elo, _odds, _result, _preview;
            readonly UiButton _prev, _next, _buy, _half, _sell, _switch;
            readonly UiInput _stake, _rankInput;
            readonly RectTransform _row;
            TMP_InputField _rankField;
            int _rank, _version;
            string _shownId, _shownUid;
            public BetRow(PredictionPanel panel, int index, float y, bool single = false)
            {
                _panel = panel; _index = index; _single = single;
                string id = PredictionMod.N(index);
                if (single)
                {
                    Transform parent = panel._singleRoot.transform;
                    _name = panel.Label("focusedName", "", 18, -56, 440, 42, 28, parent);
                    _elo = panel.Label("focusedElo", "", 478, -56, 244, 42, 28, parent);
                    _name.fontStyle = FontStyles.Bold; _elo.fontStyle = FontStyles.Bold;
                    _name.enableWordWrapping = false; _elo.enableWordWrapping = false;
                    _name.overflowMode = TextOverflowModes.Ellipsis; _elo.overflowMode = TextOverflowModes.Ellipsis;
                    _prev = panel.Button("focusedPrev", "−", 18, y, 32, Previous, parent);
                    _rankInput = RankInput(parent, "focusedRank", 54, y);
                    _next = panel.Button("focusedNext", "+", 94, y, 32, Next, parent);
                    _odds = panel.Label("focusedOdds", "", 140, y, 92, 34, 18, parent);
                    _stake = panel._ui.NumberInput(parent, "focusedStake", new Vector2(238, y), new Vector2(80, 34), 5, OnStake);
                    _stake.SetText("10");
                    _buy = panel.Button("focusedBuy", "买入", 330, y, 64, Buy, parent);
                    _half = panel.Button("focusedHalf", "减半", 402, y, 64, Half, parent);
                    _sell = panel.Button("focusedSell", "清仓", 474, y, 64, Sell, parent);
                    _switch = panel.Button("focusedSwitch", "换预测", 546, y, 84, Switch, parent);
                    _preview = panel.Label("focusedPreview", "", 18, -221, 704, 28, 15, parent);
                    _result = panel.Label("focusedResult", "", 18, -252, 704, 68, 16, parent);
                    return;
                }
                GameObject row = panel._ui.Row(panel._root.transform, "playerRow" + id, new Vector2(0, y), new Vector2(Width, 63), Color.clear);
                _row = row.transform as RectTransform;
                Transform rowParent = row.transform;
                _name = panel.Label("name" + id, "", 18, 0, 174, 60, 16, rowParent);
                _elo = panel.Label("elo" + id, "", 198, 0, 66, 34, 16, rowParent);
                _prev = panel.Button("prev" + id, "−", 270, 0, 32, Previous, rowParent);
                _rankInput = RankInput(rowParent, "rank" + id, 306, 0);
                _next = panel.Button("next" + id, "+", 346, 0, 32, Next, rowParent);
                _odds = panel.Label("odds" + id, "", 390, 0, 86, 34, 16, rowParent);
                _stake = panel._ui.NumberInput(rowParent, "stake" + id, new Vector2(482, 0), new Vector2(74, 34), 5, OnStake);
                _stake.SetText("10");
                _buy = panel.Button("buy" + id, "买入", 562, 0, 58, Buy, rowParent);
                _half = panel.Button("half" + id, "减半", 626, 0, 58, Half, rowParent);
                _sell = panel.Button("sell" + id, "清仓", 690, 0, 58, Sell, rowParent);
                _switch = panel.Button("switch" + id, "换预测", 754, 0, 72, Switch, rowParent);
                _preview = panel.Label("preview" + id, "", 270, -36, 558, 24, 14, rowParent);
                _result = panel.Label("result" + id, "", 840, 0, 265, 60, 14, rowParent);
            }
            UiInput RankInput(Transform parent, string name, float x, float y)
            {
                UiInput input = _panel._ui.NumberInput(parent, name, new Vector2(x, y), new Vector2(36, 34), 2, OnRank);
                _rankField = parent.Find(name).GetComponent(typeof(TMP_InputField)) as TMP_InputField;
                if (_rankField != null)
                {
                    _rankField.textComponent.alignment = TextAlignmentOptions.Center;
                    var placeholder = _panel.Label(name + "Placeholder", "？", 0, 0, 24, 30, 20, _rankField.textViewport);
                    placeholder.alignment = TextAlignmentOptions.Center; placeholder.raycastTarget = false;
                    _rankField.placeholder = placeholder;
                }
                return input;
            }
            public void SetDisplaySlot(int slot) { if (_row != null) _row.anchoredPosition = new Vector2(0, -138 - slot * 63); }
            bool CanEditRank()
            {
                if (!_panel._visible || _panel._history || _single == _panel._expanded || (_single && !_panel.SingleReady)) return false;
                Market market = _panel.Shown(); MatchSnapshot snapshot = _panel._mod.Snapshot;
                if (market == null || market.Id != _shownId || _index < 0 || _index >= 8 || market.Players[_index].Uid != _shownUid
                    || snapshot == null || snapshot.Id != market.Id || !snapshot.LiveObserver || _panel._mod.PreparationError != null || market.Settled) return false;
                if (_single && FocusSelection.Uid(snapshot) != _shownUid) return false;
                return market.IsTrading ? !market.Positions[_index].Resolved : _panel._mod.Enabled && !market.Locked && market.Tickets[_index] == null;
            }
            void Previous() { if (CanEditRank()) { _panel._rankSelection.Step(_index, false); _panel.Refresh(); } }
            void Next() { if (CanEditRank()) { _panel._rankSelection.Step(_index, true); _panel.Refresh(); } }
            void OnRank(string value)
            {
                if (!CanEditRank()) return;
                if (!_panel._rankSelection.SelectText(_index, value))
                {
                    _panel._mod.SetStatus("名次请输入1～8，留空可取消选择");
                    _panel._rankSelection.SelectText(_index, "");
                    _rankInput.SetText("");
                }
                _panel.Refresh();
            }
            void OnStake(string value) { }
            void Buy() { Execute(0); }
            void Half() { Execute(1); }
            void Sell() { Execute(2); }
            void Switch() { Execute(3); }
            void Execute(int action)
            {
                if (_single && !_panel.SingleReady) return;
                Market market = _panel.Shown();
                if (market == null || market.Id != _shownId || _panel._history || _index < 0 || _index >= 8 || market.Players[_index].Uid != _shownUid) return;
                if ((action == 0 || action == 3) && (_rank < 1 || _rank > 8))
                { _panel._mod.SetStatus("请先选择目标名次（1～8）"); _panel.Refresh(); return; }
                long stake = 0;
                if ((action == 0 || action == 3) && !long.TryParse(_stake.Text, NumberStyles.None, CultureInfo.InvariantCulture, out stake))
                { _panel._mod.SetStatus("请输入整数积分"); _panel.Refresh(); return; }
                string uid = market.Players[_index].Uid;
                if (market.IsTrading) _panel._mod.Trade(action, uid, _rank, stake, _version, _single ? uid : null, _shownId);
                else if (action == 0) _panel._mod.PlaceBet(uid, _rank, stake, _single ? uid : null, _shownId);
                _panel.Refresh();
            }
            public void Refresh(Market market, bool changed)
            {
                if (changed)
                {
                    if (_rankField != null) _rankField.DeactivateInputField();
                    _stake.SetText("10");
                }
                _rank = _panel._rankSelection.Selected(_index);
                _shownId = market == null ? null : market.Id;
                _shownUid = market == null || _index < 0 || _index >= 8 ? null : market.Players[_index].Uid;
                string rankText = _rank == 0 ? "" : PredictionMod.N(_rank);
                if (changed) _rankInput.SetText(rankText); else _rankInput.Show(rankText);
                _version = market == null ? 0 : market.TradeVersion;
                _half.SetInteractable(false); _sell.SetInteractable(false); _switch.SetInteractable(false);
                if (market == null || _index < 0 || _index >= 8)
                {
                    MatchSnapshot snapshot = _panel._mod.Snapshot;
                    bool known = _index >= 0 && _index < 8 && !_panel._history && snapshot != null && snapshot.Players != null && snapshot.Players.Length == 8;
                    _name.text = known ? snapshot.Players[_index].Name : (_single ? "等待观战对象" : "等待玩家");
                    _elo.text = known ? "待开盘" : "—"; _odds.text = "—";
                    _result.text = _panel._mod.PreparationError == null ? "等待开盘" : "开盘失败，见上方提示"; _preview.text = "";
                    _prev.SetInteractable(false); _next.SetInteractable(false); _buy.SetInteractable(false);
                    if (_rankField != null) _rankField.interactable = false;
                    return;
                }
                _name.text = market.Players[_index].Name;
                Rating rating = market.Ratings[_index];
                _elo.text = rating.Found ? rating.Score.ToString("0", CultureInfo.InvariantCulture) : "3000*";
                if (_single) _elo.text = "Elo " + _elo.text;
                if (market.IsTrading) RefreshTrading(market); else RefreshLegacy(market);
                if (_rankField != null) _rankField.interactable = CanEditRank();
            }
            void RefreshTrading(Market market)
            {
                Position p = market.Positions[_index];
                if (!_single)
                {
                    if (market.KnownRanks[_index] > 0) _name.text += "\n已确定第" + PredictionMod.N(market.KnownRanks[_index]) + "名";
                    else if (market.Life[_index].Points.Count > 0) _name.text += "\n命元 " + PredictionMod.N(market.Life[_index].Points[market.Life[_index].Points.Count - 1].Life);
                    else if (market.QuoteStates != null) _name.text += "\n命元 " + PredictionMod.N(market.QuoteStates[_index].Life);
                }
                bool current = !_panel._history && _panel._mod.Snapshot != null && _panel._mod.Snapshot.Id == market.Id
                    && _panel._mod.Snapshot.LiveObserver && _panel._mod.PreparationError == null;
                if (_single && !_panel.SingleReady) current = false;
                bool active = current && market.TradingWindowOpen && !market.Locked && !p.Resolved;
                _prev.SetInteractable(current && !market.Settled && !p.Resolved);
                _next.SetInteractable(current && !market.Settled && !p.Resolved);
                bool canBuy = TradingOdds.CanBuy(market, _index, _rank);
                _buy.SetText(p.Claim > 0 ? "增持" : "买入");
                _buy.SetInteractable(active && _panel._mod.Enabled && canBuy && (p.Claim == 0 || p.Rank == _rank));
                _half.SetInteractable(active && p.Claim >= 2); _sell.SetInteractable(active && p.Claim > 0);
                _switch.SetInteractable(active && _panel._mod.Enabled && canBuy && p.Claim > 0 && p.Rank != _rank);
                int rate = _rank > 0 ? market.QuoteRates[_index * 8 + _rank - 1] : 0;
                _odds.text = _rank == 0 ? "—" : (canBuy ? PredictionMod.Rate(rate) : "不可买入");
                long stake;
                if (canBuy && long.TryParse(_stake.Text, NumberStyles.None, CultureInfo.InvariantCulture, out stake) && stake >= 1 && stake <= Odds.MaximumStake)
                {
                    long claim = stake * rate / 10000;
                    _preview.text = "本次买入" + PredictionMod.N(stake) + " → 正中返" + PredictionMod.N(claim) + " / 差1返" + PredictionMod.N(claim / 4)
                        + (p.Claim > 0 && p.Rank != _rank ? "（换预测按新名次买入）" : "");
                }
                else _preview.text = _rank == 0 ? "请选择目标名次（1～8）" : "选择仍可能达到的名次，输入1～10000整数积分";
                if (p.Resolved)
                {
                    _result.text = "最终第" + PredictionMod.N(market.KnownRanks[_index]) + "名 · 结算返" + PredictionMod.N(p.FinalPayout)
                        + "\n累计投" + PredictionMod.N(p.Invested) + " 出售收回" + PredictionMod.N(p.CashReturned)
                        + "\n本玩家净收益 " + PredictionMod.N(p.NetResult);
                    _preview.text = "名次已确定，停止交易；结算已入余额";
                }
                else if (p.Claim > 0)
                {
                    long sale = TradingOdds.Sale(market, _index, false);
                    _result.text = "持第" + PredictionMod.N(p.Rank) + "名 成本" + PredictionMod.N(p.Cost) + " 正中返" + PredictionMod.N(p.Claim)
                        + "\n差1返" + PredictionMod.N(p.Claim / 4) + " 清仓值" + PredictionMod.N(sale)
                        + "\n减半值" + PredictionMod.N(TradingOdds.Sale(market, _index, true)) + " 浮盈亏" + PredictionMod.N(sale - p.Cost) + (active ? "" : "（参考）");
                }
                else _result.text = "未持仓 / 已清仓\n累计投" + PredictionMod.N(p.Invested) + " 收回" + PredictionMod.N(p.CashReturned)
                    + "\n已实现收益 " + PredictionMod.N(p.NetResult);
            }
            void RefreshLegacy(Market market)
            {
                Ticket ticket = market.Tickets[_index];
                bool active = !_panel._history && _panel._mod.Enabled && !market.Locked && !market.Settled
                    && _panel._mod.PreparationError == null && _panel._mod.Snapshot != null && _panel._mod.Snapshot.LiveObserver;
                if (_single && !_panel.SingleReady) active = false;
                _prev.SetInteractable(active && ticket == null); _next.SetInteractable(active && ticket == null);
                _buy.SetInteractable(active && ticket == null && _rank > 0); _buy.SetText(ticket == null ? "下注" : "已投");
                int rank = ticket == null ? _rank : ticket.Rank;
                int rate = ticket == null ? (rank == 0 ? 0 : Odds.EffectiveRate(market.BaseRates[_index * 8 + rank - 1], Math.Max(1, Math.Min(5, market.HighestRound)))) : ticket.RateBps;
                _odds.text = rank == 0 ? "—" : PredictionMod.Rate(rate);
                _preview.text = "旧盘口固定投注，不提供调仓";
                if (ticket == null) _result.text = market.Settled ? "未投注 · 最终第" + PredictionMod.N(market.FinalRanks[_index]) + "名" : "未投注";
                else if (market.Settled) _result.text = "本金" + PredictionMod.N(ticket.Stake) + " · 最终第" + PredictionMod.N(market.FinalRanks[_index]) + "名\n返还 " + PredictionMod.N(ticket.ReturnFor(market.FinalRanks[_index], market.RuleVersion));
                else _result.text = "本金" + PredictionMod.N(ticket.Stake) + " 正中返" + PredictionMod.N(ticket.Payout) + "\n"
                    + (market.HasAdjacentReward ? "差1返" + PredictionMod.N(ticket.AdjacentPayout) : "仅正中返还");
            }
        }
    }
}
#endif
