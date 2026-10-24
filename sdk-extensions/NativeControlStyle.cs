using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Yx.ModSdk.Unity
{
    /// <summary>Visual-only reuse of caller-selected native controls. Does not copy listeners or game components.</summary>
    public static class NativeControlStyle
    {
        public static void Button(Button target, Button template, string text, float fontSize)
        {
            if (target == null || template == null) return;
            Image source = template.targetGraphic as Image;
            Image image = target.targetGraphic as Image;
            if (source != null && image != null)
            { image.sprite = source.sprite; image.type = source.type; image.color = source.color; image.material = source.material; }
            target.colors = template.colors;
            var label = target.GetComponentInChildren<TextMeshProUGUI>(true);
            var original = template.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null) return;
            if (original != null) { label.font = original.font; label.fontSharedMaterial = original.fontSharedMaterial; label.color = original.color; }
            label.text = text ?? ""; label.fontSize = fontSize; label.enableAutoSizing = false;
            label.enableWordWrapping = false; label.richText = false; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center; label.margin = Vector4.zero;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(4f, 1f); label.rectTransform.offsetMax = new Vector2(-4f, -1f);
        }
        /// <summary>Preserve the input's listeners, selection, caret and current text.</summary>
        public static void Input(TMP_InputField field, TMP_InputField template, int maximumCharacters, float fontSize)
        {
            if (field == null) return;
            field.characterLimit = maximumCharacters;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            if (template != null)
            {
                Image source = template.targetGraphic as Image; Image target = field.targetGraphic as Image;
                if (source != null && target != null) { target.sprite = source.sprite; target.type = source.type; target.color = source.color; }
                if (field.textComponent != null && template.textComponent != null)
                { field.textComponent.font = template.textComponent.font; field.textComponent.fontSharedMaterial = template.textComponent.fontSharedMaterial; }
            }
            if (field.textComponent != null) { field.textComponent.fontSize = fontSize; field.textComponent.enableWordWrapping = false; field.textComponent.margin = Vector4.zero; }
        }
        public static void Frame(Image target, Image template)
        {
            if (target == null || template == null) return;
            target.sprite = template.sprite; target.type = template.type; target.color = template.color; target.material = template.material;
        }
    }
}
