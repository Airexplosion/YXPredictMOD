using System;
using System.Collections.Generic;
using Yx.Shared;
using Yx.ModSdk;
using Yx.ModSdk.Testing;
using YxPrediction;
namespace UnityEngine { public static class Time { public static float unscaledTime; } }
namespace UnityEngine.Networking
{
    public sealed class DownloadHandlerBuffer { public string text; }
    public sealed class UnityWebRequest
    {
        public static readonly List<UnityWebRequest> Sent = new List<UnityWebRequest>();
        public string url, error; public int timeout;
        public bool isDone, isNetworkError, isHttpError, Disposed;
        public DownloadHandlerBuffer downloadHandler = new DownloadHandlerBuffer();
        public static UnityWebRequest Get(string url) { return new UnityWebRequest { url=url }; }
        public void SendWebRequest() { Sent.Add(this); }
        public void Abort() { } public void Dispose() { Disposed=true; }
    }
}
namespace YxPrediction
{
    public sealed class PredictionMod : YxMod
    {
        public EloClient Elo;
        public override void OnLoad(ModContext ctx) { Elo=new EloClient(ctx); }
        public override void OnDisable() { Elo.Destroy(); }
        public static string N(long value) { return value.ToString(); }
    }
}
internal static class Program
{
    static int _checks;
    static void Require(bool value,string message) { _checks++;if(!value)throw new Exception(message); }
    static Player[] Players(bool shared=false)
    {
        var p=new Player[8];for(int i=0;i<8;i++)p[i]=new Player(i.ToString("x22")+(shared?"aa":i.ToString("x2")),"player",false);return p;
    }
    static void Complete(Player[] players,bool missing=false)
    {
        foreach(var request in UnityEngine.Networking.UnityWebRequest.Sent)
        {
            if(request.isDone)continue;
            var rows=new Dictionary<string,object>();
            if(!missing)foreach(var p in players)if(request.url.EndsWith(p.Uid.Substring(22)+".json"))
                rows[p.Uid]=new Dictionary<string,object>{{"rating",4200},{"sigma",150},{"dx",12}};
            request.downloadHandler.text=Json.Serialize(rows);request.isDone=true;
        }
    }
    static PredictionMod Load(TestHost host)
    {
        UnityEngine.Networking.UnityWebRequest.Sent.Clear();UnityEngine.Time.unscaledTime=0;
        return host.Load<PredictionMod>("elo.checks","0.7.6");
    }
    static void Main()
    {
        using(var host=TestHost.Start())
        {
            var mod=Load(host);var p=Players();
            Require(mod.Elo.Get(p)==null,"Unexpected pre-request result");
            Require(UnityEngine.Networking.UnityWebRequest.Sent.Count==4,"Requests not started concurrently/bounded to four");
            for(int frame=0;frame<40;frame++){Complete(p);mod.Elo.Update();}
            Require(UnityEngine.Networking.UnityWebRequest.Sent.Count==8,"Next wave waits for the half-second game poll");
            var ratings=mod.Elo.Get(p);Require(ratings!=null&&ratings[7].Found,"Concurrent query did not finish all eight");
            var profiles=mod.Elo.GetHorseProfiles(p);Require(profiles[7].DaoXinGames==12,"Horse metadata lost");
            Require(UnityEngine.Networking.UnityWebRequest.Sent.Count==8,"Cached profiles re-requested");
            mod.Elo.Destroy();foreach(var req in UnityEngine.Networking.UnityWebRequest.Sent)Require(req.Disposed,"Request leaked");
        }
        using(var host=TestHost.Start())
        {
            var mod=Load(host);var p=Players(true);mod.Elo.Get(p);
            Require(UnityEngine.Networking.UnityWebRequest.Sent.Count==1,"Same shard downloaded more than once");
            Complete(p);for(int frame=0;frame<10;frame++)mod.Elo.Update();
            Require(mod.Elo.Get(p)!=null&&mod.Elo.GetHorseProfiles(p)[7].Rating.Found,"Shared shard missed roster members");
        }
        using(var host=TestHost.Start())
        {
            var mod=Load(host);var p=Players();mod.Elo.Get(p);
            var failed=UnityEngine.Networking.UnityWebRequest.Sent[0];failed.isNetworkError=true;failed.error="timeout";failed.isDone=true;
            for(int frame=0;frame<40;frame++){Complete(p);mod.Elo.Update();}
            var result=mod.Elo.Get(p);Require(result!=null&&!result[0].Found&&result[7].Found,"One failure blocked unrelated shards");
            Require(mod.Elo.Summary.Contains("请求失败 1"),"Failure mislabeled as missing");
            UnityEngine.Time.unscaledTime=31;Require(mod.Elo.Get(p)==null,"Failed shard did not retry after cooldown");
            Require(UnityEngine.Networking.UnityWebRequest.Sent.Count==9,"Retry downloaded cached successful shards");
            Complete(p);for(int frame=0;frame<10;frame++)mod.Elo.Update();Require(mod.Elo.Get(p)[0].Found,"Retry did not recover");
        }
        using(var host=TestHost.Start())
        {
            var mod=Load(host);var p=Players(true);mod.Elo.Get(p);Complete(p,true);
            for(int i=0;i<10;i++)mod.Elo.Update();Require(!mod.Elo.Get(p)[0].Found&&mod.Elo.Summary.Contains("未收录 8"),"Missing IDs were reported as failures");
            mod.Elo.Get(p);Require(UnityEngine.Networking.UnityWebRequest.Sent.Count==1,"Missing IDs re-download same shard");
        }
        using(var host=TestHost.Start())
        {
            var mod=Load(host);var p=Players();for(int i=0;i<8;i++)p[i]=new Player("invalid","AI",i<4);
            Require(mod.Elo.Get(p)!=null&&UnityEngine.Networking.UnityWebRequest.Sent.Count==0,"AI/invalid UID started HTTP");
        }
        using(var host=TestHost.Start())
        {
            var mod=Load(host);mod.Elo.Get(Players());mod.Elo.Destroy();mod.Elo.Update();
            foreach(var req in UnityEngine.Networking.UnityWebRequest.Sent)Require(req.Disposed,"Destroy leaked pending HTTP");
            Require(UnityEngine.Networking.UnityWebRequest.Sent.Count==4,"Destroy restarted pending jobs");
        }
        Console.WriteLine("Elo concurrent pipeline checks passed: "+_checks+" (production client, offline network doubles)");
    }
}
