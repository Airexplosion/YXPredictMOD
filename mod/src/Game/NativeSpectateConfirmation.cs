#if YX_GAME
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YxPrediction
{
    // Only the synchronous confirmation opened by the explicit create-and-spectate click.
    // Never poll for or accept an unrelated/previously visible native dialog.
    public static class NativeSpectateConfirmation
    {
        static readonly FieldInfo Content = typeof(MessageBoxWindow).GetField("m_ContentLabel", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo Confirm = typeof(MessageBoxWindow).GetField("m_ConfirmButton", BindingFlags.Instance | BindingFlags.NonPublic);
        public static bool CanBegin(out string error)
        {
            error = null;
            if (Content == null || Confirm == null) { error = "当前游戏的原生确认控件不兼容"; return false; }
            Object[] windows = Object.FindObjectsOfType(typeof(MessageBoxWindow));
            for (int i = 0; i < windows.Length; i++)
            {
                MessageBoxWindow window = windows[i] as MessageBoxWindow;
                if (window != null && window.gameObject.activeInHierarchy)
                { error = "请先关闭当前游戏提示，再创建并观战"; return false; }
            }
            return true;
        }
        public static bool ConfirmOpened(out string error)
        {
            error = "观战未能启动，请查看游戏提示";
            Object[] windows = Object.FindObjectsOfType(typeof(MessageBoxWindow));
            MessageBoxWindow selected = null;
            for (int i = 0; i < windows.Length; i++)
            {
                MessageBoxWindow window = windows[i] as MessageBoxWindow;
                if (window == null || !window.gameObject.activeInHierarchy) continue;
                if (selected != null) return false;
                selected = window;
            }
            if (selected == null) return false;
            TextMeshProUGUI content = Content.GetValue(selected) as TextMeshProUGUI;
            Button confirm = Confirm.GetValue(selected) as Button;
            if (content == null || content.text != "是否观战".Translate() || confirm == null
                || !confirm.gameObject.activeInHierarchy || !confirm.interactable) return false;
            confirm.onClick.Invoke();
            error = null;
            return true;
        }
    }
}
#endif
