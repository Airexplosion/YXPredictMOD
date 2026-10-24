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
        GameObject _root, _lobbyLauncher;
        UiInput _code;
        TextMeshProUGUI _status, _message;
        readonly Row[] _rows = new Row[8];
        bool _visible;
        float _next;
        public RoomPanel(PredictionMod mod, ModContext ctx, NativeTheme theme)
        { _mod = mod; _ctx = ctx; _theme = theme; _ui = new UiKit(ctx); ctx.Input.RegisterHotkey("onlineRoom", "Ctrl+Shift+P", Toggle); }
        public void Toggle()
        {
            if (!PredictionVisibility.CanShow() && !NativeRoomSpectate.IsLobby) return;
            if (_root == null) Build();
            _visible = !_visible;
            if (_root != null) { _root.SetActive(_visible); _root.transform.SetAsLastSibling(); }
        }
        public void Open() { if (!_visible) Toggle(); }
        void Close() { _visible = false; if (_root != null) _root.SetActive(false); }
        void Build()
        {
            _root = _ui.Panel("PredictionOnlineRoom", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1140f, 650f), NativeTheme.LacquerBlack);
            if (_root == null) return;
            _theme.ApplyWindowFrame(_root.GetComponent<Image>());
            Label("title", "联机名次预测 · 独立娱乐积分", 20, -12, 690, 34, 24);
            _theme.CreateButtonTopLeft(_root.transform, "close", "关闭", 1030, -12, 90, 34, NativeBtnStyle.Close, Close, 20);
            Label("codeLabel", "六位房间号", 20, -62, 130, 36, 20);
            _code = _ui.NumberInput(_root.transform, "roomCode", new Vector2(154, -62), new Vector2(150, 38), 22, Ignore);
            _theme.CreateButtonTopLeft(_root.transform, "create", "创建房间", 320, -62, 130, 38, NativeBtnStyle.Primary, Create, 20);
            _theme.CreateButtonTopLeft(_root.transform, "join", "加入房间", 464, -62, 130, 38, NativeBtnStyle.Primary, Join, 20);
            _theme.CreateButtonTopLeft(_root.transform, "leave", "离开房间", 608, -62, 130, 38, NativeBtnStyle.Secondary, Leave, 20);
            _theme.CreateButtonTopLeft(_root.transform, "random", "随机加入", 752, -62, 142, 38, NativeBtnStyle.Primary, RandomJoin, 20);
            _theme.CreateButtonTopLeft(_root.transform, "cancelMatch", "取消匹配", 908, -62, 142, 38, NativeBtnStyle.Secondary, CancelMatch, 20);
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
        void RandomJoin() { _mod.Online.RandomJoin(); }
        void CancelMatch() { _mod.Online.CancelMatch(); }
        void Leave() { _mod.Online.Leave(); }
        public void Update()
        {
            bool lobby = NativeRoomSpectate.IsLobby;
            if (_lobbyLauncher != null) _lobbyLauncher.SetActive(lobby && !_visible);
            if (lobby && _lobbyLauncher == null)
            {
                _lobbyLauncher = _ui.Panel("PredictionRoomLauncher", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(174f, 48f), NativeTheme.LacquerBlack);
                if (_lobbyLauncher != null) _theme.CreateButtonTopLeft(_lobbyLauncher.transform, "openRooms", "联机预测", 4f, -4f, 166f, 40f, NativeBtnStyle.Primary, Toggle, 22);
            }
            if ((!PredictionVisibility.CanShow() && !lobby) || (!lobby && GameBridge.IsPlayingBattle())) { Close(); return; }
            if (!_visible || _root == null) return;
            var rect = _root.transform as RectTransform; var parent = rect.parent as RectTransform;
            float scale = Mathf.Max(0.1f, Mathf.Min(1.2f, Mathf.Min((parent.rect.width - 32f) / 1140f, (parent.rect.height - 180f) / 650f)));
            rect.localScale = new Vector3(scale,scale,1);
            if (Time.unscaledTime < _next) return; _next = Time.unscaledTime + 0.2f;
            RoomState r = _mod.Online.State;
            _status.text = r == null ? "未连接 · 大厅可随机加入已有投入的房间；同局观战可创建或输入房号加入"
                : "房间 " + r.Code + " · " + PredictionMod.N(r.Members) + " 人 · 第 " + PredictionMod.N(r.Round) + " 轮 · "
                + (r.Settled ? "已结算" : r.Open ? "交易开放" : "交易暂停") + " · 余额 " + PredictionMod.N(r.Balance) + " 分" + (r.Host ? " · 你是房主" : "");
            _message.text = _mod.Online.Message;
            for (int i = 0; i < 8; i++) _rows[i].Refresh(r);
        }
        sealed class Row
        {
            readonly RoomPanel _panel;
            readonly int _index;
            readonly TextMeshProUGUI _name, _rank, _holding;
            readonly UiInput _stake;
            readonly NativeButton _buy, _half, _sell;
            int _chosen = 1;
            public Row(RoomPanel panel, int index)
            {
                _panel = panel; _index = index; float y = -188f - index * 50f;
                string id = index.ToString(CultureInfo.InvariantCulture);
                _name = panel.Label("name"+id, "—", 20,y,205,40,20);
                panel._theme.CreateButtonTopLeft(panel._root.transform,"minus"+id,"−",230,y,34,38,NativeBtnStyle.Pill,Minus,22);
                _rank = panel.Label("rank"+id,"1名",270,y,58,38,20);
                panel._theme.CreateButtonTopLeft(panel._root.transform,"plus"+id,"+",330,y,34,38,NativeBtnStyle.Pill,Plus,22);
                _stake = panel._ui.NumberInput(panel._root.transform,"stake"+id,new Vector2(382,y),new Vector2(92,38),20,Ignore);_stake.SetText("10");
                _buy = panel._theme.CreateButtonTopLeft(panel._root.transform,"buy"+id,"投入",492,y,86,38,NativeBtnStyle.Primary,Buy,20);
                _half = panel._theme.CreateButtonTopLeft(panel._root.transform,"half"+id,"退半",590,y,74,38,NativeBtnStyle.Caution,Half,20);
                _sell = panel._theme.CreateButtonTopLeft(panel._root.transform,"sell"+id,"清仓",676,y,74,38,NativeBtnStyle.Danger,Sell,20);
                _holding = panel.Label("holding"+id,"",770,y,350,40,18);
            }
            void Minus() { _chosen = _chosen == 1 ? 8 : _chosen - 1; }
            void Plus() { _chosen = _chosen == 8 ? 1 : _chosen + 1; }
            void Buy()
            {
                RoomState r = _panel._mod.Online.State; if (r == null) return;
                long amount;
                if (!long.TryParse(_stake.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount) || amount < 1 || amount > 10000) { _panel._mod.Online.SetMessage("投入须为1至10000整数"); return; }
                string kind = r.Stakes[_index] > 0 && r.MyRanks[_index] != _chosen ? "switch" : "buy";
                _panel._mod.Online.Trade(kind,_index,_chosen,amount);
            }
            void Half() { _panel._mod.Online.Trade("half",_index,0,0); }
            void Sell() { _panel._mod.Online.Trade("sell",_index,0,0); }
            public void Refresh(RoomState r)
            {
                _rank.text = PredictionMod.N(_chosen) + "名";
                bool valid = r != null && _panel._mod.Snapshot != null && r.GameId == _panel._mod.Snapshot.Id;
                bool open = valid && r.Open && !r.Settled && !_panel._mod.Online.Busy && r.Ranks[_index] == 0;
                _buy.SetInteractable(open); _half.SetInteractable(open && r.Stakes[_index] > 0); _sell.SetInteractable(open && r.Stakes[_index] > 0);
                _name.text = r == null ? "—" : r.Names[_index];
                if (r == null) { _holding.text = "未连接"; return; }
                long total = 0; for (int j = 0; j < 8; j++) total += r.Pools[_index * 8 + j];
                long selected = r.Pools[_index * 8 + _chosen - 1];
                string rate = selected > 0 ? (total / (double)selected).ToString("0.00",CultureInfo.InvariantCulture) + "x" : "暂无投入";
                _holding.text = r.Stakes[_index] > 0 ? "猜" + PredictionMod.N(r.MyRanks[_index]) + "名 " + PredictionMod.N(r.Stakes[_index]) + "分 · " + rate : "未持仓 · " + rate;
                _buy.SetText(r.Stakes[_index] > 0 ? r.MyRanks[_index] == _chosen ? "增持" : "换预测" : "投入");
            }
        }
        public void Destroy() { _ui.DestroyAll(); _root = null; }
    }
}
#endif
