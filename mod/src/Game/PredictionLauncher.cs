#if YX_GAME
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Game;
using Yx.ModSdk.Unity;

namespace YxPrediction
{
    // SDK热更类不继承MonoBehaviour/IDragHandler；在主线程轮询指针来区分点击和拖动。
    public sealed class PredictionLauncher
    {
        const float Width = 140f;
        const float Height = 44f;
        const float DragThreshold = 6f;
        readonly UiKit _ui;
        readonly ModContext _ctx;
        readonly PredictionPanel _panel;
        readonly NativeTheme _theme;
        readonly LauncherPosition _position;
        GameObject _root;
        RectTransform _rect;
        RectTransform _parent;
        Canvas _canvas;
        UiButton _button;
        bool _pressed;
        bool _dragged;
        Vector2 _downScreen;
        Vector2 _downLocal;
        double _startX;
        double _startY;
        int _enableAfterFrame = -1;
        float _nextBuild;

        public PredictionLauncher(ModContext ctx, PredictionPanel panel, NativeTheme theme)
        {
            _ctx = ctx;
            _panel = panel;
            _theme = theme;
            _ui = new UiKit(ctx);
            _position = new LauncherPosition(ctx.Data.Get<System.Collections.Generic.Dictionary<string, object>>(LauncherPosition.SaveKey, null));
        }

        public void Update()
        {
            bool inMatch = BattleManager.Instance != null && SceneLoader.currentSceneName == "Battle";
            if (!inMatch)
            {
                FinishGesture();
                if (_root != null) _root.SetActive(false);
                _panel.Hide();
                return;
            }
            if (GameBridge.IsPlayingBattle())
            {
                FinishGesture();
                if (_root != null) _root.SetActive(false);
                return;
            }
            if (_root == null)
            {
                if (Time.unscaledTime < _nextBuild) return;
                _nextBuild = Time.unscaledTime + 0.5f;
                Build();
            }
            if (_root == null || _rect == null || _parent == null) return;
            _root.SetActive(true);
            Place(_position.X, _position.Y);
            if (_enableAfterFrame >= 0 && Time.frameCount > _enableAfterFrame)
            {
                _enableAfterFrame = -1;
                _button.SetInteractable(true);
            }
            Vector3 mouse = Input.mousePosition;
            var screen = new Vector2(mouse.x, mouse.y);
            Camera camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, screen, camera, out local)) return;
            if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(_rect, screen, camera))
            {
                _pressed = true; _dragged = false;
                _downScreen = screen; _downLocal = local;
                _startX = _position.X; _startY = _position.Y;
            }
            if (!_pressed) return;
            if ((screen - _downScreen).sqrMagnitude >= DragThreshold * DragThreshold) _dragged = true;
            if (_dragged)
            {
                // 禁用到释放后的下一帧，阻止EventSystem把拖动结束当作按钮点击。
                _button.SetInteractable(false);
                Rect bounds = _parent.rect;
                if (bounds.width > 0 && bounds.height > 0)
                    Place(_startX + (local.x - _downLocal.x) / bounds.width, _startY + (local.y - _downLocal.y) / bounds.height);
            }
            if (!Input.GetMouseButton(0)) FinishGesture();
        }

        void Build()
        {
            var anchor = new Vector2((float)_position.X, (float)_position.Y);
            var center = new Vector2(0.5f, 0.5f);
            _root = _ui.Panel("PredictionLauncher", anchor, center, Vector2.zero, new Vector2(Width, Height), new Color(0.08f, 0.09f, 0.12f, 0.96f), true);
            if (_root == null) return;
            _rect = (RectTransform)_root.GetComponent(typeof(RectTransform));
            _parent = _root.transform.parent as RectTransform;
            _canvas = Ui.FindRootCanvas();

            var bgImg = _root.GetComponent(typeof(Image)) as Image;
            if (bgImg != null) _theme.ApplyWindowFrame(bgImg);

            _button = _ui.TextButton(_root.transform, "openPrediction", "预测总览", new Vector2(2f, -2f), new Vector2(Width - 4f, Height - 4f), Click);
            if (_button != null && _button.GameObject != null)
            {
                var btnImg = _button.GameObject.GetComponent<Image>();
                if (btnImg != null && _theme.BannerSprite != null)
                {
                    btnImg.sprite = _theme.BannerSprite;
                    btnImg.type = Image.Type.Sliced;
                }
                var label = _button.GameObject.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    _theme.ApplyTextStyle(label, 16, true, TextAlignmentOptions.Center);
                    label.color = NativeTheme.AntiqueGold;
                }
            }

            Place(_position.X, _position.Y);
        }

        void Place(double x, double y)
        {
            if (_parent == null || _rect == null) return;
            Rect bounds = _parent.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            _position.Move(x, y, Width / bounds.width, Height / bounds.height);
            var anchor = new Vector2((float)_position.X, (float)_position.Y);
            _rect.anchorMin = anchor; _rect.anchorMax = anchor;
            _rect.anchoredPosition = Vector2.zero;
        }

        void Click()
        {
            if (GameBridge.IsPlayingBattle()) return;
            Vector3 mouse = Input.mousePosition;
            var screen = new Vector2(mouse.x, mouse.y);
            // 按钮的PointerUp可能早于本模组本帧Update，仍检查完整位移。
            if (_dragged || (_pressed && (screen - _downScreen).sqrMagnitude >= DragThreshold * DragThreshold) || _enableAfterFrame >= 0) return;
            _panel.Toggle();
            if (_root != null) _root.transform.SetAsLastSibling();
        }

        void FinishGesture()
        {
            if (!_pressed) return;
            bool moved = _dragged;
            _pressed = false; _dragged = false;
            if (!moved) return;
            _enableAfterFrame = Time.frameCount;
            _ctx.Data.Set(LauncherPosition.SaveKey, _position.Write());
        }

        public void Destroy()
        {
            FinishGesture();
            _ui.DestroyAll(); _root = null;
        }
    }
}
#endif
