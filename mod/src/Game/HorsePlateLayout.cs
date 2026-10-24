#if YX_GAME
using UnityEngine;

namespace YxPrediction
{
    public static class HorsePlateLayout
    {
        public const float IconSize = 34f;

        public static void PlaceBadge(RectTransform icon, RectTransform avatar)
        {
            if (icon == null || avatar == null) return;
            if (icon.parent != avatar) icon.SetParent(avatar, false);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 1f);
            icon.pivot = new Vector2(0.15f, 0.85f);
            icon.anchoredPosition = new Vector2(-2f, 2f);
            icon.sizeDelta = new Vector2(IconSize, IconSize);
            icon.localScale = Vector3.one;

            // 确保绘制在头像上层
            icon.SetAsLastSibling();
        }

        public static void PlaceIcon(RectTransform icon, RectTransform name, float centerX, float bottomY)
        {
            if (icon == null || name == null) return;
            Transform parent = name.parent;
            if (parent == null) return;
            if (icon.parent != parent) icon.SetParent(parent, false);
            var parentRect = parent as RectTransform;
            icon.anchorMin = icon.anchorMax = parentRect == null ? new Vector2(0.5f, 0.5f) : parentRect.pivot;
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(IconSize, IconSize);
            icon.localScale = Vector3.one;
            Vector3 point = parent.InverseTransformPoint(name.TransformPoint(new Vector3(centerX, bottomY, 0)));
            icon.anchoredPosition = new Vector2(point.x, point.y);
        }
    }
}
#endif
