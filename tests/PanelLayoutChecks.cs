using System.Collections.Generic;
using YxPrediction;

internal static partial class Program
{
    static void PanelLayoutChecks()
    {
        Check("real Node room response decodes shared pools and personal position", RoomWireFixture.Verify);

        Check("random room waits for matching live snapshot and rejects timeout", delegate
        {
            MatchSnapshot snapshot = Snapshot(1);
            var data = new Dictionary<string, object>(); data["code"] = "123456"; data["gameId"] = snapshot.Id; data["gameRoomId"] = "native-room_1";
            var match = new RoomMatch(data, 10);
            True(match.CanJoin(snapshot, 11));
            snapshot.LiveObserver = false; True(!match.CanJoin(snapshot, 11)); snapshot.LiveObserver = true;
            snapshot.Ended = true; True(!match.CanJoin(snapshot, 11)); snapshot.Ended = false;
            string id = snapshot.Id; snapshot.Id = "another-game"; True(!match.CanJoin(snapshot, 11)); snapshot.Id = id;
            True(!match.CanJoin(null, 11)); True(!match.CanJoin(snapshot, 100)); True(match.Expired(100));
            snapshot.Players[1] = snapshot.Players[0]; True(!match.CanJoin(snapshot, 11));
        });

        Check("enlarged overview fits common canvases with top and bottom clearance", delegate
        {
            double[] widths = { 1920, 1600, 1280, 1024 };
            double[] heights = { 1080, 900, 720, 768 };
            for (int i = 0; i < widths.Length; i++)
            {
                double scale = PanelLayout.WindowScale(widths[i], heights[i], false);
                True(PanelLayout.WindowWidth * scale <= widths[i] - 31);
                True(PanelLayout.WindowHeight * scale <= heights[i] - 179);
            }
            Near(PanelLayout.WindowScale(1920, 1080, false), 1.25);
        });

        Check("panel layout defaults to center and smart auto docking", delegate
        {
            var layout = new PanelLayout(null, null);
            Equal(layout.WindowX, 0.5);
            Equal(layout.WindowY, 0.5);
            Equal(layout.BetBarCustom, false);
            Equal(layout.IsPeek, false);
            Equal(layout.IsMinimized, false);
            Equal(PanelLayout.BetBarAutoY(true, false), PanelLayout.BetBarHandY);
            Equal(PanelLayout.BetBarAutoY(true, true), PanelLayout.BetBarHandY);
            Equal(PanelLayout.BetBarAutoY(false, true), PanelLayout.BetBarTalentsY);
            Equal(PanelLayout.BetBarAutoY(false, false), PanelLayout.BetBarEmptyY);
        });

        Check("panel layout window and betbar clamp properly to screen fractions", delegate
        {
            var layout = new PanelLayout(null, null);
            layout.MoveWindow(1.5, -0.5, 0.6, 0.4);
            Near(layout.WindowX, 0.7);
            Near(layout.WindowY, 0.2);

            layout.MoveBetBar(-1.0, 2.0, 0.4, 0.2);
            True(layout.BetBarCustom);
            Near(layout.BetBarX, 0.2);
            Near(layout.BetBarY, 0.9);

            layout.ResetBetBarPosition();
            Equal(layout.BetBarCustom, false);
        });

        Check("panel layout state persists and restores cleanly", delegate
        {
            var layout = new PanelLayout(null, null);
            layout.MoveWindow(0.4, 0.6, 0.2, 0.2);
            layout.MoveBetBar(0.3, 0.25, 0.2, 0.1);
            Dictionary<string, object> winData = layout.WriteWindow();
            Dictionary<string, object> barData = layout.WriteBetBar();

            var restored = new PanelLayout(winData, barData);
            Near(restored.WindowX, 0.4);
            Near(restored.WindowY, 0.6);
            True(restored.BetBarCustom);
            Near(restored.BetBarX, 0.3);
            Near(restored.BetBarY, 0.25);
        });
    }
}
