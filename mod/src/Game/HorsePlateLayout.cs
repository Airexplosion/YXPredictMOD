#if YX_GAME
using UnityEngine;

namespace YxPrediction
{
    public static class HorsePlateLayout
    {
        public const float IconSize = 88;

        // 昵称自身的Graphic先于其子节点绘制，故图标必须放在昵称之前的同级节点。
        // 称号仍是昵称的子节点；不移动或复制游戏的原生昵称。
        public static void PlaceIcon(RectTransform icon, RectTransform name, float centerX, float bottomY)
        {
            Transform parent = name.parent;
            if (parent == null) return;
            if (icon.parent != parent) icon.SetParent(parent, false);
            var parentRect = parent as RectTransform;
            icon.anchorMin = icon.anchorMax = parentRect == null ? new Vector2(0.5f, 0.5f) : parentRect.pivot;
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(IconSize, IconSize);
            icon.localScale = new Vector3(1, 1, 1);
            Vector3 point = parent.InverseTransformPoint(name.TransformPoint(new Vector3(centerX, bottomY, 0)));
            icon.anchoredPosition = new Vector2(point.x, point.y);

            // 已在前面时须减去自身占的一个位置，否则重复刷新会与昵称互换前后。
            int nameIndex = name.GetSiblingIndex(), iconIndex = icon.GetSiblingIndex();
            int target = iconIndex < nameIndex ? nameIndex - 1 : nameIndex;
            if (iconIndex != target) icon.SetSiblingIndex(target);
        }
    }
}
#endif
