using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Yx.ModSdk.Unity
{
    /// <summary>The original prediction shell: native title bar, inner surface, headers and footer. No game/network state.</summary>
    public sealed class NativePredictionWindow
    {
        readonly UiKit _ui;
        bool _pressed, _dragged;
        Vector2 _down, _start;
        public readonly GameObject Root, Banner, Body, Table;
        public readonly RectTransform Rect;
        public readonly CanvasGroup BodyGroup;
        public readonly TextMeshProUGUI Title, Status, Footer;
        public NativePredictionWindow(ModContext ctx, NativePanelTheme theme, string title)
        {
            _ui = new UiKit(ctx);
            Root=_ui.Panel("PredictionOverviewWindow",new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(1140f,624f),NativePanelTheme.LacquerBlack);
            if(Root==null) return;
            Rect=Root.GetComponent<RectTransform>(); theme.ApplyWindowFrame(Root.GetComponent<Image>());
            Banner=_ui.Row(Root.transform,"TitleBanner",Vector2.zero,new Vector2(1140f,54f),Color.white);theme.ApplyBanner(Banner.GetComponent<Image>());
            Title=_ui.Label(Banner.transform,"TitleLabel",title,new Vector2(24f,-10f),new Vector2(210f,34f),22);
            theme.ApplyTextStyle(Title,22,true,TextAlignmentOptions.Left);Title.color=NativePanelTheme.AntiqueGold;
            Status=_ui.Label(Banner.transform,"StatusLabel","",new Vector2(240f,-4f),new Vector2(250f,46f),16);
            theme.ApplyTextStyle(Status,16,false,TextAlignmentOptions.Left);Status.enableWordWrapping=true;
            Body=_ui.Row(Root.transform,"BodyContainer",new Vector2(12f,-54f),new Vector2(1116f,560f),new Color(0.06f,0.08f,0.10f,0.98f));
            BodyGroup=Body.AddComponent<CanvasGroup>();
            Table=_ui.Row(Body.transform,"OverviewTab",Vector2.zero,new Vector2(1116f,528f),Color.clear);Table.GetComponent<Image>().raycastTarget=false;
            Header(theme,"玩家",16f,160f);Header(theme,"田忌马牌",186f,140f);Header(theme,"猜第几名",336f,120f);Header(theme,"倍率",466f,74f);Header(theme,"投入",550f,64f);Header(theme,"操作",624f,200f);Header(theme,"我的预测与返还",830f,260f);
            Footer=_ui.Label(Body.transform,"FooterLabel","",new Vector2(10f,-514f),new Vector2(1096f,44f),16);
            theme.ApplyTextStyle(Footer,16,false,TextAlignmentOptions.Center);Footer.enableWordWrapping=true;
        }
        void Header(NativePanelTheme theme,string text,float x,float width)
        {
            var label=_ui.Label(Table.transform,"ColumnHeader",text,new Vector2(x,-4f),new Vector2(width,26f),17);
            theme.ApplyTextStyle(label,17,true,TextAlignmentOptions.Left);label.color=NativePanelTheme.AntiqueGold;
        }
        /// <summary>Poll only on the Unity main thread. Header buttons never initiate a drag.</summary>
        public void UpdateDrag()
        {
            if(Root==null||!Root.activeInHierarchy)return;
            var parent=Rect.parent as RectTransform;if(parent==null)return;
            Canvas canvas=Root.GetComponentInParent<Canvas>();
            Camera camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            Vector3 mouse=Input.mousePosition;Vector2 local;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,new Vector2(mouse.x,mouse.y),camera,out local))return;
            Vector2 inBanner;
            if(Input.GetMouseButtonDown(0) && RectTransformUtility.ScreenPointToLocalPointInRectangle(Banner.transform as RectTransform,new Vector2(mouse.x,mouse.y),camera,out inBanner)
                && inBanner.x>=0f && inBanner.x<230f && inBanner.y<=0f && inBanner.y>=-54f)
            {_pressed=true;_dragged=false;_down=local;_start=Rect.anchoredPosition;}
            if(!_pressed)return;
            if((local-_down).sqrMagnitude>=36f)_dragged=true;
            if(_dragged)
            {
                Vector2 position=_start+local-_down;
                float halfW=Rect.rect.width*Rect.localScale.x/2f,halfH=Rect.rect.height*Rect.localScale.y/2f;
                position.x=Mathf.Clamp(position.x,parent.rect.xMin+halfW,parent.rect.xMax-halfW);
                position.y=Mathf.Clamp(position.y,parent.rect.yMin+halfH,parent.rect.yMax-halfH);
                Rect.anchoredPosition=position;
            }
            if(!Input.GetMouseButton(0))_pressed=false;
        }
        public void SetPeek(bool value) { if(Root==null)return;BodyGroup.alpha=value?0.18f:1f;BodyGroup.blocksRaycasts=!value;Root.GetComponent<Image>().raycastTarget=!value; }
        public void SetMinimized(bool value) { if(Root==null)return;Body.SetActive(!value);Rect.sizeDelta=new Vector2(1140f,value?56f:624f); }
        public void Destroy(){_ui.DestroyAll();}
    }
}
