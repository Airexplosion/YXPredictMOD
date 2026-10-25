# Classic UI compatibility sources

NativeControlStyle.cs, NativePanelTheme.cs, NativePredictionRow.cs, NativePredictionWindow.cs and NativePredictionBar.cs originate from yxp-modloader/sdk/Yx.ModSdk/Unity and public SDK source/Unity. The public SDK base commit is 636a3d71cf91f67e164e221c0ee0a236ec1b9ae1; documentation: https://github.com/Airexplosion/yixianpai-mod-sdk/blob/main/docs/classic-prediction-ui.md .

Local change in MOD 0.7.5: NativePredictionWindow removes title-bar dragging and recenters when minimized or expanded. This prevents the expanded title bar from leaving the screen after a collapsed window is moved. Other components retain their upstream implementation; this change does not modify the installed SDK.

The sources compile into this MOD for compatibility with older manager SDK binaries. The public SDK also provides Yx.ModSdk.ClassicUi.dll and a sample. Do not link both the source copies and add-on assembly in the same MOD. No game or Unity implementation or vendor DLL is included. Preserve the documented fixed-window behavior when updating these sources. Business/network/identity logic remains in the MOD.
