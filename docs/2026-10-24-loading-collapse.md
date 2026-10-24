# 0.7.2 加载占位与可读折叠按钮（2026-10-24）

整理原版PR期间用户补充：Elo读取时全黑像卡住，需要加载中提示；底条收起符号显示方框，需要实际收起按钮。继续原PR目标，未被新反馈取消。

总览r=null时显示不接射线的LoadingNotice和24号数据加载中，附真实QueryStatus进度/PreparationError/等待原因，状态栏与底栏同步原因；ready后隐藏占位。图表无Current时ShowWaiting显示标题/加载文字、清旧曲线，数据恢复重置缓存重绘。不把加载假进度当完成百分比，也不宣称仅凭截图证明所有卡顿消失。

SDK NativePredictionBar把符号▾换“收起”，折叠态“展开”，按钮76宽、条990宽，原有折叠子控件行为不变。SDK主仓50154b9，公开SDK636a3d71cf91f67e164e221c0ee0a236ec1b9ae1，附加DLL/XML和源码文档示例构建检查通过；MOD兼容源码相同。

完整build122 C#、32 Node、9入口替身、53头像模型、60原生仙命布局替身、ILRuntime、Release0/0与MOD静态0/0通过；SDK/Loader Release0/0、SDK静态0error7旧warning；公开附加DLL/示例Release和IL0/0通过。ZIP CRC/DLL/厂商引用排除/控制字节[]通过。没有针对低风险文字变更新增镜像测试。

已有用户游戏37564运行，未操作/附加/关闭/启动、未覆盖安装档/配置/存档；加载占位和按钮实际显示仍待用户更新后验收。服务器未修改、正式Release清单保持。此修复包含在原版PR候选分支。
