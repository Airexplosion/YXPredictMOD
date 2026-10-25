using System;
using System.Collections.Generic;
using Yx.Shared;
using YxPrediction;

public static class RoomFixFixture
{
    static void Require(bool value) { if (!value) throw new Exception("Room follow-up regression"); }
    static Dictionary<string, object> Final(int known)
    {
        var ranks = new List<object>(); for (int i = 0; i < 8; i++) ranks.Add(i < known ? i + 1 : 0);
        return new Dictionary<string, object> { { "id", "game" }, { "round", 10 }, { "ended", true }, { "ranks", ranks } };
    }
    public static void VerifyFinals()
    {
        var queue = new RoomFinalSync();
        Require(queue.Capture("https://one.example", "one", "123456", Final(3)));
        Require(queue.Capture("https://one.example", "one", "123456", Final(8)));
        Require(!queue.Capture("https://one.example", "one", "123456", Final(3)));
        Require(!queue.Capture("https://one.example", "one", "123456", Final(8)));
        Require(queue.Capture("https://two.example", "two", "123456", Final(8)));
        queue = RoomFinalSync.Read(Json.ParseObject(Json.Serialize(queue.Write())));
        Require(queue.Count == 2);
        var job = queue.At(0);
        Require(job.Server == "https://one.example" && job.Token == "one");
        Require(!queue.Confirm(job, job.Code, job.GameId, false));
        Require(!queue.Confirm(job, job.Code, "wrong-game", true));
        Require(queue.Confirm(job, job.Code, job.GameId, true));
        Require(!queue.Confirm(job, job.Code, job.GameId, true));
        Require(queue.Count == 1 && queue.At(0).Token == "two");
        var copy = queue.Write(); ((List<object>)copy["jobs"]).Clear(); Require(queue.Count == 1);
    }
    public static void VerifySelections()
    {
        var r = new RoomState { Code = "one", GameId = "game" };
        for (int i = 0; i < 8; i++) r.Uids[i] = "uid" + i;
        var shared = new RoomSelections(); shared.Observe(r);
        Require(shared.Selected("uid0") == 0);
        shared.Step("uid0", true); shared.Step("uid0", true); Require(shared.Selected("uid0") == 2);
        shared.Observe(r); Require(shared.Selected("uid0") == 2); // 报价刷新保留草稿
        r.MyRanks[0] = 4; r.Stakes[0] = 100; shared.Observe(r); Require(shared.Selected("uid0") == 4);
        shared.Step("uid0", false); Require(shared.Selected("uid0") == 3);
        r.MyRanks[0] = 6; shared.Observe(r); Require(shared.Selected("uid0") == 6); // 另一界面换猜
        r.Uids[0] = "uid7"; r.Uids[7] = "uid0"; r.MyRanks[0] = 0; r.Stakes[0] = 0; r.MyRanks[7] = 6; r.Stakes[7] = 100;
        shared.Observe(r); Require(shared.Selected("uid0") == 6 && shared.Selected("uid7") == 0);
        Require(RoomSelections.DisplayOrder(r)[0] == 7);
        r.Stakes[7] = 0; r.MyRanks[7] = 0; shared.Observe(r); Require(shared.Selected("uid0") == 0);
        shared.Step("uid0", false); Require(shared.Selected("uid0") == 8);
        r.Code = "two"; shared.Observe(r); Require(shared.Selected("uid0") == 0);
    }
}
