# 0.5.0 工作记录（2026-10-24）

用户确认联机使用独立、不可兑换认证时长的娱乐积分；仍猜最终名次，每回合允许调仓，整局结束派奖。未实现认证时长换算。

实现：独立 Node 22+ 房间服务，八个选手各八名次资金池；服务端余额、幂等操作、版本校验、完整名次唯一结算、持久化回滚、15秒房主心跳暂停。游戏主线程轮询 UnityWebRequest，保存凭证和待确认操作，独立房间窗口入口在总览右上角及 Ctrl+Shift+P。规则见 README。

UI：左下双列仙命、观战/战斗限制和位置恢复；避免初始化式读取 ReadyLayer；仅主体透视、保留顶栏按钮；下注条真实折叠、拖动防误触、所见玩家/对局校验；手牌世界边界避让、无空间折叠；总览/下注条125%上限和小屏适配、统一字号/长文处理。选取 upstream de78a72（0.3.11）的圆形马图、黑色字形描边、焦点/悬停覆盖及电子马命名，保留本地0.4原生风格UI，不整棵覆盖上游。

验证：119 C# checks；官方 ILRuntime probe（含真实 Node 响应解码和操作 Guid）；当前游戏 refs HotUpdate ef5544c18c3a；net40 Release 0 warning/error；yx-patch warn-as-error 0/0；ZIP CRC/manifest/DLL字节一致/控制字节[]；12 Node tests 在本机与生产机 Node22 均通过（HTTP双客户端、落盘重启、重复操作、封盘和回退等）。未启动游戏、未操作鼠标键盘，原生字体、实际遮挡与真实完整对局尚未验收。

部署：使用用户指定的现有认证服务器，独立 C:/yx-prediction、计划任务 yx-prediction、回环8123、现有域名 /prediction/ 前缀反代。认证数据库/代码/密钥不变，无认证接口调用。nginx备份 C:/yx-prediction/auth-vhost-before.conf；新服务后续源码备份 backup-050-heartbeat。公网两身份共同资金池、回合封盘重开、1300/700最终余额和重复结算验证通过；更新后无变化心跳不废弃报价验证通过；两healthz均200。产生两场已完成的合成部署自检房间，无真实对局数据。

交付仅源码与测试ZIP同步现有origin/main；正式 .github/releases/latest.json 未修改。安装包 dist/local.yxp.predictionpoints-0.5.0.zip，SHA256 773483c35a1d1896c39af565b4872697f46eda7f9d666ca6bfd5740572b1b514。
