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
    public sealed class RoomPanel
    {
        readonly PredictionMod _mod;
        readonly ModContext _ctx;
        readonly NativeTheme _theme;
        readonly UiKit _ui;
        GameObject _root, _menu, _joinForm, _menuChoices;
        TextMeshProUGUI _menuMessage;
        NativeButton _menuMatch, _menuCreate, _menuJoin, _tableCreate, _tableJoin;
        float _nextDiscovery;
        readonly LobbyRoomEntry _entry;
        UiInput _code;
        TextMeshProUGUI _status, _message;
        readonly Row[] _rows = new Row[8];
        bool _visible, _connectionMenu;
        string _feedback;
        float _next;
        public RoomPanel(PredictionMod mod, ModContext ctx, NativeTheme theme)
        { _mod = mod; _ctx = ctx; _theme = theme; _ui = new UiKit(ctx); ctx.Input.RegisterHotkey("onlineRoom", "Ctrl+Shift+P", Toggle); _entry = new LobbyRoomEntry(ctx, theme, Toggle); }
        public void Toggle()
        {
            if (_visible) { Close(); return; }
            if (!PredictionVisibility.CanShow() && !NativeRoomSpectate.IsLobby) return;
            _connectionMenu = false; _visible = true; ShowPresentation(); BackChoices(); if (NativeRoomSpectate.IsLobby) _mod.Online.DiscoverSelected();
        }
        public void Open() { _connectionMenu = false; _visible = true; ShowPresentation(); }
        void Close()
        {
            _visible = false;
            if (_root != null) _root.SetActive(false);
            if (_menu != null) _menu.SetActive(false);
        }
        bool HasCurrentRoom()
        {
            return _mod.Online.State != null && _mod.Snapshot != null && _mod.Online.State.GameId == _mod.Snapshot.Id && PredictionVisibility.CanShow();
        }
        void ShowPresentation()
        {
            bool connected = PredictionVisibility.CanShow() && !_connectionMenu;
            if (connected && _root == null) Build();
            if (!connected && _menu == null) BuildMenu();
            GameObject shown = connected ? _root : _menu;
            bool newlyOpened = shown != null && !shown.activeSelf;
            if (_root != null) _root.SetActive(_visible && connected);
            if (_menu != null) _menu.SetActive(_visible && !connected);
            if (shown != null && _visible && newlyOpened) shown.transform.SetAsLastSibling();
        }
        void BuildMenu()
        {
            _menu = _ui.Panel("PredictionRoomMenu", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 230f), NativeTheme.LacquerBlack);
            if (_menu == null) return;
            _theme.ApplyWindowFrame(_menu.GetComponent<Image>());
            _theme.DecorateWindow(_menu, "名次预测");
            var title = _ui.Label(_menu.transform, "title", "名次预测", new Vector2(24f, -14f), new Vector2(400f, 36f), 24);
            _theme.ApplyTextStyle(title, 24, true, TextAlignmentOptions.Left);
            _theme.CreateButtonTopLeft(_menu.transform, "closeMenu", "×", 518f, -12f, 42f, 38f, NativeBtnStyle.Close, Close, 24);
            _menuChoices = new GameObject("Choices", typeof(RectTransform));
            _menuChoices.transform.SetParent(_menu.transform, false);
            NativeTheme.SetTopLeft(_menuChoices.transform as RectTransform, 0f, -66f, 580f, 70f);
            _menuJoin = _theme.CreateButtonTopLeft(_menuChoices.transform, "menuJoin", "加入", 24f, 0f, 164f, 60f, NativeBtnStyle.Primary, JoinCurrent, 24);
            _menuCreate = _theme.CreateButtonTopLeft(_menuChoices.transform, "menuCreate", "创建", 208f, 0f, 164f, 60f, NativeBtnStyle.Primary, Create, 24);
            _menuMatch = _theme.CreateButtonTopLeft(_menuChoices.transform, "menuMatch", "匹配", 392f, 0f, 164f, 60f, NativeBtnStyle.Primary, RandomJoin, 24);
            _joinForm = new GameObject("JoinForm", typeof(RectTransform));
            _joinForm.transform.SetParent(_menu.transform, false);
            NativeTheme.SetTopLeft(_joinForm.transform as RectTransform, 0f, -66f, 580f, 74f);
            _code = _ui.NumberInput(_joinForm.transform, "roomCode", new Vector2(24f, 0f), new Vector2(204f, 54f), 24, Ignore);
            _theme.CreateButtonTopLeft(_joinForm.transform, "joinCode", "加入房间", 248f, 0f, 164f, 54f, NativeBtnStyle.Primary, Join, 22);
            _theme.CreateButtonTopLeft(_joinForm.transform, "backChoices", "返回", 432f, 0f, 124f, 54f, NativeBtnStyle.Secondary, BackChoices, 22);
            _joinForm.SetActive(false);
            _menuMessage = _ui.Label(_menu.transform, "menuMessage", "", new Vector2(24f, -150f), new Vector2(532f, 62f), 18);
            _theme.ApplyTextStyle(_menuMessage, 18, false, TextAlignmentOptions.Left); _menuMessage.enableWordWrapping = true;
        }
        void ShowConnectionMenu() { if (_menu == null) BuildMenu(); _connectionMenu = true; if (_root != null) _root.SetActive(false); ShowJoin(); if (_menu != null) _menu.SetActive(true); }
        void JoinCurrent() { if (NativeRoomSpectate.IsLobby) _mod.Online.JoinSelected(); else ShowJoin(); }
        void ShowJoin() { if (_joinForm != null) _joinForm.SetActive(true); if (_menuChoices != null) _menuChoices.SetActive(false); }
        void BackChoices() { _connectionMenu = false; if (_joinForm != null) _joinForm.SetActive(false); if (_menuChoices != null) _menuChoices.SetActive(true); }
        void Build()
        {
            _root = _ui.Panel("PredictionOnlineRoom", new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(1140f,650f),NativeTheme.LacquerBlack);
            if (_root == null) return;
            _theme.ApplyWindowFrame(_root.GetComponent<Image>());
            _theme.DecorateWindow(_root,"名次预测");
            var title=Label("title","名次预测总览",20,-8,230,34,22); title.color=NativeTheme.AntiqueGold;
            _status=Label("status","",254,-8,560,34,16);
            _tableCreate=_theme.CreateButtonTopLeft(_root.transform,"create","创建",820,-8,72,32,NativeBtnStyle.Secondary,Create,16);
            _tableJoin=_theme.CreateButtonTopLeft(_root.transform,"join","加入",898,-8,72,32,NativeBtnStyle.Secondary,ShowConnectionMenu,16);
            _theme.CreateButtonTopLeft(_root.transform,"leave","离开",976,-8,72,32,NativeBtnStyle.Secondary,Leave,16);
            _theme.CreateButtonTopLeft(_root.transform,"close","×",1060,-8,54,32,NativeBtnStyle.Close,Close,22);
            Label("headerPlayer","玩家",20,-55,160,30,18);
            Label("headerHorse","田忌马牌",186,-55,145,30,18);
            Label("headerRank","猜第几名",336,-55,120,30,18);
            Label("headerRate","倍率",464,-55,76,30,18);
            Label("headerStake","投入",548,-55,68,30,18);
            Label("headerActions","操作",624,-55,192,30,18);
            Label("headerHolding","我的预测与返还",830,-55,270,30,18);
            for(int i=0;i<8;i++) _rows[i]=new Row(this,i);
            _message=Label("message","",20,-586,1090,48,17);_message.enableWordWrapping=true;
        }
        TextMeshProUGUI Label(string name, string value, float x, float y, float w, float h, int size)
        {
            var label = _ui.Label(_root.transform, name, value, new Vector2(x,y), new Vector2(w,h), size);
            _theme.ApplyTextStyle(label,size,false,TextAlignmentOptions.Left); if (name.StartsWith("header")) label.color=NativeTheme.AntiqueGold; label.text = value; label.raycastTarget = false; return label;
        }
        static void Ignore(string value) { }
        void Create() { _mod.Online.Connect("", true); }
        void Join() { _mod.Online.Connect(_code.Text, false); }
        void RandomJoin() { if (_mod.Online.Matching) _mod.Online.CancelMatch(); else _mod.Online.RandomJoin(); }
        void CancelMatch() { _mod.Online.CancelMatch(); }
        void Leave() { _mod.Online.Leave(); }
        public void Update()
        {
            bool lobby = NativeRoomSpectate.IsLobby;
            _entry.Update();
            if ((!PredictionVisibility.CanShow() && !lobby) || (!lobby && GameBridge.IsPlayingBattle()) || (lobby && !_entry.Visible)) { Close(); return; }
            if (!_visible) return;
            ShowPresentation();
            bool connected = PredictionVisibility.CanShow() && !_connectionMenu;
            GameObject shown = connected ? _root : _menu;
            if (shown == null) return;
            var rect = shown.transform as RectTransform; var parent = rect.parent as RectTransform;
            float scale = Mathf.Max(0.1f, Mathf.Min(1.2f, Mathf.Min((parent.rect.width - 32f) / rect.rect.width, (parent.rect.height - 180f) / rect.rect.height)));
            rect.localScale = new Vector3(scale,scale,1);
            if (!connected)
            {
                if (lobby && Time.unscaledTime >= _nextDiscovery && !_mod.Online.Busy) { _nextDiscovery=Time.unscaledTime+3f; _mod.Online.DiscoverSelected(); }
                string native; LobbyRoomEntry.SelectedButton(out native);
                bool known = !lobby || (_mod.Online.DiscoveryReady && _mod.Online.DiscoveredNative == native);
                bool exists = lobby && known && !string.IsNullOrEmpty(_mod.Online.DiscoveredCode);
                if (_menuCreate != null) { _menuCreate.SetActive(!exists); _menuCreate.SetInteractable(known && !_mod.Online.Busy); }
                if (_menuJoin != null) _menuJoin.SetInteractable(known && (!lobby || (exists && _mod.Online.DiscoveredAvailable)) && !_mod.Online.Busy);
                if (_menuMessage != null) _menuMessage.text = !known ? "查询所选对局…" : exists ? "本局已有预测房间，点击加入即可观战" : _mod.Online.Message;
                if (_menuMatch != null) _menuMatch.SetText(_mod.Online.Matching ? "取消" : "匹配");
                return;
            }
            if (Time.unscaledTime < _next) return; _next = Time.unscaledTime + 0.2f;
            if (_tableCreate != null) _tableCreate.SetActive(!HasCurrentRoom());
            if (_tableJoin != null) _tableJoin.SetActive(!HasCurrentRoom());
            RoomState r = CurrentView();
            _status.text = r == null ? "等待完整八人观战状态"
                : "房间 " + r.Code + " · " + PredictionMod.N(r.Members) + " 人 · 第 " + PredictionMod.N(r.Round) + " 轮 · "
                + (r.Settled ? "已结算" : r.Open ? "交易开放" : "交易暂停") + " · 余额 " + PredictionMod.N(r.Balance) + " 分" + (r.Host ? " · 你是房主" : "");
            if (HasCurrentRoom())
            {
                string members = "成员：";
                RoomState room = _mod.Online.State;
                for (int i=0;i<room.MemberNames.Length;i++) members += (i>0?"、":"") + room.MemberNames[i] + "（" + (string.IsNullOrEmpty(room.MemberUids[i]) ? room.MemberIds[i] : room.MemberUids[i]) + "）";
                _message.text = _feedback ?? _mod.Online.JoinNotice ?? members;
            }
            else _message.text = _feedback ?? _mod.PreparationError ?? "";
            for (int i = 0; i < 8; i++) _rows[i].Refresh(r);
        }
        public void SetFeedback(string message) { _feedback = message; }
        public RoomState CurrentView()
        {
            if (HasCurrentRoom()) return _mod.Online.State;
            Market m = _mod.Current; if (m == null) return null;
            var r = new RoomState(); r.Code = "单人"; r.GameId = m.Id; r.Round = m.HighestRound;
            r.Open = m.TradingWindowOpen; r.Settled = m.Settled; r.Balance = _mod.Book.Balance; r.Members = 1;
            for (int i = 0; i < 8; i++) { r.Uids[i] = m.Players[i].Uid; r.Names[i] = m.Players[i].Name; r.Ranks[i] = m.KnownRanks[i]; r.MyRanks[i] = m.Positions[i].Rank; r.Stakes[i] = m.Positions[i].Claim; }
            return r;
        }
        public void Submit(int player, int rank, long amount, int action)
        {
            RoomState r = CurrentView(); if (r == null) return; _feedback = null;
            if (HasCurrentRoom())
            {
                string kind = action == 1 ? "half" : action == 2 ? "sell" : r.Stakes[player] > 0 && r.MyRanks[player] != rank ? "switch" : "buy";
                _mod.Online.Trade(kind, player, rank, amount);
            }
            else
            {
                Market m = _mod.Current;
                int kind = action == 0 && m.Positions[player].Claim > 0 && m.Positions[player].Rank != rank ? 3 : action;
                _feedback = _mod.Trade(kind, m.Players[player].Uid, rank, amount, m.TradeVersion, null, m.Id);
            }
        }
        sealed class Row
        {
            readonly RoomPanel _panel;
            readonly int _index;
            readonly TextMeshProUGUI _rank, _playerName, _playerLife, _horseTitle, _oddsLabel, _holdingLabel, _resultLabel;
            readonly GameObject _rowGo;
            readonly RectTransform _rowRect;
            readonly Image _selfFlag, _horseIcon, _dividerLine;
            readonly NativeButton _btnMinus, _btnPlus;
            readonly UiInput _stake;
            readonly NativeButton _buy, _half, _sell;
            int _chosen = 1;
            string _identity;
            public Row(RoomPanel panel, int index)
            {
                _panel = panel;
                _index = index;

                _rowGo = new GameObject("Row_" + PredictionMod.N(index), typeof(RectTransform));
                _rowGo.transform.SetParent(panel._root.transform, false);

                _rowRect = _rowGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(_rowRect, 0f, -88f-index*60f, PanelLayout.WindowWidth - 24f, 54f);

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
                NativeTheme.SetTopLeft(nameRect, 16f, -6f, 160f, 24f);
                _playerName = nameGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_playerName, 16, true, TextAlignmentOptions.Left);
                _playerName.raycastTarget = false;

                var lifeGo = new GameObject("PlayerLife", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                lifeGo.transform.SetParent(row, false);
                var lifeRect = lifeGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(lifeRect, 16f, -30f, 160f, 20f);
                _playerLife = lifeGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_playerLife, 13, false, TextAlignmentOptions.Left);
                _playerLife.color = NativeTheme.TextMuted;
                _playerLife.raycastTarget = false;

                // 2. 田忌称号
                var iconGo = new GameObject("HorseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(row, false);
                var iconRect = iconGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(iconRect, 186f, -10f, 36f, 36f);
                _horseIcon = iconGo.GetComponent<Image>();
                _horseIcon.preserveAspect = true;
                _horseIcon.raycastTarget = false;

                var titleGo = new GameObject("HorseTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(row, false);
                var titleRect = titleGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(titleRect, 226f, -10f, 105f, 36f);
                _horseTitle = titleGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_horseTitle, 15, true, TextAlignmentOptions.Left);
                _horseTitle.raycastTarget = false;

                // 3. 目标名次与倍率
                _btnMinus = panel._theme.CreateButtonTopLeft(row, "Minus", "−", 336f, -11f, 30f, 34f, NativeBtnStyle.Pill, Minus, 18);

                var rankGo = new GameObject("RankLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rankGo.transform.SetParent(row, false);
                var rRect = rankGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(rRect, 370f, -11f, 52f, 34f);
                _rank = rankGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_rank, 18, true, TextAlignmentOptions.Center);
                _rank.color = NativeTheme.AntiqueGold;

                _btnPlus = panel._theme.CreateButtonTopLeft(row, "Plus", "+", 426f, -11f, 30f, 34f, NativeBtnStyle.Pill, Plus, 18);

                var oddsGo = new GameObject("OddsLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                oddsGo.transform.SetParent(row, false);
                var oRect = oddsGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(oRect, 464f, -11f, 76f, 34f);
                _oddsLabel = oddsGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_oddsLabel, 17, true, TextAlignmentOptions.Center);
                _oddsLabel.color = new Color(0.35f, 0.95f, 0.82f, 1f);

                // 4. 积分输入
                _stake = panel._ui.NumberInput(row, "Stake", new Vector2(548f, -11f), new Vector2(68f, 34f), 5, Ignore);
                Transform stakeT = row.Find("Stake");
                if (stakeT != null)
                {
                    var bg = stakeT.GetComponent<Image>();
                    if (bg != null) panel._theme.ApplyInset(bg);
                    panel._theme.StyleInput(stakeT,20);
                }

                _stake.SetText("10");

                // 5. 调仓操作按钮
                _buy = panel._theme.CreateButtonTopLeft(row, "BtnBuy", "下注", 624f, -11f, 68f, 34f, NativeBtnStyle.Primary, Buy, 15);
                _half = panel._theme.CreateButtonTopLeft(row, "BtnHalf", "退半", 698f, -11f, 56f, 34f, NativeBtnStyle.Caution, Half, 14);
                _sell = panel._theme.CreateButtonTopLeft(row, "BtnSell", "清仓", 760f, -11f, 56f, 34f, NativeBtnStyle.Danger, Sell, 14);

                // 6. 权益与结果
                var holdingGo = new GameObject("HoldingLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                holdingGo.transform.SetParent(row, false);
                var hRect = holdingGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(hRect, 830f, -8f, 200f, 24f);
                _holdingLabel = holdingGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_holdingLabel, 15, true, TextAlignmentOptions.Left);
                _holdingLabel.color = NativeTheme.TextParchment;
                _holdingLabel.raycastTarget = false;

                var resultGo = new GameObject("ResultLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                resultGo.transform.SetParent(row, false);
                var resRect = resultGo.GetComponent<RectTransform>();
                NativeTheme.SetTopLeft(resRect, 830f, -31f, 200f, 22f);
                _resultLabel = resultGo.GetComponent<TextMeshProUGUI>();
                panel._theme.ApplyTextStyle(_resultLabel, 13, false, TextAlignmentOptions.Left);
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

            void Minus() { _chosen = _chosen == 1 ? 8 : _chosen - 1; }
            void Plus() { _chosen = _chosen == 8 ? 1 : _chosen + 1; }
            void Buy()
            {
                RoomState r = _panel.CurrentView(); if (r == null || _identity != r.Code + ":" + r.GameId + ":" + r.Uids[_index]) return;
                long amount;
                if (!long.TryParse(_stake.Text, NumberStyles.None, CultureInfo.InvariantCulture, out amount) || amount < 1 || amount > 10000) { _panel._feedback = "投入须为1至10000整数"; return; }
                string kind = r.Stakes[_index] > 0 && r.MyRanks[_index] != _chosen ? "switch" : "buy";
                _panel.Submit(_index,_chosen,amount,0);
            }
            void Half() { _panel.Submit(_index,0,0,1); }
            void Sell() { _panel.Submit(_index,0,0,2); }
            public void Refresh(RoomState r)
            {
                string identity = r == null ? null : r.Code + ":" + r.GameId + ":" + r.Uids[_index];
                if (_identity != identity) { _identity = identity; _chosen = r != null && r.MyRanks[_index] > 0 ? r.MyRanks[_index] : 1; _stake.SetText("10"); }
                _rank.text = "第" + PredictionMod.N(_chosen) + "名";
                bool valid = r != null && _panel._mod.Snapshot != null && r.GameId == _panel._mod.Snapshot.Id;
                bool open = valid && r.Open && !r.Settled && (!_panel.HasCurrentRoom() || !_panel._mod.Online.Busy) && r.Ranks[_index] == 0;
                _buy.SetInteractable(open); _half.SetInteractable(open && r.Stakes[_index] > 0); _sell.SetInteractable(open && r.Stakes[_index] > 0);
                _playerName.text = r == null ? "—" : r.Names[_index];
                if (r != null)
                {
                    MatchSnapshot snapshot=_panel._mod.Snapshot; PlayerState state=null;
                    if(snapshot!=null) for(int i=0;i<snapshot.Players.Length;i++) if(snapshot.Players[i].Uid==r.Uids[_index] && snapshot.States!=null) state=snapshot.States[i];
                    _playerLife.text=state==null?"":"命元 "+PredictionMod.N(state.Life)+" · "+PredictionMod.N(state.Wins)+"胜"+PredictionMod.N(state.Losses)+"负";
                    _selfFlag.gameObject.SetActive(snapshot!=null&&FocusSelection.Uid(snapshot)==r.Uids[_index]);
                    HorseMember horse=_panel._mod.Horses.FindMember(r.Uids[_index]);
                    _horseTitle.text=horse==null?"":horse.Caption;
                    if(horse!=null) _horseTitle.color=TianJiFeature.ColorOf(horse.Title);
                    _horseIcon.sprite=horse==null?null:_panel._mod.Horses.GetSprite(horse.IconTitle);
                    _horseIcon.gameObject.SetActive(_horseIcon.sprite!=null);
                }
                if (r == null) { _holdingLabel.text = "未连接"; return; }
                long total = 0; for (int j = 0; j < 8; j++) total += r.Pools[_index * 8 + j];
                long selected = r.Pools[_index * 8 + _chosen - 1];
                string rate = !_panel.HasCurrentRoom() && _panel._mod.Current != null ? PredictionMod.Rate(_panel._mod.Current.QuoteRates[_index * 8 + _chosen - 1]) : selected > 0 ? (total / (double)selected).ToString("0.00",CultureInfo.InvariantCulture) + "x" : "暂无投入";
                _oddsLabel.text=rate=="暂无投入"?"—":rate;
                _holdingLabel.text=r.Stakes[_index]>0?"猜"+PredictionMod.N(r.MyRanks[_index])+"名 · 权益 "+PredictionMod.N(r.Stakes[_index])+"分":"未持仓";
                _resultLabel.text=r.Ranks[_index]>0?"最终第"+PredictionMod.N(r.Ranks[_index])+"名":"";
                _half.SetActive(r.Stakes[_index]>0);_sell.SetActive(r.Stakes[_index]>0);
                _buy.SetText(r.Stakes[_index] > 0 ? r.MyRanks[_index] == _chosen ? "增持" : "换预测" : "投入");
            }
        }
        public void KeepBarAbove(Transform bar) { if (_visible && _root != null && _root.activeInHierarchy && bar != null && _root.transform.GetSiblingIndex() == bar.parent.childCount - 1) bar.SetAsLastSibling(); }
        public void Destroy() { _entry.Destroy(); _ui.DestroyAll(); _root = null; _menu = null; }
    }
}
#endif
