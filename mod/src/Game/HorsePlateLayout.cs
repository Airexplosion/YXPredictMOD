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

        // 头像、ID及称号之间的空白也属于同一个矩形热区；不新增接收射线的控件。
        public static Rect HoverBounds(RectTransform avatar, RectTransform name, RectTransform caption, Rect captionGlyph)
        {
            Rect rect = avatar.rect;
            if (rect.width <= 0 || rect.height <= 0) return new Rect();
            Rect area = Include(rect, avatar, name, name.rect);
            area = Include(area, avatar, caption, captionGlyph);
            // 额外覆盖命元角标及文字下方留白，按头像大小缩放。
            float x = rect.width * 0.08f, y = rect.height * 0.16f;
            return new Rect(area.x - x, area.y - y, area.width + x * 2, area.height + y * 2);
        }

        static Rect Include(Rect bounds, RectTransform reference, RectTransform part, Rect region)
        {
            float left = bounds.x, bottom = bounds.y, right = bounds.x + bounds.width, top = bounds.y + bounds.height;
            for (int i = 0; i < 4; i++)
            {
                Vector3 point = reference.InverseTransformPoint(part.TransformPoint(new Vector3(
                    region.x + (i % 2) * region.width, region.y + (i / 2) * region.height, 0)));
                left = System.Math.Min(left, point.x); right = System.Math.Max(right, point.x);
                bottom = System.Math.Min(bottom, point.y); top = System.Math.Max(top, point.y);
            }
            return new Rect(left, bottom, right - left, top - bottom);
        }

        public static bool ContainsPoint(Rect area, Vector2 point)
        {
            return area.width > 0 && area.height > 0 && point.x >= area.x && point.x <= area.x + area.width
                && point.y >= area.y && point.y <= area.y + area.height;
        }

        public static float HoverDistance(RectTransform avatar, Rect area, Vector2 point)
        {
            // 相邻列热区重叠时，只选离鼠标最近的一列。
            return avatar.rect.width > 0 && ContainsPoint(area, point)
                ? System.Math.Abs(point.x - avatar.rect.center.x) / avatar.rect.width : float.PositiveInfinity;
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
