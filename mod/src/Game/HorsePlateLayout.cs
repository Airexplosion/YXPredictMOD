#if YX_GAME
using UnityEngine;

namespace YxPrediction
{
    public static class HorsePlateLayout
    {
        // 图标只覆盖头像本体，原头像的Sprite及原生控件层级保持不变。
        public static void PlaceIcon(RectTransform icon, RectTransform avatar)
        {
            if (icon.parent != avatar) icon.SetParent(avatar, false);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            float size = System.Math.Min(avatar.rect.width, avatar.rect.height);
            icon.sizeDelta = new Vector2(size, size);
            icon.localScale = new Vector3(1, 1, 1);
            icon.anchoredPosition = Vector2.zero;
            if (icon.GetSiblingIndex() != avatar.childCount - 1) icon.SetAsLastSibling();
        }

        // 使用裁切圆而非包围方框判定悬停，缩放由原生屏幕坐标转换负责。
        public static bool ContainsPoint(RectTransform avatar, Vector2 point)
        {
            Rect rect = avatar.rect;
            float size = System.Math.Min(rect.width, rect.height);
            if (size <= 0) return false;
            float x = (point.x - rect.center.x) * 2 / size;
            float y = (point.y - rect.center.y) * 2 / size;
            return x * x + y * y <= 1;
        }

        public static bool ShouldShow(bool hasImage, bool avatarVisible, bool iconsOnFocus, string uid, string focusedUid, bool hovered)
        {
            if (!hasImage || !avatarVisible || string.IsNullOrEmpty(uid)) return false;
            bool viewing = hovered || (!string.IsNullOrEmpty(focusedUid) && uid == focusedUid);
            return iconsOnFocus ? viewing : !viewing;
        }
    }
}
#endif
