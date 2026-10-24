#if YX_GAME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Yx.ModSdk.Unity;

namespace YxPrediction
{
    /// <summary>
    /// 将仙命图标放到左下人物旁的紧凑双列，避免误以顶栏头像为布局中心。
    /// 清空手牌中央底部区域，使下注条与手牌互不阻挡。
    /// </summary>
    public static class TalentOrbLayout
    {
        sealed class Saved
        {
            public BattleTalentIconItem Item;
            public Transform Parent;
            public Vector3 Position, BasePosition, Applied;
            public int Talent;
        }
        static readonly List<Saved> _saved = new List<Saved>();
        static float _nextUpdate;

        static FieldInfo _oppTalentsField;
        static FieldInfo _readyLayerField;
        static FieldInfo _selfInfoField;
        static FieldInfo _selfTalentsField;

        public static void Update()
        {
            if (!PredictionVisibility.CanShow() || GameBridge.IsPlayingBattle()) { Restore(); return; }
            if (Time.unscaledTime < _nextUpdate) return;
            _nextUpdate = Time.unscaledTime + 0.15f;

            try
            {
                RearrangeTalents();
            }
            catch
            {
            }
        }

        static void RearrangeTalents()
        {
            BattlePanel bp = GameBridge.GetBattlePanel();
            if (bp == null) return;

            // 1. 尝试重排观战/对手面板 (OpponentInfoPanel) 仙命
            RearrangeOpponentTalents(bp);

            // 2. 尝试重排自身玩家 (PlayerSelfInfoItem) 仙命
            RearrangeSelfTalents(bp);
        }

        static void RearrangeOpponentTalents(BattlePanel bp)
        {
            OpponentInfoPanel opp = bp.FindILRSubPanel<OpponentInfoPanel>();
            if (opp == null || opp.panel == null || !opp.panel.isShow || !opp.panel.gameObject.activeInHierarchy) return;

            if (_oppTalentsField == null)
            {
                _oppTalentsField = typeof(OpponentInfoPanel).GetField("m_Talents", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (_oppTalentsField == null) return;

            var talents = _oppTalentsField.GetValue(opp) as IList;
            if (talents == null || talents.Count == 0) return;

            ApplyCompactLayout(talents);
        }

        static void RearrangeSelfTalents(BattlePanel bp)
        {
            if (_readyLayerField == null)
            {
                _readyLayerField = typeof(BattlePanel).GetField("m_ReadyLayer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            ReadyLayer ready = _readyLayerField == null ? null : _readyLayerField.GetValue(bp) as ReadyLayer;
            if (ready == null && _readyLayerField != null)
            {
                ready = _readyLayerField.GetValue(bp) as ReadyLayer;
            }
            if (ready == null) return;

            if (_selfInfoField == null)
            {
                _selfInfoField = typeof(ReadyLayer).GetField("m_PlayerSelfInfoItem", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (_selfInfoField == null) return;

            var selfInfo = _selfInfoField.GetValue(ready) as PlayerSelfInfoItem;
            if (selfInfo == null || selfInfo.gameObject == null || !selfInfo.gameObject.activeInHierarchy) return;

            if (_selfTalentsField == null)
            {
                _selfTalentsField = typeof(PlayerSelfInfoItem).GetField("m_TalentItems", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (_selfTalentsField == null) return;

            var items = _selfTalentsField.GetValue(selfInfo) as IList;
            if (items == null || items.Count == 0) return;

            ApplyCompactLayout(items);
        }

        static void ApplyCompactLayout(IList items)
        {
            Canvas canvas = Ui.FindRootCanvas();
            var canvasRect = canvas == null ? null : canvas.transform as RectTransform;
            if (canvasRect == null) return;
            int activeCount = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i] as BattleTalentIconItem;
                if (item != null && item.gameObject != null && item.gameObject.activeSelf)
                {
                    activeCount++;
                }
            }
            if (activeCount == 0) return;

            int index = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i] as BattleTalentIconItem;
                if (item == null || item.gameObject == null || !item.gameObject.activeSelf) continue;

                // Two short columns beside the lower-left portrait. 8 icons stay within
                // x=235..309/y=108..330 of the canvas, away from the centered hand area.
                float targetX = canvasRect.rect.xMin + 235f + (index % 2) * 74f;
                float targetY = canvasRect.rect.yMin + 330f - (index / 2) * 74f;

                var rect = item.transform as RectTransform;
                if (rect != null)
                {
                    Saved saved = null;
                    for (int j = 0; j < _saved.Count; j++) if (object.ReferenceEquals(_saved[j].Item, item)) saved = _saved[j];
                    if (saved == null)
                    {
                        saved = new Saved { Item = item };
                        _saved.Add(saved);
                        saved.Position = rect.localPosition; saved.BasePosition = item.basePos;
                    }
                    else if (saved.Parent != rect.parent || (rect.localPosition - saved.Applied).sqrMagnitude > 1f)
                    {
                        saved.Position = rect.localPosition; saved.BasePosition = item.basePos;
                    }
                    saved.Parent = rect.parent; saved.Talent = item.talentId;
                    // Convert parent-local coordinates to world space, independently of the child's anchors.
                    rect.position = canvasRect.TransformPoint(new Vector3(targetX, targetY, 0f));
                    saved.Applied = rect.localPosition;
                    item.basePos = rect.localPosition;
                }

                index++;
            }
        }

        public static void Restore()
        {
            for (int i = 0; i < _saved.Count; i++)
            {
                Saved saved = _saved[i];
                if (saved.Item == null || saved.Item.transform == null) continue;
                Transform rect = saved.Item.transform;
                if (rect.parent != saved.Parent || saved.Item.talentId != saved.Talent || (rect.localPosition - saved.Applied).sqrMagnitude > 1f) continue;
                rect.localPosition = saved.Position;
                saved.Item.basePos = saved.BasePosition;
            }
            _saved.Clear();
        }
    }
}
#endif
