#if YX_GAME
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Yx.ModSdk;
using Yx.Shared;

namespace YxPrediction
{
    // UnityWebRequest completion is polled on the Unity main thread. No async or CLR callback queues.
    public sealed class RoomClient
    {
        readonly PredictionMod _mod;
        readonly ModContext _ctx;
        readonly ConfigEntry<string> _url, _name;
        UnityWebRequest _request;
        Dictionary<string, object> _sent, _pending;
        string _endpoint, _token, _code, _afterRegister;
        float _next;
        RoomMatch _match;
        string _membersRoom;
        readonly List<string> _seenMembers = new List<string>();
        public string JoinNotice { get; private set; }
        float _noticeUntil;
        string _createNativeId, _lookupCode;
        float _createDeadline;
        bool _joiningMatch;
        public bool Matching { get { return _match != null || _createNativeId != null; } }
        public RoomState State { get; private set; }
        public string Message { get; private set; }
        public bool Busy { get { return _request != null || _pending != null || _match != null || _createNativeId != null; } }
        public RoomClient(PredictionMod mod, ModContext ctx)
        {
            _mod = mod; _ctx = ctx;
            _url = ctx.Config.Bind("online", "server", "https://auth.kaigua.vip/prediction", "独立娱乐积分房间服务地址；外网使用HTTPS");
            _name = ctx.Config.Bind("online", "name", "观众", "房间昵称（首次注册时使用，1至24字）");
            ReadIdentity();
            Message = "选择加入、创建或匹配房间";
        }
        public void SetMessage(string message) { Message = message; }
        void ReadIdentity()
        {
            _endpoint = _url.Value.Trim().TrimEnd('/');
            var saved = _ctx.Data.Get<Dictionary<string, object>>("room.identity.v1", null);
            if (saved != null && Json.GetString(saved, "server", "") == _endpoint)
            {
                _token = Json.GetString(saved, "token", ""); _code = Json.GetString(saved, "code", "");
                _pending = saved.ContainsKey("pending") ? saved["pending"] as Dictionary<string, object> : null;
            }
        }
        void SaveIdentity()
        {
            var saved = new Dictionary<string, object>(); saved["server"] = _endpoint; saved["token"] = _token ?? ""; saved["code"] = _code ?? "";
            if (_pending != null) saved["pending"] = _pending;
            _ctx.Data.Set("room.identity.v1", saved);
        }
        public static Dictionary<string, object> Snapshot(MatchSnapshot s)
        {
            var d = new Dictionary<string, object>(); d["id"] = s.Id; d["round"] = s.Round;
            d["gameRoomId"] = NativeRoomSpectate.CurrentRoomId();
            d["closedThrough"] = s.ResultsThroughRound; d["open"] = s.TradingOpen && s.LiveObserver; d["ended"] = s.Ended;
            var players = new List<object>(); var ranks = new List<object>();
            for (int i = 0; i < 8; i++)
            {
                var p = new Dictionary<string, object>(); p["uid"] = s.Players[i].Uid; p["name"] = s.Players[i].Name; players.Add(p);
                int rank = s.Ended ? s.Ranks[i] : s.States != null && s.States[i] != null && s.States[i].Life <= 0 ? s.States[i].FinalRank : 0;
                ranks.Add(rank >= 1 && rank <= 8 ? rank : 0);
            }
            d["players"] = players; d["ranks"] = ranks; return d;
        }
        Dictionary<string, object> Body(string action) { var d = new Dictionary<string, object>(); d["action"] = action; if (!string.IsNullOrEmpty(_code)) d["code"] = _code; return d; }
        public void Connect(string code, bool create)
        {
            if (Busy) { Message = "等待上一操作确认"; return; }
            if (NativeRoomSpectate.IsLobby)
            {
                if (create)
                {
                    string target, error;
                    if (!LobbyRoomEntry.SpectateSelected(out target, out error)) { Message = error; return; }
                    _createNativeId = target; _createDeadline = Time.unscaledTime + 90f;
                    Message = "请确认原生观战提示；进入所选对局后创建房间，不会自动投入积分";
                }
                else
                {
                    _lookupCode = code.Trim(); _afterRegister = "lookup";
                    if (string.IsNullOrEmpty(_token)) { var register = Body("register"); register["name"] = _name.Value; Send(register); }
                    else ConnectReady();
                }
                return;
            }
            MatchSnapshot s = _mod.Snapshot;
            if (!PredictionVisibility.CanShow() || s == null || !s.IsEightPlayerMatch()) { Message = "请先进入完整八人实时观战"; return; }
            State = null;
            _afterRegister = create ? "create" : "join"; _code = create ? null : code.Trim();
            if (string.IsNullOrEmpty(_token)) { var register = Body("register"); register["name"] = _name.Value; Send(register); }
            else ConnectReady();
        }
        void ConnectReady()
        {
            if (_afterRegister == "lookup") { _afterRegister = null; var lookup = Body("lookup"); lookup["code"] = _lookupCode; Send(lookup); return; }
            if (_afterRegister == "random") { _afterRegister = null; var random = Body("random"); random["exclude"] = _code ?? ""; Send(random); return; }
            MatchSnapshot s = _mod.Snapshot;
            if (s == null || !s.LiveObserver) { Message = "观战已离开，请重新连接"; return; }
            var body = Body(_afterRegister); body["gameId"] = s.Id;
            if (_afterRegister == "create") body["snapshot"] = Snapshot(s);
            _afterRegister = null; Send(body);
        }
        public void RandomJoin()
        {
            if (Busy) { Message = "等待上一操作完成，或取消当前匹配"; return; }
            if (!NativeRoomSpectate.IsLobby) { Message = "请先通过游戏返回大厅，再随机加入其他对局"; return; }
            if (State != null && State.Host && !State.Settled) { Message = "你仍是未结束房间的房主，请先完成对局或明确离开原房间"; return; }
            _afterRegister = "random";
            if (string.IsNullOrEmpty(_token)) { var register = Body("register"); register["name"] = _name.Value; Send(register); }
            else ConnectReady();
        }
        public void CancelMatch()
        {
            if (_match == null && _createNativeId == null) return;
            _createNativeId = null; _match = null;
            Message = "已取消匹配，不加入预测房间；原生观战确认框请按游戏自己的取消按钮关闭";
        }
        public void Leave()
        {
            if (Busy) { Message = "先等待在途操作确认后再离开"; return; }
            _code = null; State = null; SaveIdentity(); Message = "已离开；已投入的积分仍留在原房间等待结算，可用原房间号重连";
        }
        public void Trade(string kind, int player, int rank, long stake)
        {
            MatchSnapshot s = _mod.Snapshot;
            if (Busy || State == null || !State.Open || !PredictionVisibility.CanShow() || s == null || s.Id != State.GameId || !s.TradingOpen || s.Ended || s.Round != State.Round || s.ResultsThroughRound >= s.Round) { Message = "当前不可交易，等待房间与观战状态同步"; return; }
            var d = Body("trade"); d["kind"] = kind; d["player"] = player; d["rank"] = rank; d["stake"] = stake;
            d["version"] = State.Version; d["operation"] = Guid.NewGuid().ToString("N");
            _pending = d; SaveIdentity(); Send(d);
        }
        void Send(Dictionary<string, object> body)
        {
            if (!string.IsNullOrEmpty(_token) && !string.IsNullOrEmpty(GameClientUtil.uid))
            {
                var identity = new Dictionary<string, object>();
                string name = GameDataManager.username.Value;
                identity["name"] = string.IsNullOrEmpty(name) ? _name.Value : name; identity["uid"] = GameClientUtil.uid;
                body["identity"] = identity;
            }
            Uri uri;
            if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            { Message = "服务器须使用HTTPS（本机测试允许HTTP）"; return; }
            try
            {
                _sent = body;
                _request = new UnityWebRequest();
                _request.url = _endpoint + "/v1/room";
                _request.method = "POST";
                _request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(Json.Serialize(body)));
                _request.downloadHandler = new DownloadHandlerBuffer(); _request.timeout = 8;
                _request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(_token)) _request.SetRequestHeader("Authorization", "Bearer " + _token);
                _request.SendWebRequest();
            }
            catch { DestroyRequest(); Message = "请求未完成，等待重连确认"; _next = Time.unscaledTime + 3f; }
        }
        void ObserveMembers(RoomState state)
        {
            bool first = _membersRoom != state.Code;
            if (first) { _seenMembers.Clear(); _membersRoom = state.Code; }
            string notice = "";
            for(int i=0;i<state.MemberIds.Length;i++)
            {
                if (_seenMembers.Contains(state.MemberIds[i])) continue;
                _seenMembers.Add(state.MemberIds[i]);
                string name = state.MemberNames[i] + "（ID " + (string.IsNullOrEmpty(state.MemberUids[i]) ? state.MemberIds[i] : state.MemberUids[i]) + "）";
                if (!first) notice += (notice.Length > 0 ? "、" : "") + name;
            }
            if (notice.Length > 0)
            {
                JoinNotice = notice + " 加入房间"; _noticeUntil = Time.unscaledTime + 8f;
                _ctx.Log.Info(JoinNotice);
                HintMessagePanel.Show(JoinNotice);
            }
        }
        public void Update()
        {
            if (JoinNotice != null && Time.unscaledTime > _noticeUntil) JoinNotice = null;
            if (_endpoint != _url.Value.Trim().TrimEnd('/'))
            {
                // Never send a saved credential to a different server.
                if (Busy) { Message = "服务器地址已变更；先恢复旧地址完成在途操作"; return; }
                DestroyRequest(); _token = null; _code = null; State = null; ReadIdentity();
            }
            if (_createNativeId != null)
            {
                MatchSnapshot ready = _mod.Snapshot;
                if (Time.unscaledTime >= _createDeadline) { _createNativeId = null; Message = "未进入所选观战，创建已取消"; }
                else if (ready != null && ready.LiveObserver && !ready.Ended && ready.IsEightPlayerMatch() && NativeRoomSpectate.CurrentRoomId() == _createNativeId)
                { _createNativeId = null; Connect("", true); return; }
                else return;
            }
            if (_match != null)
            {
                if (_match.Expired(Time.unscaledTime)) { _match = null; Message = "未在90秒内进入目标实时观战，匹配已取消；没有扣除积分"; }
                else if (_match.CanJoin(_mod.Snapshot, Time.unscaledTime))
                {
                    var join = Body("join"); join["code"] = _match.Code; join["gameId"] = _match.GameId;
                    _match = null; _joiningMatch = true; Send(join);
                }
                else return;
            }
            if (_request != null)
            {
                if (!_request.isDone) return;
                bool connect = false;
                RoomMatch selected = null;
                try
                {
                    if (_request.isNetworkError || _request.responseCode >= 500) throw new InvalidOperationException("网络未确认");
                    string text = _request.downloadHandler.text;
                    if (text == null || text.Length > 262144) throw new FormatException("响应过大");
                    var root = Json.ParseObject(text);
                    if (!Json.GetBool(root, "ok", false))
                    {
                        _joiningMatch = false;
                        Message = Json.GetString(root, "error", "请求失败");
                        if (_pending != null) { _pending = null; SaveIdentity(); }
                    }
                    else
                    {
                        var data = root["data"] as Dictionary<string, object>;
                        if (Json.GetString(_sent, "action", "") == "register")
                        { _token = Json.GetString(data, "token", ""); SaveIdentity(); connect = true; }
                        else if ((Json.GetString(_sent, "action", "") == "random" || Json.GetString(_sent, "action", "") == "lookup")) selected = new RoomMatch(data, Time.unscaledTime);
                        else
                        {
                            State = RoomState.Read(data); _code = State.Code; ObserveMembers(State);
                            if ((_joiningMatch && Json.GetString(_sent, "action", "") == "join") || Json.GetString(_sent, "action", "") == "create") { _joiningMatch = false; _mod.OnlinePanel.Open(); }
                            if (Json.GetString(_sent, "action", "") == "trade") _pending = null;
                            SaveIdentity(); Message = State.Settled ? "整局已结算，返还 " + PredictionMod.N(State.Payout) + " 分" : "资金池参考赔率随投入变化；最终赔率以封盘资金池为准";
                        }
                    }
                }
                catch { Message = _pending != null ? "网络未确认：暂停新操作并用同一编号重试，避免重复扣分" : "房间连接失败，稍后自动重连"; }
                DestroyRequest(); _next = Time.unscaledTime + 1.5f;
                if (selected != null)
                {
                    string error;
                    if (NativeRoomSpectate.Request(selected.NativeRoomId, out error))
                    {
                        _match = selected;
                        Message = "已匹配房间 " + selected.Code + "，请确认游戏原生观战提示；进入相同实时对局后自动加入，不会自动投入积分";
                    }
                    else Message = error;
                }
                if (connect) ConnectReady(); return;
            }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1.5f;
            if (_pending != null) { Send(_pending); return; }
            if (string.IsNullOrEmpty(_code) || string.IsNullOrEmpty(_token)) return;
            MatchSnapshot s = _mod.Snapshot;
            if (State != null && State.Host && s != null && s.Id == State.GameId && (s.LiveObserver || s.Ended))
            { var body = Body("sync"); body["snapshot"] = Snapshot(s); Send(body); }
            else Send(Body("view"));
        }
        void DestroyRequest() { if (_request != null) { _request.Abort(); _request.Dispose(); _request = null; } }
        public void Destroy() { DestroyRequest(); }
    }
}
#endif
