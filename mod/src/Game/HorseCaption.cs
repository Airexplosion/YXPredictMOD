#if YX_GAME
using System.Globalization;
using TMPro;
using UnityEngine;
using Yx.ModSdk.Unity;

namespace YxPrediction
{
    // SDK的默认字体/中文回退材质未必显示outlineWidth，使用黑色字形底层形成实边。
    public sealed class HorseCaption
    {
        public const float StrokeSize = 1.5f;
        public const float GapBelowName = StrokeSize + 1;
        readonly TextMeshProUGUI[] _edges = new TextMeshProUGUI[8];
        public TextMeshProUGUI Text { get; private set; }

        public HorseCaption(UiKit ui, Transform parent)
        {
            int index = 0;
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    if (x == 0 && y == 0) continue;
                    TextMeshProUGUI edge = Make(ui, parent, "captionBlackEdge" + index.ToString(CultureInfo.InvariantCulture), new Vector2(x * StrokeSize, y * StrokeSize));
                    edge.color = new Color(0, 0, 0, 1);
                    _edges[index++] = edge;
                }
            // 最后创建彩色正文，使八个黑色字形始终位于正文背后。
            Text = Make(ui, parent, "caption", Vector2.zero);
        }

        static TextMeshProUGUI Make(UiKit ui, Transform parent, string name, Vector2 offset)
        {
            var label = ui.Label(parent, name, "", offset, new Vector2(150, 48), 22);
            label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Top;
            label.richText = false; label.raycastTarget = false; label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        public void Set(string value, Color color)
        {
            if (Text.text != value)
            {
                Text.text = value;
                for (int i = 0; i < _edges.Length; i++) _edges[i].text = value;
            }
            Text.color = color;
        }
    }
}
#endif
