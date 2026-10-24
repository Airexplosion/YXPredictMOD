# 0.6.1 暗夜主题与上游0.3.13头像悬停（2026-10-24）

用户指出白底浅金字不可读，要求暗夜模式，并要求合入上游鼠标悬停查看人物头像的反向行为。

原因：原生ScoreboardPanel的Window贴图包含整张白纸，并非只有边框；直接复制到暗色主题窗体导致不透明白底。ApplyWindowFrame改为无贴图、默认材质、alpha=1的暗夜底。菜单、总览和下注条共用；输入框同样固定深底。原生按钮外形仍保留，但最终文字/背景/禁用和悬停颜色统一为暗夜高对比色，避免原生浅金字继承到白底或浅按钮。表头从空格拼接改成五个独立列，加入交替行底色和单独成员文字颜色。不改交易、积分和服务器逻辑。

上游fetch确认Teraen/YXPredictMOD main=3dcffe3（0.3.13），新增788af2f与3dcffe3。仅合入HorsePlateLayout和TianJiFeature中模式配置/UpdateIcons/PositionPlate悬停边界部分，保留本地FindMember/GetSprite/ColorOf接口和只读ReadyField，未覆盖本地下注UI。默认马图、悬停或焦点显示人物；iconsOnFocus=true反向，Ctrl+Shift+H保留既有hotkey ID，新配置不读取旧alwaysCover。热区覆盖头像/ID/称号和空白、重叠列取最近；无新增挡点击控件。

验证：120 C#、9原生入口替身、53头像模式/热区模型检查、ILRuntime探针、Release0warning/0error、MOD静态0/0、ZIP CRC/manifest/DLL字节/厂商引用排除及控制字节[]通过。模型源RectModel来自上游tools/UiLayoutChecks，只模拟二维坐标，不模拟真实字体或游戏渲染。

用户游戏进程23604正在运行，未操作/附加/重启/关闭、未覆盖默认和321安装档。新暗夜外观、悬停范围实机验收仍待用户更新重启。未更新服务器或正式Release清单；测试ZIP与任务源码按项目约定同步origin/main。
