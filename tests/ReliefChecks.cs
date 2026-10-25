using System.Collections.Generic;
using Yx.ModSdk.Testing;
using Yx.Shared;
using YxPrediction;

internal static partial class Program
{
    static void ReliefChecks()
    {
        Check("bankruptcy relief adds fifty below ten and repeated checks do not grant twice", delegate
        {
            foreach (long balance in new long[] { 0, 1, 9, 10, 11, 1000 })
            {
                var book = new PredictionBook(balance);
                Equal(book.GrantBankruptcyRelief(), balance < 10);
                Equal(book.Balance, balance < 10 ? balance + 50 : balance);
                int revision = book.Revision;
                True(!book.GrantBankruptcyRelief()); Equal(book.Revision, revision);
                book = Reload(book); True(!book.GrantBankruptcyRelief());
            }
        });
        Check("saved bankrupt wallet receives relief on load and persists it across repeated reloads", delegate
        {
            using (TestHost host = TestHost.Start())
            {
                var data = new Dictionary<string, object> { { PredictionMod.SaveKey, BookCodec.Write(new PredictionBook(9)) } };
                var mod = host.Load<PredictionMod>(Id, "0.7.8", data: data); Equal(mod.Book.Balance, 59L);
                for (int i = 0; i < 3; i++)
                {
                    data[PredictionMod.SaveKey] = Json.ParseObject(Json.Serialize(BookCodec.Write(mod.Book)));
                    mod = host.Reload<PredictionMod>(Id, "0.7.8", data: data); Equal(mod.Book.Balance, 59L);
                    mod.OnUpdate(); Equal(mod.Book.Balance, 59L);
                }
            }
        });
        Check("successful purchases grant relief without changing locked rights and can qualify again", delegate
        {
            using (TestHost host = TestHost.Start())
            {
                var mod = host.Load<PredictionMod>(Id, "0.7.8"); var s = Snapshot(1); mod.AcceptSnapshot(s, Ratings(false));
                string message = mod.Trade(0, "player0", 1, 1000, mod.Current.TradeVersion);
                Equal(mod.Book.Balance, 50L); Equal(mod.Current.Positions[0].Invested, 1000L); True(message.Contains("救济金"));
                long oldClaim = mod.Current.Positions[0].Claim;
                mod.Trade(0, "player0", 1, 50, mod.Current.TradeVersion);
                Equal(mod.Book.Balance, 50L); Equal(mod.Current.Positions[0].Invested, 1050L); True(mod.Current.Positions[0].Claim > oldClaim);
                mod.OnUpdate(); Equal(mod.Book.Balance, 50L);
            }
        });
        Check("ten is not bankrupt while spending to nine grants fifty and invalid actions grant nothing", delegate
        {
            using (TestHost host = TestHost.Start())
            {
                var data = new Dictionary<string, object> { { PredictionMod.SaveKey, BookCodec.Write(new PredictionBook(10)) } };
                var mod = host.Load<PredictionMod>(Id, "0.7.8", data: data); Equal(mod.Book.Balance, 10L);
                var s = Snapshot(1); mod.AcceptSnapshot(s, Ratings(false));
                mod.Trade(0, "player0", 1, 11, mod.Current.TradeVersion); Equal(mod.Book.Balance, 10L);
                mod.Trade(0, "player0", 1, 1, mod.Current.TradeVersion); Equal(mod.Book.Balance, 59L);
                long claim = mod.Current.Positions[0].Claim;
                mod.Trade(0, "player0", 1, 1000, mod.Current.TradeVersion); Equal(mod.Book.Balance, 59L); Equal(mod.Current.Positions[0].Claim, claim);
            }
        });
    }
}
