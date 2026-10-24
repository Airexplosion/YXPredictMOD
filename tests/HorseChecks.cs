using System;
using System.Collections.Generic;
using Yx.Shared;
using YxPrediction;

internal static partial class Program
{
    static HorseProfile[] HorseProfiles()
    {
        var profiles = new HorseProfile[8];
        for (int i = 0; i < 8; i++) profiles[i] = new HorseProfile(new Rating(4400 - i * 100, 100, true), 10, 100, i == 7);
        return profiles;
    }
    static void HorseWins(MatchSnapshot snapshot, int index, int wins, int losses = 0)
    { snapshot.States[index] = new PlayerState(100, wins, losses, 0); }
    static void HorseChecks()
    {
        Check("horse profile reads exact UID even when two players share their name", delegate
        {
            var shard = Json.ParseObject("{\"one\":{\"name\":\"same\",\"rating\":4100,\"sigma\":100,\"dx\":20,\"games\":50},\"two\":{\"name\":\"same\",\"rating\":3200,\"sigma\":200}}");
            var first = HorseProfile.Read(shard, "one");
            Near(first.Rating.Score, 4100); Equal(first.DaoXinGames, 20); Equal(first.Games, 50);
            Near(HorseProfile.Read(shard, "two").Rating.Score, 3200);
            True(!HorseProfile.Read(shard, "missing").Rating.Found);
        });
        Check("carousel uses latest numeric season and ignores incomplete or invalid season values", delegate
        {
            var shard = Json.ParseObject("{\"one\":{\"rating\":4000,\"sigma\":100,\"seasons\":{\"9\":{\"start\":4000,\"peak\":4300,\"end\":3900},\"10\":{\"start\":3900,\"peak\":4100,\"end\":4000}}}}");
            True(!HorseProfile.Read(shard, "one").Spinning);
            var latest = Json.GetObject(Json.GetObject(Json.GetObject(shard, "one"), "seasons"), "10");
            latest["end"] = 3900; True(HorseProfile.Read(shard, "one").Spinning);
            latest.Remove("start"); True(!HorseProfile.Read(shard, "one").Spinning);
            latest["start"] = 3900; latest["peak"] = double.NaN; True(!HorseProfile.Read(shard, "one").Spinning);
        });
        Check("horse titles use real ratings only and assign large bottom gaps as seahorse", delegate
        {
            var s = Snapshot(1); var profiles = HorseProfiles(); var roster = new HorseRoster();
            profiles[6] = new HorseProfile(new Rating(3600, 100, true));
            profiles[7] = new HorseProfile(new Rating(3100, 100, true));
            roster.Observe(s, profiles);
            Equal(roster.Find(s.Players[0].Uid).Title, "特等马"); Equal(roster.Find(s.Players[1].Uid).Title, "上等马");
            Equal(roster.Find(s.Players[7].Uid).Title, "海马");
            profiles[7] = new HorseProfile(Rating.Unknown()); roster.Observe(s, profiles);
            Equal(roster.Find(s.Players[7].Uid).Title, ""); Equal(roster.Find(s.Players[6].Uid).Title, "牛马");
        });
        Check("equal ratings share titles and missing ratings do not invent a top or bottom horse", delegate
        {
            var s = Snapshot(1); var profiles = HorseProfiles(); var roster = new HorseRoster();
            for (int i = 0; i < 8; i++) profiles[i] = new HorseProfile(new Rating(3500, 100, true));
            roster.Observe(s, profiles);
            for (int i = 0; i < 8; i++) Equal(roster.Find(s.Players[i].Uid).Title, "中等马");
            for (int i = 1; i < 8; i++) profiles[i] = new HorseProfile(Rating.Unknown());
            roster.Observe(s, profiles); Equal(roster.Find(s.Players[0].Uid).Title, "中等马");
        });
        Check("dark horse counts observed wins once and survives duplicate polling without losing carousel tag", delegate
        {
            var s = Snapshot(1); var roster = new HorseRoster(); var profiles = HorseProfiles(); roster.Observe(s, profiles);
            s.Round = 2; HorseWins(s, 7, 1); roster.Observe(s, profiles); roster.Observe(s, profiles);
            var member = roster.Find(s.Players[7].Uid); Equal(member.ConsecutiveWins, 1); True(!member.DarkHorse);
            s.Round = 3; HorseWins(s, 7, 2); roster.Observe(s, profiles); True(member.DarkHorse);
            for (int i = 0; i < 10; i++) roster.Observe(s, profiles);
            True(member.DarkHorse); Equal(member.Tags, "旋转木马"); Equal(member.ConsecutiveWins, 2);
            s.Round = 4; HorseWins(s, 7, 2, 1); roster.Observe(s, profiles);
            True(!member.DarkHorse); Equal(member.Tags, "旋转木马"); Equal(member.Title, "牛马");
        });
        Check("late join does not mistake cumulative victories for consecutive wins", delegate
        {
            var s = Snapshot(12); var roster = new HorseRoster(); var profiles = HorseProfiles(); HorseWins(s, 7, 8);
            roster.Observe(s, profiles); var member = roster.Find(s.Players[7].Uid); True(!member.DarkHorse);
            s.Round = 13; HorseWins(s, 7, 9); roster.Observe(s, profiles); True(!member.DarkHorse);
            s.Round = 14; HorseWins(s, 7, 10); roster.Observe(s, profiles); True(member.DarkHorse);
        });
        Check("two counter changes in the same round cannot award a two-round streak", delegate
        {
            var s = Snapshot(1); var roster = new HorseRoster(); var profiles = HorseProfiles(); roster.Observe(s, profiles);
            s.Round = 2; HorseWins(s, 7, 1); roster.Observe(s, profiles);
            HorseWins(s, 7, 2); roster.Observe(s, profiles);
            Equal(roster.Find(s.Players[7].Uid).ConsecutiveWins, 1); True(!roster.Find(s.Players[7].Uid).DarkHorse);
        });
        Check("missing rounds and multiwin jumps do not invent a consecutive streak", delegate
        {
            var s = Snapshot(1); var roster = new HorseRoster(); var profiles = HorseProfiles(); roster.Observe(s, profiles);
            s.Round = 4; HorseWins(s, 7, 3); roster.Observe(s, profiles);
            True(!roster.Find(s.Players[7].Uid).DarkHorse);
            s.Round = 5; HorseWins(s, 7, 4); roster.Observe(s, profiles);
            s.Round = 8; HorseWins(s, 7, 5); roster.Observe(s, profiles);
            Equal(roster.Find(s.Players[7].Uid).ConsecutiveWins, 1);
        });
        Check("reordered roster and renamed player retain UID state and accept new profile data", delegate
        {
            var s = Snapshot(1); var roster = new HorseRoster(); var profiles = HorseProfiles(); roster.Observe(s, profiles);
            string uid = s.Players[7].Uid; s.Round = 2; HorseWins(s, 7, 1); roster.Observe(s, profiles);
            Player player = s.Players[0]; s.Players[0] = new Player(uid, "new name", false); s.Players[7] = player;
            PlayerState state = s.States[0]; s.States[0] = s.States[7]; s.States[7] = state;
            HorseProfile profile = profiles[0]; profiles[0] = profiles[7]; profiles[7] = profile;
            roster.Observe(s, profiles); var member = roster.Find(uid); Equal(member.Name, "new name"); Equal(member.ConsecutiveWins, 1);
            s.Round = 3; HorseWins(s, 0, 2); roster.Observe(s, profiles); True(member.DarkHorse);
        });
        Check("new match with same players resets dark horse baseline", delegate
        {
            var s = Snapshot(1); var roster = new HorseRoster(); var profiles = HorseProfiles(); roster.Observe(s, profiles);
            s.Round = 2; HorseWins(s, 7, 1); roster.Observe(s, profiles);
            s.Round = 3; HorseWins(s, 7, 2); roster.Observe(s, profiles); True(roster.Find(s.Players[7].Uid).DarkHorse);
            s.Id = "new-match"; roster.Observe(s, profiles);
            True(!roster.Find(s.Players[7].Uid).DarkHorse); Equal(roster.Find(s.Players[7].Uid).ConsecutiveWins, 0);
        });
        Check("stale round and decreasing counters cannot count wins a second time", delegate
        {
            var s = Snapshot(1); var roster = new HorseRoster(); var profiles = HorseProfiles(); roster.Observe(s, profiles);
            s.Round = 2; HorseWins(s, 7, 1); roster.Observe(s, profiles);
            s.Round = 1; HorseWins(s, 7, 0); roster.Observe(s, profiles);
            s.Round = 2; HorseWins(s, 7, 1); roster.Observe(s, profiles); Equal(roster.Find(s.Players[7].Uid).ConsecutiveWins, 1);
            s.Round = 3; HorseWins(s, 7, 2); roster.Observe(s, profiles); True(roster.Find(s.Players[7].Uid).DarkHorse);
            s.States[7] = new PlayerState(0, 2, 0, 8); roster.Observe(s, profiles); True(!roster.Find(s.Players[7].Uid).DarkHorse);
            roster.Observe(null, null); Equal(roster.Members, null);
        });
    }
}
