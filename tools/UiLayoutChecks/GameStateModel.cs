// 仅供UI检查程序运行生产代码的身份判断；不进入MOD DLL或安装包。
public sealed class BattleManager
{
    public static BattleManager Instance;
    public bool replaying, isSpectating, isRealtimeSpectating, isJudge;
    public Proto.GameStatus currentGameStatus;
}
internal static class SceneLoader { public static string currentSceneName; }
internal static class GameStatusExtension
{
    public static bool SelfIsPlayer(Proto.GameStatus status) { return status.Participating; }
}
namespace Proto
{
    public enum GameMode { Normal, ReviewMode }
    public sealed class GameStatus
    {
        public GameMode gameMode;
        public bool Participating;
    }
}
