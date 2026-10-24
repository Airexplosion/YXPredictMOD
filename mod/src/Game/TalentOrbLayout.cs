#if YX_GAME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace YxPrediction
{
    /// <summary>
    /// 将手牌区下方的仙命天赋圆标重构为围绕左侧人物角色头像的环绕法宝阵列（弧形排列）。
    /// 清空手牌中央底部区域，使下注条与手牌互不阻挡。
    /// </summary>
    public static class TalentOrbLayout
    {
        // 围绕头像右侧弧形参数（以屏幕左下角或头像中心为参照）
        const float CenterX = 165f;
        const float CenterY = 165f;
        const float OrbitRadius = 138f;
        const float StartAngleDeg = 75f;   // 顶部偏右
        const float EndAngleDeg = -25f;    // 底部偏右
        const float DegToRad = 0.0174532925f;

        static float _nextUpdate;

        static FieldInfo _oppTalentsField;
        static FieldInfo _readyLayerField;
        static FieldInfo _selfInfoField;
        static FieldInfo _selfTalentsField;

        public static void Update()
        {
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

            Vector2 center = new Vector2(CenterX, CenterY);
            if (opp.bpInfoItem != null && opp.bpInfoItem.transform != null)
            {
                var rt = opp.bpInfoItem.transform as RectTransform;
                if (rt != null && rt.parent != null)
                {
                    Vector3 worldPos = rt.TransformPoint(Vector3.zero);
                    for (int i = 0; i < talents.Count; i++)
                    {
                        var item = talents[i] as BattleTalentIconItem;
                        if (item != null && item.transform != null && item.transform.parent != null)
                        {
                            center = item.transform.parent.InverseTransformPoint(worldPos);
                            center.y += 20f;
                            break;
                        }
                    }
                }
            }

            ApplyArcLayout(talents, center);
        }

        static void RearrangeSelfTalents(BattlePanel bp)
        {
            if (_readyLayerField == null)
            {
                _readyLayerField = typeof(BattlePanel).GetField("m_ReadyLayer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            ReadyLayer ready = bp.readyLayer;
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

            Vector2 center = new Vector2(CenterX, CenterY);
            if (selfInfo.transform != null)
            {
                var rt = selfInfo.transform as RectTransform;
                if (rt != null)
                {
                    Vector3 worldPos = rt.TransformPoint(new Vector3(145f, 145f, 0f));
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i] as BattleTalentIconItem;
                        if (item != null && item.transform != null && item.transform.parent != null)
                        {
                            center = item.transform.parent.InverseTransformPoint(worldPos);
                            break;
                        }
                    }
                }
            }

            ApplyArcLayout(items, center);
        }

        static void ApplyArcLayout(IList items, Vector2 center)
        {
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

                float fraction = activeCount == 1 ? 0.5f : (float)index / (activeCount - 1);
                float angleDeg = Mathf.Lerp(StartAngleDeg, EndAngleDeg, fraction);
                float rad = angleDeg * DegToRad;

                float targetX = center.x + OrbitRadius * Mathf.Cos(rad);
                float targetY = center.y + OrbitRadius * Mathf.Sin(rad);

                var rect = item.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(targetX, targetY);
                    item.basePos = rect.localPosition;
                }

                index++;
            }
        }

        public static void Restore()
        {
        }
    }
}
#endif
