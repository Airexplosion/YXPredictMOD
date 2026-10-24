#if YX_GAME
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Game;
using Yx.ModSdk.Unity;

namespace YxPrediction
{
    public enum NativeBtnStyle
    {
        Primary,   // 金色主操作（下注/加注/确定）
        Secondary, // 暗玉/岩石次操作（走势/展开/简洁）
        Caution,   // 铜金警告操作（退半）
        Danger,    // 绯红危险操作（清仓）
        Pill,      // 紧凑胶囊（名次加减）
        Close      // 关闭按钮（×）
    }

    public sealed class NativeButton
    {
        public readonly GameObject GameObject;
        public readonly Button Button;
        public readonly TextMeshProUGUI Label;
        public readonly Image Background;
        readonly UnityAction _action;

        public NativeButton(GameObject root, Button btn, TextMeshProUGUI label, Image bg, Action onClick, ModContext ctx)
        {
            GameObject = root;
            Button = btn;
            Label = label;
            Background = bg;
            if (btn != null && onClick != null)
            {
                btn.onClick = new Button.ButtonClickedEvent();
                _action = new UnityAction(ctx.Guarded("NativeButton.OnClick", onClick));
                btn.onClick.AddListener(_action);
            }
        }

        public void SetText(string text)
        {
            if (Label != null) Label.text = text;
        }

        public void SetInteractable(bool interactable)
        {
            if (Button != null) Button.interactable = interactable;
        }

        public void SetActive(bool active)
        {
            if (GameObject != null) GameObject.SetActive(active);
        }
    }

    public sealed class NativeTheme
    {
        readonly ModContext _ctx;
        bool _harvested;

        Sprite _windowSprite;
        Sprite _bannerSprite;
        Sprite _insetImage;
        Sprite _selfFlagSprite;
        Sprite _lineSprite;
        Sprite _closeButtonSprite;
        Sprite _primaryBtnSprite;
        Sprite _secondaryBtnSprite;

        TMP_FontAsset _nativeFont;
        Material _nativeFontMaterial;

        // 典雅修仙墨玉与鎏金配色保底
        public static readonly Color LacquerBlack = new Color(0.08f, 0.09f, 0.12f, 0.96f);
        public static readonly Color RecessedJade = new Color(0.05f, 0.08f, 0.09f, 0.94f);
        public static readonly Color AntiqueGold = new Color(0.85f, 0.72f, 0.42f, 1f);
        public static readonly Color SoftGoldGlow = new Color(0.85f, 0.70f, 0.30f, 0.28f);
        public static readonly Color DividerBronze = new Color(0.55f, 0.48f, 0.36f, 0.45f);
        public static readonly Color TextParchment = new Color(0.92f, 0.88f, 0.80f, 1f);
        public static readonly Color TextMuted = new Color(0.68f, 0.65f, 0.58f, 1f);

        public Sprite WindowSprite { get { Harvest(); return _windowSprite; } }
        public Sprite BannerSprite { get { Harvest(); return _bannerSprite; } }
        public Sprite InsetSprite { get { Harvest(); return _insetImage; } }
        public Sprite SelfFlagSprite { get { Harvest(); return _selfFlagSprite; } }
        public Sprite LineSprite { get { Harvest(); return _lineSprite; } }
        public TMP_FontAsset Font { get { Harvest(); return _nativeFont; } }
        public Material FontMaterial { get { Harvest(); return _nativeFontMaterial; } }

        public NativeTheme(ModContext ctx)
        {
            _ctx = ctx;
        }

