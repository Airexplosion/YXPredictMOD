# Source compatibility shim

NativeControlStyle.cs is byte-identical to the new SDK source at yxp-modloader/sdk/Yx.ModSdk/Unity/NativeControlStyle.cs (SDK commit 63e39872c5fa9126cf816150e9685598cc2812d3). It is compiled only for the game build, to support older deployed SDK binaries. No game implementation or vendor DLL is included. Remove this compile include when requiring an SDK release which supplies the helper, and then reference the SDK class directly.

The authoritative API belongs in SDK; this is a compatibility source copy, not a new mod-specific implementation. Keep both files byte-identical when updating it.
