#if YX_GAME
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
namespace YxPrediction
{
    /// <summary>Persistent upper-right shortcut to the same single/multiplayer overview.</summary>
    public sealed class PredictionOverviewEntry
    {
        readonly PredictionMod _mod;
        readonly NativeTheme _theme;
        readonly UiKit _ui;
        GameObject _root;
        public PredictionOverviewEntry(PredictionMod mod, ModContext ctx, NativeTheme theme) { _mod=mod;_theme=theme;_ui=new UiKit(ctx); }
        void Toggle(){_mod.OnlinePanel.Toggle();}
        public void Update()
        {
            bool visible=PredictionVisibility.CanShow()&&!GameBridge.IsPlayingBattle();
            if(_root!=null)_root.SetActive(visible);
            if(!visible)return;
            if(_root==null)
            {
                _root=_ui.Panel("PredictionOverviewEntry",new Vector2(1f,1f),new Vector2(1f,1f),new Vector2(-110f,-28f),new Vector2(144f,44f),Color.clear,false);
                if(_root==null)return;
                _theme.CreateButtonTopLeft(_root.transform,"PredictionOverviewButton","预测总览",0f,0f,144f,44f,NativePanelButtonStyle.Secondary,Toggle,20);
            }
        }
        public void Destroy(){_ui.DestroyAll();_root=null;}
    }
}
#endif