        public void Harvest()
        {
            if (_harvested) return;
            try
            {
                BattleManager manager = BattleManager.Instance;
                if (manager == null) return;
                FieldInfo fi = manager.GetType().GetField("m_BattlePanel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                BattlePanel bp = fi == null ? null : fi.GetValue(manager) as BattlePanel;
                if (bp == null) return;

                // 安全遍历 BattlePanel 下的原生预制体组件，不激活任何未初始化的 Panel
                Transform[] transforms = bp.transform.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    string name = t.name;

                    if (_windowSprite == null && name == "Window" && t.parent != null && t.parent.name == "ScoreboardPanel")
                    {
                        var img = t.GetComponent<Image>();
                        if (img != null && img.sprite != null) _windowSprite = img.sprite;
                    }
                    else if (_bannerSprite == null && (name == "TtitleBG" || name == "TitleBG"))
                    {
                        var img = t.GetComponent<Image>();
                        if (img != null && img.sprite != null) _bannerSprite = img.sprite;
                    }
                    else if (_insetImage == null && name == "BattleInfoContainer")
                    {
                        var img = t.GetComponent<Image>();
                        if (img != null && img.sprite != null) _insetImage = img.sprite;
                    }
                    else if (_closeButtonSprite == null && name == "CloseButton")
                    {
                        var img = t.GetComponent<Image>();
                        if (img != null && img.sprite != null) _closeButtonSprite = img.sprite;
                    }
                    else if (_selfFlagSprite == null && name == "SelfFlag")
                    {
                        var img = t.GetComponent<Image>();
                        if (img != null && img.sprite != null) _selfFlagSprite = img.sprite;
                    }
                    else if (_lineSprite == null && name == "Line" && t.parent != null && t.parent.name.Contains("Score"))
                    {
                        var img = t.GetComponent<Image>();
                        if (img != null && img.sprite != null) _lineSprite = img.sprite;
                    }
                    else if (_primaryBtnSprite == null && (name == "ConfirmBtn" || name == "ReadyCountdownButton"))
                    {
                        var img = t.GetComponent<Image>();
                        if (img != null && img.sprite != null) _primaryBtnSprite = img.sprite;
                    }
                    else if (_secondaryBtnSprite == null && (name == "RefreshButton" || name == "CardScrollOperationButton"))
                    {
                        var img = t.GetComponent<Image>();
                        if (img != null && img.sprite != null) _secondaryBtnSprite = img.sprite;
                    }

                    if (_nativeFont == null && (name == "RoundLabel" || name == "TitleLabel"))
                    {
                        var tmp = t.GetComponent<TextMeshProUGUI>();
                        if (tmp != null && tmp.font != null)
                        {
                            _nativeFont = tmp.font;
                            _nativeFontMaterial = tmp.fontSharedMaterial;
                        }
                    }
                }

                if (_windowSprite != null && _bannerSprite != null)
                {
                    _harvested = true;
                }
            }
            catch (Exception ex)
            {
                _ctx.Log.Warn("采集游戏原生UI资源跳过：" + ex.Message);
            }
        }

        public void ApplyTextStyle(TextMeshProUGUI label, int fontSize = 16, bool bold = false)
        {
            ApplyTextStyle(label, fontSize, bold, TextAlignmentOptions.Center);
        }

        public void ApplyTextStyle(TextMeshProUGUI label, int fontSize, bool bold, TextAlignmentOptions align)
        {
            if (label == null) return;
            Harvest();
            if (_nativeFont != null)
            {
                label.font = _nativeFont;
                if (_nativeFontMaterial != null) label.fontSharedMaterial = _nativeFontMaterial;
            }
            label.fontSize = fontSize;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = align;
            label.color = TextParchment;
        }

        public void ApplyWindowFrame(Image image)
        {
            if (image == null) return;
            Harvest();
            if (_windowSprite != null)
            {
                image.sprite = _windowSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = LacquerBlack;
            }
        }

        public void ApplyBanner(Image image)
        {
            if (image == null) return;
            Harvest();
            if (_bannerSprite != null)
            {
                image.sprite = _bannerSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = new Color(0.20f, 0.16f, 0.10f, 0.98f);
            }
        }

        public void ApplyInset(Image image)
        {
            if (image == null) return;
            Harvest();
            if (_insetImage != null)
            {
                image.sprite = _insetImage;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = RecessedJade;
            }
        }

        public void ApplyLine(Image image)
        {
            if (image == null) return;
            Harvest();
            if (_lineSprite != null)
            {
                image.sprite = _lineSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = DividerBronze;
            }
        }

        public void ApplySelfFlag(Image image)
        {
            if (image == null) return;
            Harvest();
            if (_selfFlagSprite != null)
            {
                image.sprite = _selfFlagSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = SoftGoldGlow;
            }
        }

        public static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public NativeButton CreateButtonTopLeft(Transform parent, string name, string text, float x, float y, float width, float height, NativeBtnStyle style, Action onClick, int fontSize = 15)
        {
            Harvest();
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            SetTopLeft(rect, x, y, width, height);

            var img = go.GetComponent<Image>();
            var btn = go.GetComponent<Button>();

            var labelGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            ApplyTextStyle(label, fontSize, true, TextAlignmentOptions.Center);
            label.text = text;
            label.raycastTarget = false;

            ApplyBtnVisual(img, btn, label, style);

            return new NativeButton(go, btn, label, img, onClick, _ctx);
        }

