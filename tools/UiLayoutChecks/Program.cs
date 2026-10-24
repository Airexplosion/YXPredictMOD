using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TMPro;
using UnityEngine;
using YxPrediction;

internal static class Program
{
    static void Near(float value, float expected)
    { if (Math.Abs(value - expected) > 0.001f) throw new Exception("Expected " + expected + ", got " + value); }
    static void True(bool value, string message) { if (!value) throw new Exception(message); }
    static RectTransform Box(Transform parent, float x, float y, float width, float height)
    {
        return new RectTransform { parent = parent, anchorMin = new Vector2(0, 1), anchorMax = new Vector2(0, 1),
            pivot = new Vector2(0, 1), anchoredPosition = new Vector2(x, y), sizeDelta = new Vector2(width, height) };
    }
    static TextMeshProUGUI Label(Transform parent, float x, float y, float width, float height)
    {
        RectTransform outer = Box(parent, x, y, width, height);
        // 已核对SDK MakeText：内层stretch、pivot(0.5,0.5)，offsetMin(6,2)/offsetMax(-6,-2)。
        return new TextMeshProUGUI { rectTransform = new RectTransform { parent = outer,
            anchorMin = new Vector2(0, 0), anchorMax = new Vector2(1, 1), pivot = new Vector2(0.5f, 0.5f), sizeDelta = new Vector2(-12, -4) } };
    }
    static bool Calls(MethodDefinition method, string name)
    {
        foreach (Instruction i in method.Body.Instructions) { var call = i.Operand as MethodReference; if (call != null && call.Name == name) return true; }
        return false;
    }
    static void SdkContract(string path)
    {
        using (ModuleDefinition module = ModuleDefinition.ReadModule(path))
        {
            TypeDefinition kit = module.GetType("Yx.ModSdk.Unity.UiKit");
            MethodDefinition label = null, makeText = null;
            foreach (var method in kit.Methods) { if (method.Name == "Label") label = method; if (method.Name == "MakeText") makeText = method; }
            True(label != null && makeText != null && Calls(label, "TopLeft") && Calls(label, "MakeText"), "SDK label wrapper contract changed");
            True(Calls(makeText, "SetParent") && Calls(makeText, "set_anchorMin") && Calls(makeText, "set_anchorMax")
                && Calls(makeText, "set_offsetMin") && Calls(makeText, "set_offsetMax"), "SDK inner text contract changed");
        }
        Console.WriteLine("PASS actual SDK label/container hierarchy contract");
    }
    static void ReproduceOldFailure()
    {
        var row = Box(null, 0, 0, 1120, 63);
        var name = Label(row, 18, 0, 174, 60);
        name.rectTransform.anchoredPosition = new Vector2(18, 0); name.rectTransform.sizeDelta = new Vector2(174, 30);
        Near(name.rectTransform.TopLeft().x, -51);
        var result = Label(row, 840, 0, 265, 60);
        result.rectTransform.anchoredPosition = new Vector2(716, 0); result.rectTransform.sizeDelta = new Vector2(386, 60);
        Near(result.rectTransform.TopLeft().x, 1363);
        Console.WriteLine("PASS reproduced reported player-name -51 and result 1363 overflow");
    }
    static void Modes(float scale)
    {
        var panel = Box(null, 400, 200, 1120, 730); panel.localScale = new Vector3(scale, scale, 1);
        for (int slot = 0; slot < 8; slot++)
        {
            var row = Box(panel, 0, -138 - slot * 63, 1120, 63);
            var name = Label(row, 18, 0, 174, 60); var result = Label(row, 840, 0, 265, 60); var preview = Label(row, 270, -36, 558, 24);
            // 同一批实际控件反复切换详细、简洁、历史、简洁，检查容器及内层stretch均不漂移。
            for (int mode = 0; mode < 4; mode++)
            {
                bool detailed = mode == 0, history = mode == 2;
                float x = detailed ? 840 : (history ? 428 : 716), width = detailed ? 265 : (history ? 674 : 386);
                UiTextBox.Place(name, 18, 0, 174, 30); UiTextBox.Place(result, x, 0, width, 62);
                UiTextBox.Place(preview, detailed ? 270 : 212, -36, detailed ? 558 : 484, 26);
                Near(row.InverseTransformPoint(name.rectTransform.TopLeft()).x, 24);
                Near(row.InverseTransformPoint(result.rectTransform.TopLeft()).x, x + 6);
                Near(result.rectTransform.Width, width - 12); Near(result.rectTransform.Height, 58);
                Near(preview.rectTransform.anchoredPosition.y, 0); Near(preview.rectTransform.sizeDelta.y, -4);
                Near(row.InverseTransformPoint(preview.rectTransform.TopLeft()).y, -38);
                True(x >= 0 && x + width <= 1120, "Result container exceeds panel");
            }
        }
        Console.WriteLine("PASS eight rows keep bounds across modes at scale " + scale);
    }
    static void HeaderAndGlyph()
    {
        var panel = Box(null, 400, 200, 1120, 730); panel.localScale = new Vector3(0.65f, 0.65f, 1);
        var heading = Label(panel, 716, -104, 386, 28);
        UiTextBox.Place(heading, 428, -104, 674, 28);
        Near(panel.InverseTransformPoint(heading.rectTransform.TopLeft()).x, 434); Near(heading.rectTransform.Width, 662);
        var name = Box(panel, 600, -100, 160, 40);
        var plate = Box(name, 0, 0, 150, 48);
        var caption = Label(plate, 0, 0, 150, 48);
        var glyph = new Vector3(0, 16, 0);
        float top = UiTextBox.PointIn(plate, caption, glyph).y;
        Near(top, -8); // 忽略SDK内层位移时会误用16，正好多出24单位间隙。
        plate.anchoredPosition = new Vector2(0, -10 - 1 - top);
        Near(UiTextBox.PointIn(name, caption, glyph).y, -11);
        UiTextBox.BringToFront(caption);
        True(UiTextBox.Container(caption).Raised && !caption.rectTransform.Raised, "Only inner text was raised");
        Console.WriteLine("PASS history heading, nested glyph gap and complete label stacking");
    }
    static void ObserverVisibility()
    {
        SceneLoader.currentSceneName = "Battle";
        BattleManager.Instance = null;
        True(!PredictionVisibility.CanShow(), "Missing manager exposed prediction");
        var manager = new BattleManager(); BattleManager.Instance = manager;
        True(!PredictionVisibility.CanShow(), "Missing game status exposed prediction");
        var status = new Proto.GameStatus(); manager.currentGameStatus = status;
        // 穷举观战/实时观战/裁判、参赛、回放标志；参赛和回放始终优先阻止显示。
        for (int flags = 0; flags < 64; flags++)
        {
            manager.isSpectating = (flags & 1) != 0;
            manager.isRealtimeSpectating = (flags & 2) != 0;
            manager.isJudge = (flags & 4) != 0;
            status.Participating = (flags & 8) != 0;
            manager.replaying = (flags & 16) != 0;
            status.gameMode = (flags & 32) != 0 ? Proto.GameMode.ReviewMode : Proto.GameMode.Normal;
            bool allowed = (flags & 7) != 0 && (flags & 56) == 0;
            True(PredictionVisibility.CanShow() == allowed, "Wrong observer access flags " + flags);
            SceneLoader.currentSceneName = "MainMenu";
            True(!PredictionVisibility.CanShow(), "Prediction remained visible outside Battle");
            SceneLoader.currentSceneName = "Battle";
        }
        manager.isSpectating = true; manager.isRealtimeSpectating = false; manager.isJudge = false;
        manager.replaying = false; status.gameMode = Proto.GameMode.Normal; status.Participating = false;
        True(PredictionVisibility.CanShow(), "Spectator entry unavailable");
        status.Participating = true; // 淘汰参赛者可能同时带有isSpectating，仍不可显示。
        True(!PredictionVisibility.CanShow(), "Eliminated participant exposed prediction");
        status.Participating = false;
        True(PredictionVisibility.CanShow(), "Returning to live spectating did not restore entry");
        Console.WriteLine("PASS observer-only visibility: 64 role combinations, scenes and mode transitions");
    }
    static void VisibilityBindings(string path)
    {
        using (ModuleDefinition module = ModuleDefinition.ReadModule(path))
        {
            string[] types = { "PredictionLauncher", "PredictionLauncher", "PredictionPanel", "PredictionPanel", "PredictionMod", "PredictionMod" };
            string[] methods = { "Update", "Click", "Update", "Toggle", "PlaceBet", "Trade" };
            for (int i = 0; i < types.Length; i++)
            {
                TypeDefinition type = module.GetType("YxPrediction." + types[i]);
                MethodDefinition target = null;
                foreach (var method in type.Methods) if (method.Name == methods[i]) target = method;
                True(target != null && Calls(target, "CanShow"), "Missing live guard: " + types[i] + "." + methods[i]);
            }
            TypeDefinition horses = module.GetType("YxPrediction.TianJiFeature");
            foreach (var method in horses.Methods)
                if (method.HasBody) True(!Calls(method, "CanShow") && !Calls(method, "IsLiveObserver"), "Horse titles incorrectly restricted to observers");
            TypeDefinition mod = module.GetType("YxPrediction.PredictionMod");
            foreach (var method in mod.Methods)
                if (method.Name == "OnUpdate")
                    True(!Calls(method, "CanShow") && !Calls(method, "IsLiveObserver"), "Prediction gate blocked shared state and title updates");
        }
        Console.WriteLine("PASS compiled launcher/panel/trade guards and independent title updates");
    }
    static int Main(string[] args)
    {
        try { SdkContract(args[0]); ReproduceOldFailure(); Modes(1); Modes(0.65f); HeaderAndGlyph(); ObserverVisibility(); VisibilityBindings(args[1]); Console.WriteLine("UI LAYOUT / VISIBILITY PASS (models and compiled bindings; no game rendering)"); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
