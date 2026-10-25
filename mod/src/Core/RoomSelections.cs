using System.Collections.Generic;

namespace YxPrediction
{
    // 两个原生界面共用UID草稿；报价刷新保留选择，真实持仓变化覆盖选择。
    public sealed class RoomSelections
    {
        sealed class Draft { public int Selected, Purchased; }
        readonly Dictionary<string, Draft> _drafts = new Dictionary<string, Draft>();
        string _room, _game;
        public void Observe(RoomState state)
        {
            if (state == null) { _drafts.Clear(); _room = _game = null; return; }
            if (_room != state.Code || _game != state.GameId) { _drafts.Clear(); _room = state.Code; _game = state.GameId; }
            for (int i = 0; i < 8; i++)
            {
                string uid = state.Uids[i]; if (string.IsNullOrEmpty(uid)) continue;
                Draft d; if (!_drafts.TryGetValue(uid, out d)) { d = new Draft(); _drafts[uid] = d; }
                int purchased = state.Stakes[i] > 0 ? state.MyRanks[i] : 0;
                if (purchased != d.Purchased) d.Selected = purchased;
                d.Purchased = purchased;
            }
        }
        public int Selected(string uid) { Draft d; return uid != null && _drafts.TryGetValue(uid, out d) ? d.Selected : 0; }
        public void Step(string uid, bool forward)
        {
            Draft d; if (uid == null || !_drafts.TryGetValue(uid, out d)) return;
            int rank = d.Selected;
            d.Selected = rank == 0 ? (forward ? 1 : 8) : (forward ? rank % 8 + 1 : (rank + 6) % 8 + 1);
        }
        public static int[] DisplayOrder(RoomState state)
        {
            var result = new int[8]; int at = 0;
            for (int group = 0; group < 2; group++) for (int i = 0; i < 8; i++)
                if ((state.Stakes[i] > 0 ? 0 : 1) == group) result[at++] = i;
            return result;
        }
    }
}
