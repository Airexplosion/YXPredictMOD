#if YX_GAME
namespace YxPrediction
{
    // 每帧、开面板和点击交易都读当前身份，不等待盘口快照的半秒轮询。
    // 称号共用的名单、查分及状态采集不受此开关影响。
    public static class PredictionVisibility
    {
        public static bool CanShow()
        {
            BattleManager manager = BattleManager.Instance;
            return manager != null && SceneLoader.currentSceneName == "Battle"
                && IsLiveObserver(manager, manager.currentGameStatus);
        }

        public static bool IsLiveObserver(BattleManager manager, Proto.GameStatus status)
        {
            return manager != null && status != null && !manager.replaying
                && status.gameMode != Proto.GameMode.ReviewMode
                && (manager.isSpectating || manager.isRealtimeSpectating || manager.isJudge)
                && !GameStatusExtension.SelfIsPlayer(status);
        }
    }
}
#endif
