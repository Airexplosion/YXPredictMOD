# 0.6.0 统一预测界面、SDK原生样式与入房提醒（2026-10-24）

本轮用户要求：仙命围绕左侧人物按境界固定位置；单人与联机同入口同UI；删除多局/历史/走势图；操作按钮原生样式；删除差X分/参考清仓长句；修复数字只能10。后续明确要求原生UI调用进入SDK，以及成员进入显示游戏名/ID提示。

实现：移除PredictionPanel/PredictionLauncher/LifeChart游戏UI源文件，保留既有账本与待结算数据。RoomPanel作为唯一总览界面，对当前本地盘口或当前服务器房间返回共同RoomState视图并路由Submit；角色下注条重写为同一视图/提交入口，仅总览快捷键，无走势图，输入框不再刷新回旧草稿，点击时直接解析1–10000整数，切换身份重置草稿，不自动以10兜底。输入长度5、样式22号；表格草稿按房间/对局/UID复位。

仙命TalentOrbit：只在实时观众修炼阶段调整，读取现有原生列表与talentLevel；独立父节点缩放、境界固定角度、同境界外扩；原生m_NameLabel所在名称容器隐藏并由MOD横排名替代。注意basePos在当前游戏为世界坐标，未再写入localPosition。退出/战斗/停用还原原父级、局部位置/缩放、basePos及名称显隐。该环绕为候选布局，角色/分辨率及大量同境界多仙命尚未实机验收，不能声称不遮挡。

SDK：主仓库新增sdk/Yx.ModSdk/Unity/NativeControlStyle.cs及docs/dev/native-control-style.md，本地独立提交63e39872c5fa9126cf816150e9685598cc2812d3，只提交这两新文件，保留主仓其他既有脏改动。Button/Input/Frame仅复制视觉、保留目标监听和当前输入，不克隆游戏业务脚本。SDK/Loader Release0/0，SDK静态0error/7个现有warning。MOD的sdk-extensions源码兼容副本与SDK逐字节一致，编入MOD以支持玩家现有旧SDK，未要求替换运行时；未分发SDK/游戏DLL。SDK主仓该提交仅本地，未推送或公开发布SDK。

成员：认证请求可带客户端自报游戏name/uid，变动才保存。view仅对房间成员返回memberDetails：公开非Bearer标识、名字、UID；lookup/random无成员信息。客户端按稳定公开ID去重，新会话首次初始化不弹旧成员，再次刷新不重弹；新成员通过原生HintMessagePanel及房间底部显示。ID并非官方独立验证，不把自报身份描述为账号认证。旧客户端暂无游戏UID时退回公开成员编号。

服务器已独立更新，备份C:/yx-prediction/backup-060-member-names，原余额和认证服务不改。19 Node测试本机与生产Node22均通过；公网合成两身份测试游戏名/ID、重复加入不新增、无Bearer泄露、最终结算和原认证healthz200通过。自检房间已结算。

检查：120 C#回归、9既有原生入口替身、ILRuntime探针（含新增成员响应解码）、Release0/0、MOD静态0/0、ZIP CRC/DLL/manifest/厂商引用排除、SDK共享源码一致、控制字节[]通过。UI专属改动不由这些离线测试替代实机验收。

用户游戏进程14148在本轮运行；未附加/操作/关闭、未覆盖默认或321安装档/配置/存档。新的统一UI、真实键盘输入、环绕与成员弹窗未实机。正式Release清单不变；交付测试包dist/local.yxp.predictionpoints-0.6.0.zip，502280字节，SHA256 0719143e6fd8e948f036f7f2ede56e6934721a2306f353ee298d8efca8c9a410。
