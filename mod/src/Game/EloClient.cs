#if YX_GAME
using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using Yx.ModSdk;
using Yx.Shared;

namespace YxPrediction
{
    // 轮询Unity异步请求，不在游戏主线程阻塞，也不使用ILRuntime不支持的async/迭代器。
    public sealed class EloClient
    {
        readonly ModContext _ctx;
        readonly Dictionary<string, Dictionary<string, object>> _shards = new Dictionary<string, Dictionary<string, object>>();
        readonly List<string> _order = new List<string>();
        UnityWebRequest _request;
        string _suffix;
        public string Summary { get; private set; }
        public EloClient(ModContext ctx) { _ctx = ctx; }

        public Rating[] Get(Player[] players)
        {
            var ratings = new Rating[8];
            bool waiting = false;
            int found = 0, missing = 0, failed = 0, invalid = 0, ai = 0, completed = 0;
            for (int i = 0; i < 8; i++)
            {
                if (players[i].IsAi) { ai++; completed++; ratings[i] = Rating.Unknown(); continue; }
                if (!EloData.IsPublicUid(players[i].Uid)) { invalid++; completed++; ratings[i] = Rating.Unknown(); continue; }
                string uid = players[i].Uid;
                string suffix = uid.Substring(uid.Length - 2);
                Dictionary<string, object> shard;
                if (!_shards.TryGetValue(suffix, out shard))
                {
                    waiting = true;
                    if (_request == null) Begin(uid, suffix);
                    continue;
                }
                completed++;
                if (shard == null) { failed++; ratings[i] = Rating.Unknown(); continue; }
                try
                {
                    ratings[i] = EloData.ReadPlayer(shard, uid);
                    if (ratings[i].Found) found++; else missing++;
                }
                catch (Exception ex)
                {
                    failed++; ratings[i] = Rating.Unknown();
                    _ctx.Log.Warn("Elo分数解析失败：" + ex.Message);
                }
            }
            Summary = waiting ? "Elo查询中，已处理 " + PredictionMod.N(completed) + "/8"
                : "Elo：命中 " + PredictionMod.N(found) + " / 未收录 " + PredictionMod.N(missing) + " / 请求失败 " + PredictionMod.N(failed) + " / UID异常 " + PredictionMod.N(invalid) + " / AI " + PredictionMod.N(ai);
            return waiting ? null : ratings;
        }

        public HorseProfile[] GetHorseProfiles(Player[] players)
        {
            if (Get(players) == null) return null; // 和预测共用请求及分片缓存，不用昵称反查或改写UID。
            var profiles = new HorseProfile[8];
            for (int i = 0; i < 8; i++)
            {
                Dictionary<string, object> shard;
                string uid = players[i].Uid;
                if (players[i].IsAi || !EloData.IsPublicUid(uid) || !_shards.TryGetValue(uid.Substring(uid.Length - 2), out shard) || shard == null)
                { profiles[i] = new HorseProfile(Rating.Unknown()); continue; }
                try { profiles[i] = HorseProfile.Read(shard, uid); }
                catch { profiles[i] = new HorseProfile(Rating.Unknown()); }
            }
            return profiles;
        }

        void Begin(string uid, string suffix)
        {
            _suffix = suffix;
            try
            {
                _request = UnityWebRequest.Get(EloData.Url(uid));
                _request.timeout = 8;
                _request.SendWebRequest();
            }
            catch (Exception ex)
            {
                _ctx.Log.Warn("Elo请求无法启动，将明确标为未查到：" + ex.Message);
                Remember(suffix, null);
                Destroy();
            }
        }

        public void Update()
        {
            if (_request == null || !_request.isDone) return;
            Dictionary<string, object> data = null;
            try
            {
                if (_request.isNetworkError || _request.isHttpError) throw new InvalidOperationException(_request.error);
                string json = _request.downloadHandler.text;
                if (json == null || json.Length > 2097152) throw new FormatException("响应体超出限制");
                data = Json.ParseObject(json);
                if (data == null) throw new FormatException("响应不是JSON对象");
            }
            catch (Exception ex) { _ctx.Log.Warn("Elo查询失败，本次使用标注的3000基准：" + ex.Message); }
            Remember(_suffix, data);
            _request.Dispose();
            _request = null;
        }

        void Remember(string suffix, Dictionary<string, object> data)
        {
            if (!_shards.ContainsKey(suffix)) _order.Add(suffix);
            _shards[suffix] = data;
            if (_order.Count > 16)
            {
                _shards.Remove(_order[0]);
                _order.RemoveAt(0);
            }
        }

        public void Destroy()
        {
            if (_request == null) return;
            _request.Abort(); _request.Dispose(); _request = null;
        }
    }
}
#endif
