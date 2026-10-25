using System;
using System.Collections.Generic;
using Yx.Shared;

namespace YxPrediction
{
    public sealed class RoomMatch
    {
        public readonly string Code, GameId, NativeRoomId;
        public readonly double Deadline;
        public RoomMatch(Dictionary<string, object> data, double now)
        {
            Code = Json.GetString(data, "code", ""); GameId = Json.GetString(data, "gameId", ""); NativeRoomId = Json.GetString(data, "gameRoomId", "");
            if (Code.Length != 6 || GameId.Length == 0 || GameId.Length > 1024 || NativeRoomId.Length == 0 || NativeRoomId.Length > 128) throw new FormatException("匹配信息无效");
            for (int i = 0; i < Code.Length; i++) if (Code[i] < '0' || Code[i] > '9') throw new FormatException("匹配房间号无效");
            for (int i = 0; i < NativeRoomId.Length; i++)
            {
                char c = NativeRoomId[i];
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '_' && c != '-') throw new FormatException("观战目标无效");
            }
            Deadline = now + 90;
        }
        public bool Expired(double now) { return now >= Deadline; }
        public bool CanJoin(MatchSnapshot snapshot, double now)
        {
            return !Expired(now) && snapshot != null && snapshot.Id == GameId && snapshot.LiveObserver && !snapshot.Ended && snapshot.IsEightPlayerMatch();
        }
    }
}
