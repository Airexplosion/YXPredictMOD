// 只模拟本工具已核对的SDK Label双层结构，供生产描边控件的构建/同步回归。
namespace Yx.ModSdk.Unity
{
    public sealed class UiKit
    {
        public readonly System.Collections.Generic.List<TMPro.TextMeshProUGUI> Labels = new System.Collections.Generic.List<TMPro.TextMeshProUGUI>();
        public TMPro.TextMeshProUGUI Label(UnityEngine.Transform parent, string name, string value,
            UnityEngine.Vector2 position, UnityEngine.Vector2 size, int fontSize)
        {
            var outer = new UnityEngine.RectTransform { parent = parent,
                anchorMin = new UnityEngine.Vector2(0, 1), anchorMax = new UnityEngine.Vector2(0, 1),
                pivot = new UnityEngine.Vector2(0, 1), anchoredPosition = position, sizeDelta = size };
            var label = new TMPro.TextMeshProUGUI { text = value, fontSize = fontSize,
                rectTransform = new UnityEngine.RectTransform { parent = outer,
                    anchorMin = new UnityEngine.Vector2(0, 0), anchorMax = new UnityEngine.Vector2(1, 1),
                    pivot = new UnityEngine.Vector2(0.5f, 0.5f), sizeDelta = new UnityEngine.Vector2(-12, -4) } };
            Labels.Add(label);
            return label;
        }
    }
}
