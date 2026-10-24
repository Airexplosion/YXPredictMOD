// 仅用于布局回归的二维UGUI锚点模型；不模拟字体渲染或真实客户端。
namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public class Transform
    {
        Transform _parent;
        readonly System.Collections.Generic.List<Transform> _children = new System.Collections.Generic.List<Transform>();
        public Transform parent { get { return _parent; } set { SetParent(value, false); } }
        public int childCount { get { return _children.Count; } }
        public Transform GetChild(int index) { return _children[index]; }
        public void SetParent(Transform value, bool worldPositionStays)
        {
            if (_parent == value) return;
            if (_parent != null) _parent._children.Remove(this);
            _parent = value;
            if (_parent != null) _parent._children.Add(this);
        }
        public int GetSiblingIndex() { return _parent == null ? 0 : _parent._children.IndexOf(this); }
        public void SetSiblingIndex(int index)
        {
            if (_parent == null) return;
            var siblings = _parent._children;
            siblings.Remove(this);
            siblings.Insert(System.Math.Max(0, System.Math.Min(index, siblings.Count)), this);
        }
        public Vector3 localScale = new Vector3(1, 1, 1);
        public bool Raised;
        public void SetAsLastSibling() { Raised = true; if (_parent != null) SetSiblingIndex(_parent.childCount - 1); }
        internal virtual Vector3 LocalPosition() { return new Vector3(); }
        public Vector3 TransformPoint(Vector3 point)
        {
            Vector3 pos = LocalPosition();
            var result = new Vector3(pos.x + point.x * localScale.x, pos.y + point.y * localScale.y, pos.z + point.z * localScale.z);
            return parent == null ? result : parent.TransformPoint(result);
        }
        public Vector3 InverseTransformPoint(Vector3 point)
        {
            Vector3 p = parent == null ? point : parent.InverseTransformPoint(point);
            Vector3 pos = LocalPosition();
            return new Vector3((p.x - pos.x) / localScale.x, (p.y - pos.y) / localScale.y, (p.z - pos.z) / localScale.z);
        }
    }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
        public float Width { get { var p = parent as RectTransform; return sizeDelta.x + (p == null ? 0 : p.Width * (anchorMax.x - anchorMin.x)); } }
        public float Height { get { var p = parent as RectTransform; return sizeDelta.y + (p == null ? 0 : p.Height * (anchorMax.y - anchorMin.y)); } }
        internal override Vector3 LocalPosition()
        {
            var p = parent as RectTransform;
            if (p == null) return new Vector3(anchoredPosition.x, anchoredPosition.y, 0);
            float ax = anchorMin.x + (anchorMax.x - anchorMin.x) * pivot.x;
            float ay = anchorMin.y + (anchorMax.y - anchorMin.y) * pivot.y;
            return new Vector3((ax - p.pivot.x) * p.Width + anchoredPosition.x, (ay - p.pivot.y) * p.Height + anchoredPosition.y, 0);
        }
        public Vector3 TopLeft() { return TransformPoint(new Vector3(-pivot.x * Width, (1 - pivot.y) * Height, 0)); }
    }
}
namespace TMPro
{
    public sealed class TextMeshProUGUI
    {
        public UnityEngine.RectTransform rectTransform;
    }
}
