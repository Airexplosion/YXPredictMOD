# 0.7.0 Elo分帧、基础倍率系数、单人规则对齐、走势图与入口（2026-10-24）

连续用户要求：Elo查一次卡一下，恢复原走势图；多人必须有基础倍率×投注修正系数、投注仅影响下回合系数，其他规则和原单人相同，明确包括差一名25%；删透视，恢复截图右上角“预测总览”入口。全部纳入本轮，未因最后的入口问题丢弃前面工作。

Elo：UnityWebRequest原本异步，但完成后主线程整份Json.ParseObject导致风险。新EloShardScan以256字符块扫描top-level UID对象，约1ms软预算后让出，只解析已请求UID选中行，单行16KB上限、分片2MB上限，嵌套64上限；解析单条后让出。缓存HorseProfile，UID缓存256、分片缓存16，换名单/同后缀新UID补查。仅Unity主线程进入热更域，无ThreadPool/async/跨线程ILRuntime解释；响应UTF解码与单条JSON解析仍同步，1ms非硬实时承诺。真实00分片样本约141895字节283人，下载只在.cache本机调查，不提交。未实机测帧耗时，不宣称根治全部卡顿（首次马图/UI仍可能有耗时）。

规则：新房间snapshot携带当前本地单人QuoteRates作为baseRates（UID映射）；rules2基础矩阵与每轮冻结factors，quote=base*factor。不可能base0保持0，其余与单人1.01..50上限一致。投注系数clamp(1+0.25*(1-8*该名次持仓成本/该选手总持仓成本),.75,1.25)，零投入1；本轮投注不改本轮系数，下轮用未resolved的剩余成本计算。基础算法因公开局势刷新仍可当轮更新，系数不跟着变化；已买权益锁定。

其他规则对照单人：Buy floor本金×报价权益，增持累加；Half移除floor权益/2，Sale按当前报价折算权益后97%，按原比例释放成本；Switch失败原子回滚，次数每账户每选手512持久化；正中全权益、相邻floor权益/4、不环绕、名次确认逐人结算且最终不重复，余额上限1e12。仅剩一人停止交易；零价值清仓允许但返0。rules1旧房间保留旧资金池承诺，旧房间不会因主机更新自动转换。UI标明规则；新规则各方更新0.7.0使用新对局。余额不合并。

测试：tests/RuleParityFixture用真实C#单人账本导出4组共32步操作与最终结果，server/rules.test逐步对照Balance/Claim/Cost/Rank/Resolved/派奖，覆盖买/增持/退半/清仓/改猜/失败；build自动重导hash防漂移。额外测试下一轮生效/基础同轮刷新/旧权益冻结、早淘汰/相邻25/重复派奖、零报价、512与重启、旧规则保留等。32 Node测试本地及生产Node22通过；122 C#、9原生入口替身、53头像模型、ILRuntime含Elo分片多budget/转义/坏envelope和rules2响应解码通过；Release0/0、MOD静态0/0。这些不等于全部实际对局已验收。

服务器备份backup-070-single-parity与backup-070-final-single-survivor，独立C:/yx-prediction更新新增odds.mjs、fixtures与规则测试，认证数据/进程不改。公网两身份真实HTTPS:base4x零投入仍有倍率、买100锁400，次轮factor.75报价3x、旧权益400不变、退半返64、正中剩余200、相邻返100、重复同步不重派奖；最终单存活封盘专项也通过，两healthz200。所有合成自检房间已结束。未迁移已有房间规则。

UI：恢复原LifeChart池化线段显示，统一总览三图命元/命元占比/剩余换牌，共同八人坐标，前后切玩家，只用本客户端实际收到的历史。倍率列独立显示quote，右侧base×factor，最终行显示返还。删除MOD透视按钮/动作（SDK通用SetPeek仍供别的MOD）；恢复PredictionOverviewEntry右上角预测总览，和下注条总览同一RoomPanel.Toggle。历史多局不恢复，旧UI SDK无改动。

用户已有游戏35556，本轮未启动/附加/操作/关闭或覆盖安装；走势图/右上角入口外观、实际多人整局与Elo帧耗时待实机验收。正式Release清单不改，交付dist/local.yxp.predictionpoints-0.7.0.zip。
