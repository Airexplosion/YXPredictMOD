using System;
using System.Collections.Generic;
using System.IO;
using Yx.Shared;
using YxPrediction;
internal static partial class Program
{
    static void ExportRuleParity(string path)
    {
        var scenarios=new List<object>();
        for(int scenario=0;scenario<4;scenario++)
        {
            MatchSnapshot s=Snapshot(scenario%2==0?1:6);PredictionBook b=TradeBook(s);Market m=b.Find(s.Id);
            var row=new Dictionary<string,object>();var players=new List<object>();var rates=new List<object>();
            for(int i=0;i<8;i++){players.Add(new Dictionary<string,object>{{"uid",s.Players[i].Uid},{"name",s.Players[i].Name}});for(int rank=0;rank<8;rank++)rates.Add(m.QuoteRates[i*8+rank]);}
            row["id"]=s.Id;row["round"]=s.Round;row["players"]=players;row["baseRates"]=rates;
            var steps=new List<object>();
            string[] kinds={"buy","buy","half","switch","sell","buy","buy","switch"};
            int[] ranks={1,1,0,2,0,3,3,3};long[] stakes={101,37,0,53,0,100,9,10000};
            for(int j=0;j<kinds.Length;j++)
            {
                int player=j>=5?1:0;string error;
                if(kinds[j]=="buy")error=b.Buy(s,s.Players[player].Uid,ranks[j],stakes[j],m.TradeVersion);
                else if(kinds[j]=="half"||kinds[j]=="sell")error=b.Sell(s,s.Players[player].Uid,kinds[j]=="half",m.TradeVersion);
                else error=b.Switch(s,s.Players[player].Uid,ranks[j],stakes[j],m.TradeVersion);
                var positions=new List<object>();for(int i=0;i<8;i++)positions.Add(new Dictionary<string,object>{{"claim",m.Positions[i].Claim},{"cost",m.Positions[i].Cost},{"rank",m.Positions[i].Rank},{"resolved",m.Positions[i].Resolved}});
                steps.Add(new Dictionary<string,object>{{"kind",kinds[j]},{"player",player},{"rank",ranks[j]},{"stake",stakes[j]},{"ok",error==null},{"balance",b.Balance},{"positions",positions}});
            }
            s.Ended=true;s.Ranks=new int[]{1,2,3,4,5,6,7,8};
            if(scenario>=2)s.Ranks=new int[]{2,3,1,4,5,6,7,8};
            b.Observe(s,null);row["steps"]=steps;row["ranks"]=new List<object>{s.Ranks[0],s.Ranks[1],s.Ranks[2],s.Ranks[3],s.Ranks[4],s.Ranks[5],s.Ranks[6],s.Ranks[7]};row["finalBalance"]=b.Balance;row["payout"]=m.TotalPayout;scenarios.Add(row);
        }
        File.WriteAllText(path,Json.Serialize(scenarios));Console.WriteLine("Single-player oracle exported: "+path);
    }
}
