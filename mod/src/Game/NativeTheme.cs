#if YX_GAME
using Yx.ModSdk;
using Yx.ModSdk.Unity;
namespace YxPrediction
{
    public sealed class NativeTheme : NativePanelTheme
    {
        public NativeTheme(ModContext ctx) : base(ctx) { }
        public void RefreshSource()
        {
            if (NativeRoomSpectate.IsLobby)
            {
                string id; var button=LobbyRoomEntry.SelectedButton(out id);
                if(button!=null) SetSource(button.transform.parent);
            }
            else { var panel=GameBridge.GetBattlePanel(); if(panel!=null) SetSource(panel.transform); }
        }
    }
}
#endif
