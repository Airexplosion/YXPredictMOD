using System;
using YxPrediction;
namespace Proto { public enum OpenType { OpenXianFuLobby } }
public static class SceneLoader { public static string currentSceneName; }
public static class OpenManager { public static bool Enabled; public static bool IsOpen(Proto.OpenType type) { return Enabled; } }
public sealed class FakeRoom { public string Id; }
public sealed class FakeClient { public FakeRoom room; }
public static class GameClientUtil { public static FakeClient client; }
public class ILRPanelBase
{
    public static LobbyPanel Lobby;
    public static T FindILRPanel<T>() { return (T)(object)Lobby; }
}
public sealed class LobbyPanel
{
    public XianFuLobbyPanel Child;
    public T FindILRSubPanelRuntime<T>() { return (T)(object)Child; }
}
public sealed class XianFuLobbyPanel
{
    public bool Shown, Throw;
    public string Target;
    public void Show() { if (Throw) throw new Exception("init failed"); Shown = true; }
    public void SpectateRequest(string id) { if (!Shown) throw new Exception("not shown"); Target = id; }
}
internal static class Program
{
    static int Main()
    {
        string error; int checks = 0;
        SceneLoader.currentSceneName = "Battle"; OpenManager.Enabled = true;
        ILRPanelBase.Lobby = new LobbyPanel { Child = new XianFuLobbyPanel() };
        Assert(!NativeRoomSpectate.Request("native-id",out error) && ILRPanelBase.Lobby.Child.Target == null);checks++;
        SceneLoader.currentSceneName = "Lobby";OpenManager.Enabled = false;
        Assert(!NativeRoomSpectate.Request("native-id",out error) && ILRPanelBase.Lobby.Child.Target == null);checks++;
        OpenManager.Enabled = true;ILRPanelBase.Lobby = null;Assert(!NativeRoomSpectate.Request("native-id",out error));checks++;
        ILRPanelBase.Lobby = new LobbyPanel();Assert(!NativeRoomSpectate.Request("native-id",out error));checks++;
        ILRPanelBase.Lobby.Child = new XianFuLobbyPanel { Throw = true };Assert(!NativeRoomSpectate.Request("native-id",out error));checks++;
        ILRPanelBase.Lobby.Child = new XianFuLobbyPanel();Assert(NativeRoomSpectate.Request("native-id",out error) && error == null && ILRPanelBase.Lobby.Child.Shown && ILRPanelBase.Lobby.Child.Target == "native-id");checks++;
        GameClientUtil.client = null;Assert(NativeRoomSpectate.CurrentRoomId() == "");checks++;
        GameClientUtil.client = new FakeClient();Assert(NativeRoomSpectate.CurrentRoomId() == "");checks++;
        GameClientUtil.client.room = new FakeRoom { Id = "actual-room" };Assert(NativeRoomSpectate.CurrentRoomId() == "actual-room");checks++;
        checks += ConfirmationChecks();
        Console.WriteLine("Native panel entry checks passed: " + checks);return 0;
    }
    static void Assert(bool condition) { if (!condition) throw new Exception("Native entry guard or panel workflow failed"); }
    static int ConfirmationChecks()
    {
        string error;int clicks=0;
        UnityEngine.Object.Windows=new UnityEngine.Object[0];Assert(NativeSpectateConfirmation.CanBegin(out error));
        Assert(!NativeSpectateConfirmation.ConfirmOpened(out error));
        var window=new MessageBoxWindow("translated spectate",delegate { clicks++; });
        UnityEngine.Object.Windows=new UnityEngine.Object[]{window};
        Assert(!NativeSpectateConfirmation.CanBegin(out error)&&clicks==0);
        Assert(NativeSpectateConfirmation.ConfirmOpened(out error)&&clicks==1);
        window=new MessageBoxWindow("unrelated confirmation",delegate { clicks++; });
        UnityEngine.Object.Windows=new UnityEngine.Object[]{window};
        Assert(!NativeSpectateConfirmation.ConfirmOpened(out error)&&clicks==1);
        window=new MessageBoxWindow("translated spectate",delegate { clicks++; });window.Button.interactable=false;
        UnityEngine.Object.Windows=new UnityEngine.Object[]{window};Assert(!NativeSpectateConfirmation.ConfirmOpened(out error)&&clicks==1);
        window.Button.interactable=true;UnityEngine.Object.Windows=new UnityEngine.Object[]{window,new MessageBoxWindow("translated spectate",delegate{clicks++;})};
        Assert(!NativeSpectateConfirmation.ConfirmOpened(out error)&&clicks==1);
        window.gameObject.activeInHierarchy=false;UnityEngine.Object.Windows=new UnityEngine.Object[]{window};
        Assert(NativeSpectateConfirmation.CanBegin(out error)&&!NativeSpectateConfirmation.ConfirmOpened(out error));
        return 8;
    }
}
namespace UnityEngine
{
    public class Object { public static Object[] Windows;public static Object[] FindObjectsOfType(Type t) { return Windows; } }
    public class GameObject { public bool activeInHierarchy=true; }
}
namespace TMPro { public sealed class TextMeshProUGUI { public string text; } }
namespace UnityEngine.UI
{
    public sealed class Button
    {
        public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();public bool interactable=true;
        public Event onClick=new Event();
        public sealed class Event { public Action Click;public void Invoke(){if(Click!=null)Click();} }
    }
}
public sealed class MessageBoxWindow : UnityEngine.Object
{
    readonly TMPro.TextMeshProUGUI m_ContentLabel;
    readonly UnityEngine.UI.Button m_ConfirmButton;
    public UnityEngine.UI.Button Button { get { return m_ConfirmButton; } }
    public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();
    public MessageBoxWindow(string text,Action click) { m_ContentLabel=new TMPro.TextMeshProUGUI{text=text};m_ConfirmButton=new UnityEngine.UI.Button();m_ConfirmButton.onClick.Click=click; }
}
public static class Translation { public static string Translate(this string text) { return text=="是否观战"?"translated spectate":text; } }
