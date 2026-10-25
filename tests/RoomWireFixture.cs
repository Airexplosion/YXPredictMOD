using System;
using Yx.Shared;
using YxPrediction;

public static class RoomWireFixture
{
    // Generated from the real Node room engine's response for two members.
    public static void Verify()
    {
        var selection = new System.Collections.Generic.Dictionary<string, object>();
        selection["code"] = "123456"; selection["gameId"] = "fixture-game"; selection["gameRoomId"] = "native-room";
        var match = new RoomMatch(selection, 10);
        if (!match.Expired(100) || match.CanJoin(null, 11)) throw new Exception("Room match wait failed");
        if (Guid.NewGuid().ToString("N").Length != 32) throw new Exception("Room operation ID failed");
        RoomState r = RoomState.Read(Json.ParseObject(@"{""code"":""123456"",""gameId"":""fixture-game"",""host"":true,""members"":2,""memberDetails"":[{""id"":""public-1"",""name"":""游戏甲"",""uid"":""12345""},{""id"":""public-2"",""name"":""游戏乙"",""uid"":""67890""}],""round"":2,""version"":4,""open"":true,""ended"":false,""settled"":false,""players"":[{""uid"":""uid-0"",""name"":""player-0""},{""uid"":""uid-1"",""name"":""player-1""},{""uid"":""uid-2"",""name"":""player-2""},{""uid"":""uid-3"",""name"":""player-3""},{""uid"":""uid-4"",""name"":""player-4""},{""uid"":""uid-5"",""name"":""player-5""},{""uid"":""uid-6"",""name"":""player-6""},{""uid"":""uid-7"",""name"":""player-7""}],""ranks"":[0,0,0,0,0,0,0,0],""totals"":[[200,100,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0]],""positions"":[{""rank"":2,""stake"":100},null,null,null,null,null,null,null],""balance"":900,""payout"":0}"));
        if (r.MemberNames.Length != 2 || r.MemberNames[1] != "游戏乙" || r.MemberUids[0] != "12345") throw new Exception("Room member decoding failed");
        if (r.Code != "123456" || r.GameId != "fixture-game" || r.Members != 2 || r.Balance != 900 || r.Round != 2 || !r.Open || !r.Host) throw new Exception("Room header decoding failed");
        if (r.Uids[0] != "uid-0" || r.MyRanks[0] != 2 || r.Stakes[0] != 100 || r.Pools[0] != 200 || r.Pools[1] != 100) throw new Exception("Room pool/position decoding failed");
        if (!r.AllowMatch) throw new Exception("Legacy room matching default changed");
    }
}
