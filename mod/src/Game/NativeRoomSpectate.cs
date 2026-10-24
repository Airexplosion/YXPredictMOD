#if YX_GAME
using System;
using Proto;

namespace YxPrediction
{
    public static class NativeRoomSpectate
    {
        public static bool IsLobby { get { return SceneLoader.currentSceneName == "Lobby"; } }
        public static string CurrentRoomId()
        {
            var client = GameClientUtil.client;
            return client == null || client.room == null ? "" : client.room.Id;
        }
        public static bool Request(string roomId, out string error)
        {
            error = null;
            if (!IsLobby) { error = "请先通过游戏返回大厅，再随机加入其他对局"; return false; }
            if (!OpenManager.IsOpen(OpenType.OpenXianFuLobby)) { error = "当前账号未开放仙府大厅观战入口"; return false; }
            try
            {
                var lobby = ILRPanelBase.FindILRPanel<LobbyPanel>();
                if (lobby == null) { error = "等待游戏大厅就绪"; return false; }
                // Same native panel workflow as its SpectateButton. The game's confirmation,
                // group readiness checks, permissions and asynchronous scene transition remain native.
                var panel = lobby.FindILRSubPanelRuntime<XianFuLobbyPanel>();
                if (panel == null) { error = "原生观战面板不可用"; return false; }
                panel.Show();
                panel.SpectateRequest(roomId);
                return true;
            }
            catch { error = "原生观战入口未能打开，请查看游戏提示后重试"; return false; }
        }
    }
}
#endif
