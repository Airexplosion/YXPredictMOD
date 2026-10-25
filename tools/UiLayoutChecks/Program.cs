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
        plate.anchoredPosition = new Vector2(0, -10 - HorseCaption.GapBelowName - top);
        Near(UiTextBox.PointIn(name, caption, glyph).y + HorseCaption.StrokeSize, -11);
        UiTextBox.BringToFront(caption);
        True(UiTextBox.Container(caption).Raised && !caption.rectTransform.Raised, "Only inner text was raised");
        Console.WriteLine("PASS history heading, nested glyph gap and complete label stacking");
    }
    static void BlackCaptionStroke()
    {
        var root = Box(null, 0, 0, 150, 48);
        var ui = new Yx.ModSdk.Unity.UiKit();
        var caption = new HorseCaption(ui, root);
        True(ui.Labels.Count == 9 && ui.Labels[8] == caption.Text, "Caption needs eight black edges behind one colored face");
        foreach (string value in new string[] { "中等马 3710", "特等马 4266", "电子马·地狱" })
        {
            var color = new Color(1, 0.83f, 0.28f, 1);
            caption.Set(value, color); caption.Set(value, color);
            var order = new System.Collections.Generic.List<Transform>(); PaintOrder(root, order);
            var offsets = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < ui.Labels.Count; i++)
            {
                var layer = ui.Labels[i]; var box = UiTextBox.Container(layer);
                True(layer.text == value && layer.fontStyle == FontStyles.Bold && layer.fontSize == 22, "Stroke text, score or font failed to follow face");
                True(!layer.raycastTarget && !layer.richText && !layer.enableWordWrapping, "Stroke layer captured input or changed text layout");
                True(box.parent == root, "Stroke layer escaped the caption cleanup root");
                if (i == 8) { Near(box.anchoredPosition.x, 0); Near(box.anchoredPosition.y, 0); Near(layer.color.g, color.g); continue; }
                Near(layer.color.r, 0); Near(layer.color.g, 0); Near(layer.color.b, 0); Near(layer.color.a, 1);
                True(order.IndexOf(box) < order.IndexOf(UiTextBox.Container(caption.Text)), "Black edge covers colored face");
                float x = box.anchoredPosition.x, y = box.anchoredPosition.y;
                True((x != 0 || y != 0) && System.Math.Abs(x) <= 1.5f && System.Math.Abs(y) <= 1.5f, "Invalid stroke offset");
                offsets.Add(x + "," + y);
            }
            True(offsets.Count == 8, "Black edges must cover eight distinct directions");
        }
        Console.WriteLine("PASS shader-independent 1.5px solid black caption outline, eight directions, text/score updates, color and cleanup ownership");
    }
    static void PaintOrder(Transform root, System.Collections.Generic.List<Transform> order)
    {
        order.Add(root);
        for (int i = 0; i < root.childCount; i++) PaintOrder(root.GetChild(i), order);
    }
    static void HorseIconLayering()
    {
        foreach (float scale in new float[] { 1, 0.65f, 1.5f })
        {
            var canvas = Box(null, 300, 200, 1920, 1080); canvas.localScale = new Vector3(scale, scale, 1);
            for (int slot = 0; slot < 8; slot++)
            {
                var item = Box(canvas, 300 + slot * 158, -30, 150, 200);
                item.pivot = new Vector2(0.5f, slot % 2 == 0 ? 0.5f : 1);
                var avatar = Box(item, 0, -20, 100, 100);
                var name = Box(item, 0, -125, 150, 30);
                name.pivot = new Vector2(0.5f, 0.5f); name.localScale = new Vector3(0.9f, 0.9f, 1);
                var hud = Box(item, 0, 0, 30, 30);
                var caption = Label(name, 0, -30, 150, 48);
                var icon = Box(name, 0, 0, 56, 56);
                var face = Box(icon, 0, 0, 100, 100);
                var order = new System.Collections.Generic.List<Transform>(); PaintOrder(item, order);
                True(order.IndexOf(icon) > order.IndexOf(name), "Old child icon should reproduce covering native ID");
                for (int repeat = 0; repeat < 20; repeat++)
                {
                    avatar.sizeDelta = new Vector2(90 + repeat, 100 + repeat);
                    avatar.pivot = new Vector2(0.3f, 0.7f);
                    HorsePlateLayout.PlaceIcon(icon, avatar);
                    True(icon.parent == avatar && name.parent == item, "Native ID was moved or icon did not cover avatar");
                    True(avatar.GetSiblingIndex() == 0 && name.GetSiblingIndex() == 1 && hud.GetSiblingIndex() == 2, "Native siblings were reordered");
                    Near(icon.Width, 90 + repeat); Near(icon.Height, 90 + repeat);
                    Vector2 center = avatar.rect.center;
                    Vector3 expected = avatar.TransformPoint(new Vector3(center.x, center.y, 0));
                    Vector3 actual = icon.TransformPoint(new Vector3(0, 0, 0));
                    Near(actual.x, expected.x); Near(actual.y, expected.y);
                    order.Clear(); PaintOrder(item, order);
                    True(order.IndexOf(icon) < order.IndexOf(name) && order.IndexOf(icon) < order.IndexOf(caption.rectTransform), "ID or caption painted behind icon");
                    True(face.parent == icon && order.IndexOf(avatar) < order.IndexOf(face) && order.IndexOf(face) < order.IndexOf(name), "Masked image did not cover avatar or covered native ID");
                    True(order.IndexOf(face) < order.IndexOf(hud), "Overlay covered native health/selection HUD");
                }
                var moved = Box(canvas, slot * 158, -60, 180, 210);
                avatar.SetParent(moved, false);
                HorsePlateLayout.PlaceIcon(icon, avatar);
                True(icon.parent == avatar && avatar.parent == moved && name.parent == item, "Icon did not follow reparented avatar or moved ID");
            }
        }
        Console.WriteLine("PASS circular avatar overlays follow native size, pivot, eight slots, 20 refreshes, scaling and reparenting; native ID/title/HUD stay above");
    }
    static void HorseIconVisibility()
    {
        string[] players = { "player1", "player2", "player3" };
        foreach (string focus in new string[] { null, "", "other", "player1", "player2" })
        {
            foreach (string uid in players)
            {
                True(HorsePlateLayout.ShouldShow(true, true, false, uid, focus, false) == (uid != focus), "Default mode did not reveal focused role or cover other avatars");
                True(!HorsePlateLayout.ShouldShow(true, true, false, uid, focus, true), "Default hover did not reveal the role");
                True(HorsePlateLayout.ShouldShow(true, true, true, uid, focus, false) == (uid == focus), "Alternate mode did not reveal icon only for focused UID");
                True(HorsePlateLayout.ShouldShow(true, true, true, uid, focus, true), "Alternate hover did not reveal the icon");
                for (int flags = 0; flags < 4; flags++)
                {
                    bool iconsOnFocus = (flags & 1) != 0, hover = (flags & 2) != 0;
                    True(!HorsePlateLayout.ShouldShow(false, true, iconsOnFocus, uid, focus, hover), "Missing sprite or disabled image setting exposed overlay");
                    True(!HorsePlateLayout.ShouldShow(true, false, iconsOnFocus, uid, focus, hover), "Hidden or reused native avatar exposed overlay");
                    True(!HorsePlateLayout.ShouldShow(true, true, iconsOnFocus, null, focus, hover), "Unbound player exposed overlay");
                }
            }
        }
        // 同时查看A、悬停B时两者都露出角色；移开鼠标、改看B后A恢复马图。
        True(!HorsePlateLayout.ShouldShow(true, true, false, "player1", "player1", false), "Viewing A did not reveal A");
        True(!HorsePlateLayout.ShouldShow(true, true, false, "player2", "player1", true), "Hover B did not reveal B while viewing A");
        True(HorsePlateLayout.ShouldShow(true, true, false, "player2", "player1", false), "Leaving B did not restore its icon");
        True(HorsePlateLayout.ShouldShow(true, true, false, "player1", "player2", false), "Switching to B did not restore A's icon");
        True(!HorsePlateLayout.ShouldShow(true, true, false, "player2", "player2", false), "Switching to B did not reveal B");
        Console.WriteLine("PASS default icons/role-on-focus and alternate roles/icon-on-focus, pointer leave/focus switch and disabled guards");
    }
    static void HorseHoverRegions()
    {
        foreach (float scale in new float[] { 0.65f, 1, 1.5f })
        {
            var canvas = Box(null, 300, 200, 1920, 1080); canvas.localScale = new Vector3(scale, scale, 1);
            for (int slot = 0; slot < 8; slot++)
            {
                var item = Box(canvas, 300 + slot * 158, -30, 150, 210);
                var avatar = Box(item, 75, -60, 100, 100); avatar.pivot = new Vector2(0.5f, 0.5f);
                var name = Box(item, 75, -126, 150, 30); name.pivot = new Vector2(0.5f, 0.5f);
                var caption = Box(name, 75, -30, 150, 48); caption.pivot = new Vector2(0.5f, 1);
                var glyph = new Rect(-70, -23, 140, 23);
                Rect area = HorsePlateLayout.HoverBounds(avatar, name, caption, glyph);
                Near(area.x, -83); Near(area.y, -120); Near(area.width, 166); Near(area.height, 186);
                foreach (Vector2 point in new Vector2[] { new Vector2(-80, 60), new Vector2(65, 55), new Vector2(0, 0),
                    new Vector2(70, -68), new Vector2(70, -104), new Vector2(-80, -90), new Vector2(0, -118) })
                {
                    True(HorsePlateLayout.ContainsPoint(area, point), "Player rectangle missed corner, health badge, ID, caption or blank gap");
                    True(!HorsePlateLayout.ShouldShow(true, true, false, "player", null, true), "Expanded hover failed in default mode");
                    True(HorsePlateLayout.ShouldShow(true, true, true, "player", null, true), "Expanded hover failed in alternate mode");
                }
                True(!HorsePlateLayout.ContainsPoint(area, new Vector2(0, -121)), "Area extended too far below title");
                True(!HorsePlateLayout.ContainsPoint(area, new Vector2(84, 0)), "Area extended into neighboring slot");
                True(!HorsePlateLayout.ContainsPoint(area, new Vector2(158, 0)), "Adjacent avatar center triggered this player");
                True(float.IsPositiveInfinity(HorsePlateLayout.HoverDistance(avatar, area, new Vector2(0, -121))), "Outside region became a hover candidate");
                // 两列相距158，热区边缘可能重叠；x=81离右列更近。
                True(HorsePlateLayout.ContainsPoint(area, new Vector2(81, 0)) && HorsePlateLayout.ContainsPoint(area, new Vector2(-77, 0)), "Overlap fixture missing");
                True(HorsePlateLayout.HoverDistance(avatar, area, new Vector2(-77, 0))
                    < HorsePlateLayout.HoverDistance(avatar, area, new Vector2(81, 0)), "Overlap did not prefer nearest player");
                // 改变pivot和文字缩放后，按实际控件坐标重算，而不是沿用屏幕像素常量。
                avatar.pivot = new Vector2(0.3f, 0.7f); name.localScale = new Vector3(0.9f, 0.9f, 1);
                area = HorsePlateLayout.HoverBounds(avatar, name, caption, glyph);
                Vector3 captionPoint = avatar.InverseTransformPoint(caption.TransformPoint(new Vector3(60, -23, 0)));
                True(HorsePlateLayout.ContainsPoint(area, new Vector2(captionPoint.x, captionPoint.y)), "Changed pivot or text scale lost title hit area");
                avatar.sizeDelta = Vector2.zero;
                area = HorsePlateLayout.HoverBounds(avatar, name, caption, glyph);
                True(!HorsePlateLayout.ContainsPoint(area, Vector2.zero), "Unlaid-out avatar triggered hover");
            }
        }
        Console.WriteLine("PASS full player hover rectangle: avatar corners, health badge, ID, caption, blank gaps, boundaries, nearest overlap, eight slots and three scales");
    }
    static void HorseBindings(string path)
    {
        using (ModuleDefinition module = ModuleDefinition.ReadModule(path))
        {
            TypeDefinition type = module.GetType("YxPrediction.TianJiFeature");
            foreach (var method in type.Methods)
            {
                if (method.Name == "Update") True(Calls(method, "UpdateIcons"), "Icon hover/focus not refreshed every frame");
                if (method.Name == ".ctor")
                {
                    True(Calls(method, "RegisterHotkey"), "Configurable hotkey is missing");
                    bool newMode = false;
                    foreach (Instruction instruction in method.Body.Instructions)
                    {
                        string value = instruction.Operand as string;
                        True(value != "alwaysCover", "Removed always-cover setting was rebound");
                        if (value == "iconsOnFocus")
                        {
                            newMode = true;
                            True(instruction.Next.OpCode == OpCodes.Ldc_I4_0, "Upgraded users must start with icons visible by default");
                        }
                    }
                    True(newMode, "New independent mode setting is missing");
                }
                if (method.Name == "ToggleIconMode") True(Calls(method, "set_Value") && Calls(method, "GetComponent"), "Hotkey must persist mode and avoid typing in input fields");
                if (method.Name == "UpdateIcons")
                    True(Calls(method, "PlaceIcon") && Calls(method, "ShouldShow") && Calls(method, "HoverDistance") && Calls(method, "get_isActiveAndEnabled"), "Avatar placement, visibility or full player hover guard not bound");
                if (method.Name == "PositionPlate") True(Calls(method, "HoverBounds"), "Actual ID/caption bounds not bound to hover area");
                if (method.Name == "ClearPlates") True(Calls(method, "DestroyPlate"), "Clearing a match leaked independent icons");
                if (method.Name == "RefreshPlates")
                {
                    True(Calls(method, "DestroyPlate"), "Stale icons were not cleaned up");
                    True(Calls(method, "Set"), "HorseCaption outline not updated from production title");
                    True(Calls(method, "CircleMask") && Calls(method, "set_showMaskGraphic"), "Circular native UI mask is missing");
                }
            }
        }
        Console.WriteLine("PASS compiled avatar overlay, per-frame visibility, configurable persisted hotkey, input guard, cleanup and circular mask bindings");
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
        try { SdkContract(args[0]); ReproduceOldFailure(); Modes(1); Modes(0.65f); HeaderAndGlyph(); BlackCaptionStroke(); HorseIconLayering(); HorseIconVisibility(); HorseHoverRegions(); HorseBindings(args[1]); ObserverVisibility(); VisibilityBindings(args[1]); Console.WriteLine("UI LAYOUT / VISIBILITY PASS (models and compiled bindings; no game rendering)"); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
