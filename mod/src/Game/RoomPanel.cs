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
        NativeButton _menuMatch;
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
            _connectionMenu = false; _visible = true; ShowPresentation(); BackChoices();
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
            var title = _ui.Label(_menu.transform, "title", "联机预测", new Vector2(24f, -14f), new Vector2(400f, 36f), 24);
            _theme.ApplyTextStyle(title, 24, true, TextAlignmentOptions.Left);
            _theme.CreateButtonTopLeft(_menu.transform, "closeMenu", "×", 518f, -12f, 42f, 38f, NativeBtnStyle.Close, Close, 24);
            _menuChoices = new GameObject("Choices", typeof(RectTransform));
            _menuChoices.transform.SetParent(_menu.transform, false);
            NativeTheme.SetTopLeft(_menuChoices.transform as RectTransform, 0f, -66f, 580f, 70f);
            _theme.CreateButtonTopLeft(_menuChoices.transform, "menuJoin", "加入", 24f, 0f, 164f, 60f, NativeBtnStyle.Primary, ShowJoin, 24);
            _theme.CreateButtonTopLeft(_menuChoices.transform, "menuCreate", "创建", 208f, 0f, 164f, 60f, NativeBtnStyle.Primary, Create, 24);
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
        void ShowJoin() { if (_joinForm != null) _joinForm.SetActive(true); if (_menuChoices != null) _menuChoices.SetActive(false); }
        void BackChoices() { _connectionMenu = false; if (_joinForm != null) _joinForm.SetActive(false); if (_menuChoices != null) _menuChoices.SetActive(true); }
        void Build()
        {
            _root = _ui.Panel("PredictionOnlineRoom", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1140f, 650f), NativeTheme.LacquerBlack);
            if (_root == null) return;
            _theme.ApplyWindowFrame(_root.GetComponent<Image>());
            Label("title", "名次预测", 20, -12, 690, 34, 24);
            _theme.CreateButtonTopLeft(_root.transform, "close", "关闭", 1030, -12, 90, 34, NativeBtnStyle.Close, Close, 20);
            _theme.CreateButtonTopLeft(_root.transform, "create", "创建房间", 24, -62, 150, 38, NativeBtnStyle.Primary, Create, 20);
            _theme.CreateButtonTopLeft(_root.transform, "join", "加入房间", 188, -62, 150, 38, NativeBtnStyle.Secondary, ShowConnectionMenu, 20);
            _theme.CreateButtonTopLeft(_root.transform, "leave", "离开房间", 608, -62, 130, 38, NativeBtnStyle.Secondary, Leave, 20);
            _theme.CreateButtonTopLeft(_root.transform, "random", "房间选项", 752, -62, 142, 38, NativeBtnStyle.Primary, ShowConnectionMenu, 20);
            _status = Label("status", "请在管理器 MOD 配置 online.server 与昵称，再创建或加入房间", 20, -112, 1090, 34, 20);
            Label("headers", "选手                         预测名次        投入积分       操作                                    持仓 / 资金池参考赔率", 20, -152, 1090, 30, 18);
            for (int i = 0; i < 8; i++) _rows[i] = new Row(this, i);
            _message = Label("message", "", 20, -594, 1090, 44, 18);
            _message.enableWordWrapping = true;
        }
        TextMeshProUGUI Label(string name, string value, float x, float y, float w, float h, int size)
        {
            var label = _ui.Label(_root.transform, name, value, new Vector2(x,y), new Vector2(w,h), size);
            _theme.ApplyTextStyle(label,size,false,TextAlignmentOptions.Left); label.text = value; label.raycastTarget = false; return label;
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
            if (!connected) { if (_menuMessage != null) _menuMessage.text = _mod.Online.Message; if (_menuMatch != null) _menuMatch.SetText(_mod.Online.Matching ? "取消" : "匹配"); return; }
            if (Time.unscaledTime < _next) return; _next = Time.unscaledTime + 0.2f;
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
            readonly TextMeshProUGUI _name, _rank, _holding;
            readonly UiInput _stake;
            readonly NativeButton _buy, _half, _sell;
            int _chosen = 1;
            string _identity;
            public Row(RoomPanel panel, int index)
            {
                _panel = panel; _index = index; float y = -188f - index * 50f;
                string id = index.ToString(CultureInfo.InvariantCulture);
                _name = panel.Label("name"+id, "—", 20,y,205,40,20);
                panel._theme.CreateButtonTopLeft(panel._root.transform,"minus"+id,"−",230,y,34,38,NativeBtnStyle.Pill,Minus,22);
                _rank = panel.Label("rank"+id,"1名",270,y,58,38,20);
                panel._theme.CreateButtonTopLeft(panel._root.transform,"plus"+id,"+",330,y,34,38,NativeBtnStyle.Pill,Plus,22);
                _stake = panel._ui.NumberInput(panel._root.transform,"stake"+id,new Vector2(382,y),new Vector2(92,38),5,Ignore);_stake.SetText("10");
                panel._theme.StyleInput(panel._root.transform.Find("stake"+id), 22);
                _buy = panel._theme.CreateButtonTopLeft(panel._root.transform,"buy"+id,"投入",492,y,86,38,NativeBtnStyle.Primary,Buy,20);
                _half = panel._theme.CreateButtonTopLeft(panel._root.transform,"half"+id,"退半",590,y,74,38,NativeBtnStyle.Caution,Half,20);
                _sell = panel._theme.CreateButtonTopLeft(panel._root.transform,"sell"+id,"清仓",676,y,74,38,NativeBtnStyle.Danger,Sell,20);
                _holding = panel.Label("holding"+id,"",770,y,350,40,18);
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
                _rank.text = PredictionMod.N(_chosen) + "名";
                bool valid = r != null && _panel._mod.Snapshot != null && r.GameId == _panel._mod.Snapshot.Id;
                bool open = valid && r.Open && !r.Settled && (!_panel.HasCurrentRoom() || !_panel._mod.Online.Busy) && r.Ranks[_index] == 0;
                _buy.SetInteractable(open); _half.SetInteractable(open && r.Stakes[_index] > 0); _sell.SetInteractable(open && r.Stakes[_index] > 0);
                _name.text = r == null ? "—" : r.Names[_index];
                if (r == null) { _holding.text = "未连接"; return; }
                long total = 0; for (int j = 0; j < 8; j++) total += r.Pools[_index * 8 + j];
                long selected = r.Pools[_index * 8 + _chosen - 1];
                string rate = !_panel.HasCurrentRoom() && _panel._mod.Current != null ? PredictionMod.Rate(_panel._mod.Current.QuoteRates[_index * 8 + _chosen - 1]) : selected > 0 ? (total / (double)selected).ToString("0.00",CultureInfo.InvariantCulture) + "x" : "暂无投入";
                _holding.text = r.Stakes[_index] > 0 ? "猜" + PredictionMod.N(r.MyRanks[_index]) + "名 " + PredictionMod.N(r.Stakes[_index]) + "分 · " + rate : "未持仓 · " + rate;
                _buy.SetText(r.Stakes[_index] > 0 ? r.MyRanks[_index] == _chosen ? "增持" : "换预测" : "投入");
            }
        }
        public void KeepBarAbove(Transform bar) { if (_visible && _root != null && _root.activeInHierarchy && bar != null && _root.transform.GetSiblingIndex() == bar.parent.childCount - 1) bar.SetAsLastSibling(); }
        public void Destroy() { _entry.Destroy(); _ui.DestroyAll(); _root = null; _menu = null; }
    }
}
#endif
