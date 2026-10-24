# 0.6.4 完整旧窗体恢复及SDK公开（2026-10-24）

用户指出0.6.3仍不是完整旧UI，要求恢复旧个人赌马界面并把原生UI相关代码进入SDK和GitHub。核对当前321确实安装0.6.3，问题不是漏安装，而是此前只移植旧BetRow，窗体/顶部仍自绘重做。本次以d1ebd17的完整原PredictionPanel层次重建SDK控件：1140x624、54高原生标题横幅、12/-54主体内衬、原七列表头、-32起58行距、底栏、关注高亮、马牌图文、赔率列和下注控件。MOD标题工具栏保留详细、马牌、透视、折起、房间、离开、关闭；删除的历史/走势图/差X分提示不回归。

新SDK NativePanelTheme只从调用者给的已初始化Transform采集原生样式，不引用游戏业务；NativePredictionWindow统一窗体/Peek/Minimize/标题拖动；NativePredictionRow为旧行的构造代码，暴露控件句柄，不读游戏状态、不算赔率、不处理网络。MOD仅提供数据与动作，原NativeTheme缩为游戏子树选择适配器，旧视觉代码不再留在MOD双维护。未持仓默认恢复未选名次—，提交必须选择1–8，输入不在刷新时覆盖。单机/联机余额、成员提醒、已有房间查询和所有账本数据保留。

SDK主仓本地与sub1提交ee1f93351b1444463eb92eef503b2ceabbe3b402，仅新三个SDK源及说明，主仓其他脏改动保留。公开Airexplosion/yixianpai-mod-sdk main提交e61c5e8a5bc0755b2612bedf2cd7a73bc3ddae7b，发布source/Unity四源、SDK附加Yx.ModSdk.ClassicUi.dll+XML、构建工程、完整可编译示例和用法；没有替换既有SDK二进制、不分发游戏或Unity DLL/厂商反编译/马图/用户数据。附加DLL仅自有UI代码，消费者通过libs加载，旧管理器可用。MOD临时同源源码兼容四份与SDK/public仓逐字节一致，未同时引用附加DLL。公开DLL匿名raw下载SHA256 a7655c8cc0395f9895b493d5f6e14a19b1976b7079df0c2fd8a504a76b3a1596与本地产物一致。

检查：MOD完整构建120核心/9入口替身/53悬停模型/ILRuntime/Release0warning0error/IL0error0warning通过；公开附加DLL和示例独立使用公开原SDK编译0/0，IL0/0；主仓SDK/Loader Release0/0，SDK既有0error7warning。包CRC、版本、DLL字节、厂商引用排除、SDK四源同一、控制字节[]通过。未进行真实游戏渲染验收，不能称像素完全一致；原生资源最终外观仍待用户重启确认。

本轮检测游戏已关闭后，备份并更新当前321档MOD至0.6.4，DLL哈希逐字节校验；仅替换MOD二进制/manifest/README，不改积分、身份凭证、配置或默认档。未启动游戏、未附加或操作鼠标键盘；服务器逻辑没改无部署、正式Release清单不改。

包dist/local.yxp.predictionpoints-0.6.4.zip，507307字节，SHA256 9a117f2abb9b2f107d546719372ec800635eef22da38f9182a67f85185c82152。