        public NativeButton CreateButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, NativeBtnStyle style, Action onClick, int fontSize = 16)
        {
            Harvest();
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = go.GetComponent<Image>();
            var btn = go.GetComponent<Button>();

            var labelGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            ApplyTextStyle(label, fontSize, true, TextAlignmentOptions.Center);
            label.text = text;
            label.raycastTarget = false;

            ApplyBtnVisual(img, btn, label, style);

            return new NativeButton(go, btn, label, img, onClick, _ctx);
        }

        void ApplyBtnVisual(Image img, Button btn, TextMeshProUGUI label, NativeBtnStyle style)
        {
            ColorBlock colors = btn.colors;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;

            if (style == NativeBtnStyle.Primary)
            {
                if (_primaryBtnSprite != null)
                {
                    img.sprite = _primaryBtnSprite;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
                    colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                    colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                }
                else
                {
                    img.color = new Color(0.72f, 0.48f, 0.20f, 1f);
                    colors.normalColor = new Color(0.72f, 0.48f, 0.20f, 1f);
                    colors.highlightedColor = new Color(0.85f, 0.58f, 0.26f, 1f);
                    colors.pressedColor = new Color(0.55f, 0.35f, 0.14f, 1f);
                    colors.disabledColor = new Color(0.35f, 0.28f, 0.20f, 0.5f);
                }
                if (label != null) label.color = new Color(1f, 0.95f, 0.85f, 1f);
            }
            else if (style == NativeBtnStyle.Secondary)
            {
                if (_secondaryBtnSprite != null)
                {
                    img.sprite = _secondaryBtnSprite;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
                    colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                    colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                }
                else
                {
                    img.color = new Color(0.18f, 0.26f, 0.28f, 0.95f);
                    colors.normalColor = new Color(0.18f, 0.26f, 0.28f, 0.95f);
                    colors.highlightedColor = new Color(0.24f, 0.34f, 0.36f, 1f);
                    colors.pressedColor = new Color(0.12f, 0.18f, 0.20f, 1f);
                    colors.disabledColor = new Color(0.15f, 0.18f, 0.20f, 0.5f);
                }
                if (label != null) label.color = new Color(0.88f, 0.94f, 0.92f, 1f);
            }
            else if (style == NativeBtnStyle.Danger)
            {
                img.color = new Color(0.58f, 0.18f, 0.16f, 0.95f);
                colors.normalColor = new Color(0.58f, 0.18f, 0.16f, 0.95f);
                colors.highlightedColor = new Color(0.72f, 0.24f, 0.20f, 1f);
                colors.pressedColor = new Color(0.42f, 0.12f, 0.10f, 1f);
                colors.disabledColor = new Color(0.30f, 0.15f, 0.15f, 0.5f);
                if (label != null) label.color = new Color(1f, 0.88f, 0.88f, 1f);
            }
            else if (style == NativeBtnStyle.Caution)
            {
                img.color = new Color(0.60f, 0.42f, 0.18f, 0.95f);
                colors.normalColor = new Color(0.60f, 0.42f, 0.18f, 0.95f);
                colors.highlightedColor = new Color(0.74f, 0.52f, 0.22f, 1f);
                colors.pressedColor = new Color(0.45f, 0.30f, 0.12f, 1f);
                colors.disabledColor = new Color(0.30f, 0.25f, 0.18f, 0.5f);
                if (label != null) label.color = new Color(1f, 0.94f, 0.82f, 1f);
            }
            else if (style == NativeBtnStyle.Close)
            {
                if (_closeButtonSprite != null)
                {
                    img.sprite = _closeButtonSprite;
                    img.color = Color.white;
                    if (label != null) label.text = "";
                }
                else
                {
                    img.color = new Color(0.50f, 0.15f, 0.15f, 0.85f);
                    if (label != null)
                    {
                        label.text = "×";
                        label.fontSize = 24;
                        label.color = Color.white;
                    }
                }
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
            else // Pill
            {
                img.color = new Color(0.24f, 0.22f, 0.18f, 0.95f);
                colors.normalColor = new Color(0.24f, 0.22f, 0.18f, 0.95f);
                colors.highlightedColor = new Color(0.36f, 0.32f, 0.25f, 1f);
                colors.pressedColor = new Color(0.16f, 0.14f, 0.12f, 1f);
                colors.disabledColor = new Color(0.18f, 0.16f, 0.14f, 0.5f);
                if (label != null) label.color = AntiqueGold;
            }

            btn.colors = colors;
        }
    }
}
#endif
