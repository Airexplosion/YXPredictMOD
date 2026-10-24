using System;
using System.Collections.Generic;
using System.Globalization;
using Yx.Shared;

namespace YxPrediction
{
    public sealed class RoomState
    {
        public string Code, GameId;
        public string[] MemberIds = new string[0], MemberNames = new string[0], MemberUids = new string[0];
        public bool Host, Open, Settled;
        public int Version, Round, Members;
        public long Balance, Payout;
        public readonly string[] Uids = new string[8], Names = new string[8];
        public readonly int[] Ranks = new int[8], MyRanks = new int[8];
        public readonly long[] Stakes = new long[8], Pools = new long[64];
        public static RoomState Read(Dictionary<string, object> d)
        {
            var r = new RoomState();
            r.Code = Json.GetString(d, "code", ""); r.GameId = Json.GetString(d, "gameId", "");
            r.Host = Json.GetBool(d, "host", false); r.Open = Json.GetBool(d, "open", false); r.Settled = Json.GetBool(d, "settled", false);
            r.Version = (int)Json.GetNumber(d, "version", 0); r.Round = (int)Json.GetNumber(d, "round", 0); r.Members = (int)Json.GetNumber(d, "members", 0);
            r.Balance = (long)Json.GetNumber(d, "balance", 0); r.Payout = (long)Json.GetNumber(d, "payout", 0);
            var members = Json.GetArray(d, "memberDetails");
            if (members != null)
            {
                if (members.Count > 32) throw new FormatException("房间成员数量无效");
                r.MemberIds = new string[members.Count]; r.MemberNames = new string[members.Count]; r.MemberUids = new string[members.Count];
                for(int i=0;i<members.Count;i++) { var member=members[i] as Dictionary<string,object>; r.MemberIds[i]=Json.GetString(member,"id",""); r.MemberNames[i]=Json.GetString(member,"name",""); r.MemberUids[i]=Json.GetString(member,"uid",""); }
            }
            var players = d["players"] as List<object>; var ranks = d["ranks"] as List<object>;
            var positions = d["positions"] as List<object>; var pools = d["totals"] as List<object>;
            if (players == null || players.Count != 8 || ranks == null || ranks.Count != 8 || positions == null || positions.Count != 8 || pools == null || pools.Count != 8) throw new FormatException("房间响应维度错误");
            for (int i = 0; i < 8; i++)
            {
                var player = players[i] as Dictionary<string, object>;
                r.Uids[i] = Json.GetString(player, "uid", ""); r.Names[i] = Json.GetString(player, "name", "");
                r.Ranks[i] = Convert.ToInt32(ranks[i], CultureInfo.InvariantCulture);
                var pos = positions[i] as Dictionary<string, object>;
                if (pos != null) { r.MyRanks[i] = (int)Json.GetNumber(pos, "rank", 0); r.Stakes[i] = (long)Json.GetNumber(pos, "stake", 0); }
                var totals = pools[i] as List<object>; if (totals == null || totals.Count != 8) throw new FormatException("资金池维度错误");
                for (int j = 0; j < 8; j++) r.Pools[i * 8 + j] = Convert.ToInt64(totals[j], CultureInfo.InvariantCulture);
            }
            return r;
        }
    }

}
