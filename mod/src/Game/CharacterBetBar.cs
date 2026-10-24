#if YX_GAME
using System.Globalization;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
namespace YxPrediction
{
    public sealed class CharacterBetBar
    {
        readonly PredictionMod _mod;
        readonly NativeTheme _theme;
        readonly UiKit _ui;
        GameObject _root;
        UiInput _stake;
        NativeButton _buy, _half, _sell;
        TMPro.TextMeshProUGUI _name, _rank, _holding;
        int _selected = 1, _index = -1;
        string _uid, _game;
        bool _collapsed;
        public CharacterBetBar(PredictionMod mod, ModContext ctx, NativeTheme theme) { _mod = mod; _theme = theme; _ui = new UiKit(ctx); }
        static void Ignore(string value) { }
        void Build()
        {
            _root = _ui.Panel("PredictionCharacterBetBar", new Vector2(0.5f,0f),new Vector2(0.5f,0f),new Vector2(0f,76f),new Vector2(1010f,70f),NativeTheme.LacquerBlack);
            if (_root == null) return;
            _theme.ApplyWindowFrame(_root.GetComponent<UnityEngine.UI.Image>());
            _name = _ui.Label(_root.transform,"name","",new Vector2(12,-16),new Vector2(160,38),20);
            _theme.CreateButtonTopLeft(_root.transform,"minus","−",176,-16,40,38,NativeBtnStyle.Secondary,Minus,22);
            _rank = _ui.Label(_root.transform,"rank","",new Vector2(222,-16),new Vector2(68,38),20);
            _theme.CreateButtonTopLeft(_root.transform,"plus","+",296,-16,40,38,NativeBtnStyle.Secondary,Plus,22);
            _stake = _ui.NumberInput(_root.transform,"StakeInput",new Vector2(350,-16),new Vector2(90,38),5,Ignore); _stake.SetText("10"); _theme.StyleInput(_root.transform.Find("StakeInput"),22);
            _buy = _theme.CreateButtonTopLeft(_root.transform,"buy","投入",452,-16,88,38,NativeBtnStyle.Primary,Buy,20);
            _half = _theme.CreateButtonTopLeft(_root.transform,"half","退半",548,-16,78,38,NativeBtnStyle.Secondary,Half,20);
            _sell = _theme.CreateButtonTopLeft(_root.transform,"sell","清仓",634,-16,78,38,NativeBtnStyle.Secondary,Sell,20);
            _holding = _ui.Label(_root.transform,"holding","",new Vector2(724,-16),new Vector2(150,38),18);
            _theme.CreateButtonTopLeft(_root.transform,"overview","总览",880,-16,76,38,NativeBtnStyle.Secondary,Overview,20);
            _theme.CreateButtonTopLeft(_root.transform,"collapse","收",964,-16,40,38,NativeBtnStyle.Secondary,Collapse,18);
        }
        public void Update()
        {
            bool show = PredictionVisibility.CanShow() && !GameBridge.IsPlayingBattle();
            if (_root != null) _root.SetActive(show);
            if (!show) return;
            if (_root == null) Build(); if (_root == null) return;
            RoomState r = _mod.OnlinePanel.CurrentView(); string focus = FocusSelection.Uid(_mod.Snapshot);
            int index = -1; if (r != null) for(int i=0;i<8;i++) if(r.Uids[i]==focus) index=i;
            if(index<0) { _root.SetActive(false); return; }
            if (_uid != focus || _game != r.GameId) { _uid=focus;_game=r.GameId;_index=index;_selected=r.MyRanks[index]>0?r.MyRanks[index]:1;_stake.SetText("10"); }
            _name.text=r.Names[index];_rank.text=PredictionMod.N(_selected)+"名";
            _holding.text=r.Stakes[index]>0?"猜"+PredictionMod.N(r.MyRanks[index])+"名 · "+PredictionMod.N(r.Stakes[index])+"分":"未持仓";
            bool open=r.Open && r.Ranks[index]==0;
            _buy.SetInteractable(open);_half.SetInteractable(open&&r.Stakes[index]>0);_sell.SetInteractable(open&&r.Stakes[index]>0);
            _buy.SetText(r.Stakes[index]>0?(r.MyRanks[index]==_selected?"加注":"换猜"):"投入");
            var rt=_root.transform as RectTransform;var parent=rt.parent as RectTransform;
            float scale=Mathf.Min(1.15f,(parent.rect.width-32f)/1010f);rt.localScale=new Vector3(scale,scale,1);
            bool hand,talent;GameBridge.InspectBottomHandZone(out hand,out talent);
            rt.anchoredPosition=new Vector2(0f,hand?Mathf.Max(326f,GameBridge.BottomHandTop(parent)+14f):76f);
            _mod.OnlinePanel.KeepBarAbove(_root.transform);
        }
        void Minus(){_selected=_selected==1?8:_selected-1;}
        void Plus(){_selected=_selected==8?1:_selected+1;}
        bool Valid(){RoomState r=_mod.OnlinePanel.CurrentView();return r!=null&&r.GameId==_game&&_index>=0&&r.Uids[_index]==_uid&&FocusSelection.Uid(_mod.Snapshot)==_uid;}
        void Buy(){if(!Valid())return;long amount;if(!long.TryParse(_stake.Text,NumberStyles.None,CultureInfo.InvariantCulture,out amount)||amount<1||amount>10000){_mod.OnlinePanel.SetFeedback("投入须为1至10000整数");return;}_mod.OnlinePanel.Submit(_index,_selected,amount,0);}
        void Half(){if(Valid())_mod.OnlinePanel.Submit(_index,0,0,1);}
        void Sell(){if(Valid())_mod.OnlinePanel.Submit(_index,0,0,2);}
        void Overview(){_mod.OnlinePanel.Toggle();}
        void Collapse(){_collapsed=!_collapsed;for(int i=0;i<_root.transform.childCount;i++){Transform child=_root.transform.GetChild(i);if(child.name!="collapse")child.gameObject.SetActive(!_collapsed);}(_root.transform as RectTransform).sizeDelta=new Vector2(_collapsed?60f:1010f,70f); NativeTheme.SetTopLeft(_root.transform.Find("collapse") as RectTransform,_collapsed?10f:964f,-16f,40f,38f);}
        public void Destroy(){_ui.DestroyAll();_root=null;}
    }
}
#endif
