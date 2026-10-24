# Classic UI compatibility sources

NativeControlStyle.cs, NativePanelTheme.cs, NativePredictionRow.cs NativePredictionWindow.cs and NativePredictionBar.cs are byte-identical to yxp-modloader/sdk/Yx.ModSdk/Unity and public SDK source/Unity. The public SDK commit is 4006c499a000b0dab119714d066fbc2438119ba3; documentation: https://github.com/Airexplosion/yixianpai-mod-sdk/blob/main/docs/classic-prediction-ui.md .

The sources compile into this MOD for compatibility with older manager SDK binaries. The public SDK also provides Yx.ModSdk.ClassicUi.dll and a sample. Do not link both the source copies and add-on assembly in the same MOD. No game or Unity implementation or vendor DLL is included. Keep all copies byte-identical when updating. Business/network/identity logic remains in the MOD.
