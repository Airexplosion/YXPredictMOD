# 0.5.1 随机加入开放房间（2026-10-24）

用户要求随机加入已经开赌的房间，并授权备份/安装默认档、启动Steam游戏验证，确认Steam已登录且游戏关闭。沿用独立不可兑换娱乐积分及原生入口约束。

服务端新增 random 动作：仅选择房主心跳15秒内、交易开放、未满32人、未结束、已有投入、携带原生游戏房间ID的房间。使用crypto.randomInt抽取；只返回code/gameId/gameRoomId/round/members，绝不返回身份凭证，不加入成员、不扣分。加入仍核对实际gameId；旧客户端房间不带gameRoomId时不可随机发现。游戏房间ID由房主sync补入，填入后不可替换。

客户端大厅右侧“联机预测”及房间面板“随机加入/取消匹配”。通过XianFuLobbyPanel.Show + SpectateRequest(string)走原生按钮相同面板流程，保留游戏原生确认、准备/队伍检查、权限、异步观战。未调用原生协议发送入口，不直接切场景。离开当前对局须用户通过游戏原生界面返回大厅。90秒内取得相同gameId、完整八人、未结束、实时观众快照才提交join，成功后打开预测面板；取消/超时不加入、不自动投入。

重新提取当前游戏refs，HotUpdate ef5544c18c3a；仅在本机refs检查原生签名。新增LucidSight.Runtime.ColyseusSDK本机编译引用及prepare-game恢复步骤；该厂商DLL未随包提交或分发。

验证：120 C#回归、9项实际NativeRoomSpectate源码入口替身检查、ILRuntime探针（含RoomMatch）、net40 Release 0/0、SDK warn-as-error 0/0；16 Node测试在本机和生产Node22均通过。公网HTTPS随机发现→未加入成员→错误对局拒绝→正确对局加入且余额1000→最终结算→已结束不再匹配通过；合成自检房间已结算，原认证与房间healthz均200。

生产部署更新到C:/yx-prediction，备份backup-051-random，数据保留，原认证进程/数据库不改。正式Release清单未修改。

实机：首次脚本RPC抢跑，第二次启动在MOD加载前超时；两次均清理测试进程。第三次成功加载0.5.1并登录Lobby，大厅launcher可见、面板1140x650打开、随机按钮得到“暂无房主在线、已有人投入且可加入的开放房间”；原生SpectateRequest打开原生确认，CancelButton取消后仍在Lobby。随后游戏关闭，无残留YiXianPai.exe。证据.cache/game-smoke-051/run-1792858716213/evidence.json；临时探针只在.cache中，不进入发布包或默认档。没有可供实际跨局的在线房主，因此“原生进入另一真实对局→自动join”完整链路尚未实测；也不以组件可见替代字体外观验收。

默认档已备份到.cache/install-backup-051，安装并启用local.yxp.predictionpoints 0.5.1；其他MOD启用项、配置、余额与数据未修改。DLL与安装档逐字节哈希一致，ZIP CRC/manifest/DLL/厂商引用排除/控制字节[]通过。包dist/local.yxp.predictionpoints-0.5.1.zip，512867字节，SHA256 8da7375db0d33eac22e4bce127c59907734d79e10c4b69a21080a3eef52e4184。
