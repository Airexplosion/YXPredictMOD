#if YX_GAME
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
namespace YxPrediction
{
    /// <summary>Apply the native collapsed realm slots only to the observer's opponent panel.</summary>
    public sealed class NativeTalentLayout
    {
        sealed class RectState
        {
            public readonly RectTransform Rect;
            readonly Vector2 _min, _max, _pivot, _position, _size;
            readonly Vector3 _scale;
            readonly Quaternion _rotation;
            public RectState(RectTransform rect)
            {
                Rect=rect;_min=rect.anchorMin;_max=rect.anchorMax;_pivot=rect.pivot;
                _position=rect.anchoredPosition;_size=rect.sizeDelta;_scale=rect.localScale;_rotation=rect.localRotation;
            }
            public void Restore()
            {
                if(Rect==null)return;
                Rect.anchorMin=_min;Rect.anchorMax=_max;Rect.pivot=_pivot;Rect.anchoredPosition=_position;
                Rect.sizeDelta=_size;Rect.localScale=_scale;Rect.localRotation=_rotation;
            }
        }
        sealed class Slot
        {
            public BattleTalentIconItem Item;
            public RectState Rect;
            public Vector3 BaseScale, BasePosition;
            public bool Zoomed;
        }
        static readonly FieldInfo ZoomField=typeof(BattleTalentIconItem).GetField("m_IsZoom",BindingFlags.Instance|BindingFlags.NonPublic);
        readonly Slot[] _slots=new Slot[NativeTalentSlots.Count];
        RectState _root;
        LayoutGroup _group;
        bool _groupEnabled;
        public void Apply(IList items)
        {
            if(items==null||items.Count==0){Restore();return;}
            // Never wrap/reparent native icons: the game owns these controls and their hover logic.
            BattleTalentIconItem first=items[0] as BattleTalentIconItem;
            var root=first==null||first.transform==null?null:first.transform.parent as RectTransform;
            if(root==null){Restore();return;}
            bool changed=_root==null||_root.Rect!=root;
            for(int i=0;i<_slots.Length&&!changed;i++)
            {
                var item=i<items.Count?items[i] as BattleTalentIconItem:null;
                if((_slots[i]==null?null:_slots[i].Item)!=item)changed=true;
            }
            if(changed)
            {
                Restore();_root=new RectState(root);
                _group=root.GetComponent(typeof(LayoutGroup)) as LayoutGroup;
                if(_group!=null){_groupEnabled=_group.enabled;_group.enabled=false;}
                for(int i=0;i<items.Count&&i<_slots.Length;i++)
                {
                    var item=items[i] as BattleTalentIconItem;
                    var rect=item==null?null:item.transform as RectTransform;
                    if(rect==null||rect.parent!=root)continue;
                    _slots[i]=new Slot{Item=item,Rect=new RectState(rect),BaseScale=item.baseScale,BasePosition=item.basePos,Zoomed=ZoomField!=null&&(bool)ZoomField.GetValue(item)};
                }
            }
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.zero;root.pivot=new Vector2(0.5f,0.5f);
            root.anchoredPosition=new Vector2(NativeTalentSlots.CenterX,NativeTalentSlots.CenterY);
            root.sizeDelta=new Vector2(NativeTalentSlots.ContainerSize,NativeTalentSlots.ContainerSize);
            root.localScale=Vector3.one;root.localRotation=Quaternion.identity;
            for(int i=0;i<_slots.Length;i++)
            {
                Slot slot=_slots[i];if(slot==null||slot.Rect.Rect==null)continue;
                var rect=slot.Rect.Rect;
                if(rect.parent!=root)continue;
                float scale=NativeTalentSlots.Scale(i);
                slot.Item.baseScale=new Vector3(scale,scale,1f);
                slot.Item.Zoom(false);
                rect.anchorMin=new Vector2(0.5f,0.5f);rect.anchorMax=rect.anchorMin;rect.pivot=rect.anchorMin;
                rect.anchoredPosition=new Vector2(NativeTalentSlots.X(i),NativeTalentSlots.Y(i));
                rect.sizeDelta=new Vector2(NativeTalentSlots.IconSize,NativeTalentSlots.IconSize);
                rect.localScale=new Vector3(scale,scale,1f);rect.localRotation=Quaternion.identity;
                slot.Item.basePos=rect.position;
            }
        }
        public void Restore()
        {
            for(int i=0;i<_slots.Length;i++)
            {
                Slot slot=_slots[i];
                if(slot!=null&&slot.Rect.Rect!=null&&_root!=null&&slot.Rect.Rect.parent==_root.Rect)
                {
                    slot.Item.baseScale=slot.BaseScale;slot.Item.Zoom(slot.Zoomed);slot.Rect.Restore();slot.Item.basePos=slot.BasePosition;
                }
                _slots[i]=null;
            }
            if(_root!=null)_root.Restore();
            if(_group!=null)_group.enabled=_groupEnabled;
            _root=null;_group=null;
        }
    }
    public sealed class TalentOrbit
    {
        static readonly FieldInfo Opp=typeof(OpponentInfoPanel).GetField("m_Talents",BindingFlags.Instance|BindingFlags.NonPublic);
        readonly ModContext _ctx;
        readonly NativeTalentLayout _layout=new NativeTalentLayout();
        float _next;
        string _lastError;
        public TalentOrbit(ModContext ctx){_ctx=ctx;}
        public void Update()
        {
            if(!PredictionVisibility.CanShow()||GameBridge.IsPlayingBattle()){_layout.Restore();return;}
            if(Time.unscaledTime<_next)return;_next=Time.unscaledTime+0.2f;
            try
            {
                var bp=GameBridge.GetBattlePanel();
                var opp=bp==null?null:bp.FindILRSubPanel<OpponentInfoPanel>();
                if(opp==null||opp.panel==null||!opp.panel.isShow||!opp.panel.gameObject.activeInHierarchy){_layout.Restore();return;}
                _layout.Apply(Opp==null?null:Opp.GetValue(opp) as IList);_lastError=null;
            }
            catch(Exception ex)
            {
                _layout.Restore();string message=ex.Message;
                if(message!=_lastError){_lastError=message;_ctx.Log.Warn("原生仙命布局暂不可用："+message);}
            }
        }
        public void Destroy(){_layout.Restore();}
    }
}
#endif
