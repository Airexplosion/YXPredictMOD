#if YX_GAME
using TMPro;
using UnityEngine;

namespace YxPrediction
{
    // UiKit.Label返回外层布局框内部的伸展TMP文本，位置/尺寸必须应用在外框上。
    public static class UiTextBox
    {
        public static RectTransform Container(TextMeshProUGUI text)
        { return text.rectTransform.parent as RectTransform; }

        public static void Place(TextMeshProUGUI text, float x, float y, float width, float height)
        {
            RectTransform box = Container(text);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0, 1);
            box.anchoredPosition = new Vector2(x, y);
            box.sizeDelta = new Vector2(width, height);
        }

        public static Vector3 PointIn(Transform reference, TextMeshProUGUI text, Vector3 localPoint)
        { return reference.InverseTransformPoint(text.rectTransform.TransformPoint(localPoint)); }

        public static void BringToFront(TextMeshProUGUI text) { Container(text).SetAsLastSibling(); }
    }
}
#endif
