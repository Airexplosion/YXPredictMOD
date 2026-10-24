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
        EloShardScan _scan;
        string[] _scanTargets;
        readonly List<string> _covered=new List<string>();
        readonly List<string> _wanted=new List<string>();
        readonly Dictionary<string,HorseProfile> _profiles=new Dictionary<string,HorseProfile>();
        public string Summary { get; private set; }
        public EloClient(ModContext ctx) { _ctx = ctx; }

        public Rating[] Get(Player[] players)
        {
            for(int i=0;i<players.Length;i++)
                if(EloData.IsPublicUid(players[i].Uid)&&!_wanted.Contains(players[i].Uid))
                {
                    _wanted.Add(players[i].Uid);
                    if(_wanted.Count>256){string oldest=_wanted[0];_wanted.RemoveAt(0);_profiles.Remove(oldest);_covered.Remove(oldest);}
                }
            for(int i=0;i<players.Length;i++) if(EloData.IsPublicUid(players[i].Uid)&&!_covered.Contains(players[i].Uid)) { Dictionary<string,object> existing;string key=players[i].Uid.Substring(22);if(_shards.TryGetValue(key,out existing)&&existing!=null)_shards.Remove(key); }
            var ratings = new Rating[8];
            bool waiting = false;
            int found = 0, missing = 0, failed = 0, invalid = 0, ai = 0, completed = 0;
            for (int i = 0; i < 8; i++)
            {
                if (players[i].IsAi) { ai++; completed++; ratings[i] = Rating.Unknown(); continue; }
                if (!EloData.IsPublicUid(players[i].Uid)) { invalid++; completed++; ratings[i] = Rating.Unknown(); continue; }
                string uid = players[i].Uid;
                string suffix = uid.Substring(uid.Length - 2);
                HorseProfile cached;
                if(_profiles.TryGetValue(uid,out cached)){completed++;ratings[i]=cached.Rating;if(cached.Rating.Found)found++;else missing++;continue;}
                Dictionary<string, object> shard;
                if (!_shards.TryGetValue(suffix, out shard))
                {
                    waiting = true;
                    if (_request == null && _scan == null) Begin(uid, suffix);
                    continue;
                }
                completed++;
                if (shard == null) { failed++; ratings[i] = Rating.Unknown(); continue; }
                try
                {
                    HorseProfile profile;
                    if(!_profiles.TryGetValue(uid,out profile)){profile=HorseProfile.Read(shard,uid);_profiles[uid]=profile;}
                    ratings[i] = profile.Rating;
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
                HorseProfile cached;
                string uid = players[i].Uid;
                if(!players[i].IsAi && _profiles.TryGetValue(uid,out cached)) profiles[i]=cached;
                else profiles[i]=new HorseProfile(Rating.Unknown());
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
            if(_scan!=null)
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    do {_scan.Step(256);} while(!_scan.Done && timer.ElapsedMilliseconds<1);
                    if(!_scan.Done)return;
                    Remember(_suffix,_scan.Rows);for(int i=0;i<_scanTargets.Length;i++)if(!_covered.Contains(_scanTargets[i]))_covered.Add(_scanTargets[i]);_scan=null;
                    if(timer.ElapsedMilliseconds>=8)_ctx.Log.Warn("Elo选定资料解析耗时 "+PredictionMod.N(timer.ElapsedMilliseconds)+"ms");
                }
                catch(Exception ex){_ctx.Log.Warn("Elo分帧解析失败："+ex.Message);Remember(_suffix,null);_scan=null;}
                return;
            }
            if (_request == null || !_request.isDone) return;
            try
            {
                if(_request.isNetworkError||_request.isHttpError)throw new InvalidOperationException(_request.error);
                string text=_request.downloadHandler.text;
                var targets=new List<string>();for(int i=0;i<_wanted.Count;i++)if(_wanted[i].Substring(22)==_suffix)targets.Add(_wanted[i]);_scanTargets=targets.ToArray();_scan=new EloShardScan(text,_scanTargets);
            }
            catch(Exception ex){_ctx.Log.Warn("Elo查询失败："+ex.Message);Remember(_suffix,null);}
            _request.Dispose();_request=null;
        }

        void Remember(string suffix, Dictionary<string, object> data)
        {
            if (!_order.Contains(suffix)) _order.Add(suffix);
            _shards[suffix] = data;
            if (_order.Count > 16)
            {
                _shards.Remove(_order[0]);
                _order.RemoveAt(0);
            }
        }

        public void Destroy()
        {
            _scan = null;
            if (_request == null) return;
            _request.Abort(); _request.Dispose(); _request = null;
        }
    }
}
#endif
