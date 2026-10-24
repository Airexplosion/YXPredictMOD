namespace YxPrediction
{
    public static class GameRanks
    {
        // Proto.BattlePlayerData.rank / LifeRankInfo.rank 为0起始；游戏PlayerScoreItem显示时也加1。
        // 模组内部统一使用1～8，0仅表示尚未确定。
        public static int FromProtocol(int rank) { return rank >= 0 && rank < 8 ? rank + 1 : 0; }

        public static PlayerState ReadState(int life, int wins, int losses, int protocolRank)
        {
            return new PlayerState(life, wins, losses, life <= 0 ? FromProtocol(protocolRank) : 0);
        }
    }
}
