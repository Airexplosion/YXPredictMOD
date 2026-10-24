#if YX_GAME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
namespace YxPrediction
{
    public sealed class TalentOrbit
    {
        sealed class Slot { public BattleTalentIconItem Item; public Transform Parent; public Vector3 Local, Base, Scale; public GameObject Wrapper; public Transform Name; public bool NameActive; public int Id; public TextMeshProUGUI Caption; }
        readonly List<Slot> _slots=new List<Slot>();
        readonly UiKit _ui;
        float _next;
        string _focus;
        static readonly FieldInfo NameField=typeof(BattleTalentIconItem).GetField("m_NameLabel",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo Opp=typeof(OpponentInfoPanel).GetField("m_Talents",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo Ready=typeof(BattlePanel).GetField("m_ReadyLayer",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo Self=typeof(ReadyLayer).GetField("m_PlayerSelfInfoItem",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo Items=typeof(PlayerSelfInfoItem).GetField("m_TalentItems",BindingFlags.Instance|BindingFlags.NonPublic);
        public TalentOrbit(ModContext ctx){_ui=new UiKit(ctx);}
        public void Update()
        {
            if(!PredictionVisibility.CanShow()||GameBridge.IsPlayingBattle()){Restore();return;}
            if(Time.unscaledTime<_next)return;_next=Time.unscaledTime+0.2f;
            BattlePanel bp=GameBridge.GetBattlePanel();if(bp==null)return;
            string focus=BattleManager.Instance.currentSpectateId;
            if(_focus!=focus){Restore();_focus=focus;}
            var opp=bp.FindILRSubPanel<OpponentInfoPanel>();
            IList list=null;
            if(opp!=null&&opp.panel!=null&&opp.panel.isShow&&opp.panel.gameObject.activeInHierarchy)list=Opp.GetValue(opp) as IList;
            else {var ready=Ready.GetValue(bp) as ReadyLayer;var self=ready==null?null:Self.GetValue(ready) as PlayerSelfInfoItem;if(self!=null&&self.gameObject.activeInHierarchy)list=Items.GetValue(self) as IList;}
            if(list==null){Restore();return;}
            var canvas=Ui.FindRootCanvas();var root=canvas==null?null:canvas.transform as RectTransform;if(root==null)return;
            int[] counts=new int[7];
            for(int i=0;i<list.Count;i++)
            {
                var item=list[i] as BattleTalentIconItem;if(item==null||item.talentId<0||!item.gameObject.activeInHierarchy)continue;
                int level=(int)item.talentLevel;if(level<1||level>6)continue;int duplicate=counts[level]++;
                Slot saved=null;for(int j=0;j<_slots.Count;j++)if(object.ReferenceEquals(_slots[j].Item,item))saved=_slots[j];
                if(saved==null)
                {
                    saved=new Slot{Item=item,Parent=item.transform.parent,Local=item.transform.localPosition,Scale=item.transform.localScale,Base=item.basePos,Id=item.talentId};
                    saved.Wrapper=new GameObject("PredictionTalentOrbit",typeof(RectTransform));saved.Wrapper.transform.SetParent(saved.Parent,false); (saved.Wrapper.transform as RectTransform).sizeDelta=Vector2.zero;
                    item.transform.SetParent(saved.Wrapper.transform,true);
                    var nativeName=NameField.GetValue(item) as TextMeshProUGUI; saved.Name=nativeName==null?null:nativeName.transform;if(saved.Name!=null){saved.Name=saved.Name.parent;saved.NameActive=saved.Name.gameObject.activeSelf;}
                    saved.Caption=_ui.Label(saved.Wrapper.transform,"OrbitCaption","",new Vector2(-80f,-50f),new Vector2(160f,36f),20);
                    saved.Caption.enableWordWrapping=false; saved.Caption.alignment=TextAlignmentOptions.Center; saved.Caption.raycastTarget=false;
                    _slots.Add(saved);
                }
                // Native hover still scales the item itself; only the separate wrapper sets display size.
                float degrees=160f-(level-1)*48f+(duplicate%2==0?0f:12f);
                float radians=degrees*0.0174532925f;float radius=174f+duplicate*48f;
                Vector3 world=root.TransformPoint(new Vector3(root.rect.xMin+250f+Mathf.Cos(radians)*radius,root.rect.yMin+250f+Mathf.Sin(radians)*radius,0f));
                saved.Wrapper.transform.position=world;saved.Wrapper.transform.localScale=new Vector3(0.62f,0.62f,1f);
                item.transform.position=world;item.basePos=world;
                if(saved.Name!=null)saved.Name.gameObject.SetActive(false);
                var config=ConfigManager.GetTalentConfig(item.talentId);
                saved.Caption.text=config==null?"":config.GetName();
            }
        }
        void Restore()
        {
            for(int i=0;i<_slots.Count;i++)
            {
                Slot s=_slots[i];if(s.Item!=null&&s.Item.transform!=null&&s.Wrapper!=null&&s.Item.transform.parent==s.Wrapper.transform&&s.Parent!=null)
                {s.Item.transform.SetParent(s.Parent,true);s.Item.transform.localPosition=s.Local;s.Item.transform.localScale=s.Scale;s.Item.basePos=s.Base;if(s.Name!=null)s.Name.gameObject.SetActive(s.NameActive);}
                if(s.Wrapper!=null)UnityEngine.Object.Destroy(s.Wrapper);
            }
            _slots.Clear();_focus=null;
        }
        public void Destroy(){Restore();_ui.DestroyAll();}
    }
}
#endif
