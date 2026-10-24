using System.Globalization;

namespace YxPrediction
{
    // 单人面板与总表共用草稿；仅持仓名次变化时重新跟随持仓，报价刷新不覆盖手动选择。
    public sealed class RankSelection
    {
        readonly int[] _selected = new int[8];
        readonly int[] _purchased = new int[8];
        string _marketId;

        public void Observe(Market market)
        {
            string id = market == null ? null : market.Id;
            bool changed = _marketId != id;
            _marketId = id;
            for (int i = 0; i < 8; i++)
            {
                int rank = PurchasedRank(market, i);
                if (changed || market == null || rank != _purchased[i]) _selected[i] = rank;
                _purchased[i] = rank;
            }
        }

        public int Selected(int player) { return player >= 0 && player < 8 ? _selected[player] : 0; }

        public bool SelectText(int player, string text)
        {
            if (player < 0 || player >= 8) return false;
            int rank = 0;
            if (!string.IsNullOrEmpty(text) && (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out rank) || rank < 1 || rank > 8)) return false;
            _selected[player] = rank;
            return true;
        }

        public void Step(int player, bool forward)
        {
            if (player < 0 || player >= 8) return;
            int rank = _selected[player];
            _selected[player] = rank == 0 ? (forward ? 1 : 8) : (forward ? rank % 8 + 1 : (rank + 6) % 8 + 1);
        }

        public static int PurchasedRank(Market market, int player)
        {
            if (market == null || player < 0 || player >= 8) return 0;
            if (!market.IsTrading) return market.Tickets[player] == null ? 0 : market.Tickets[player].Rank;
            Position p = market.Positions[player];
            return p.Claim > 0 ? p.Rank : 0;
        }

        public static int[] DisplayOrder(Market market)
        {
            var order = new int[8];
            int next = 0;
            // 两组内部保持原玩家顺序；排序只改变展示位置，不改变盘口的玩家下标。
            for (int group = 0; group < 2; group++)
                for (int i = 0; i < 8; i++)
                    if ((PurchasedRank(market, i) > 0 ? 0 : 1) == group) order[next++] = i;
            return order;
        }
    }
}
