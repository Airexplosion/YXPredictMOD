# 原始 UID Elo API

本目录是独立的只读数据适配器、OpenSkill 计算器与 API。它不登录游戏、不加入观战、不发送游戏请求，也不修改身份采集服务。部署在赌马同服务器，域名 `https://elo.kaigua.vip`；根路径返回 JSON，无 Elo 网页。

## 数据与边界

- 初始状态使用原项目公开的 [elo-state-seed](https://github.com/raymondwrw/yxp-card-stats/releases/tag/elo-state-seed)，2026-10-23 附件，SHA256 `6a180fdfc218565f85c9371eb3b71d05b8206d8cd11d3cc4e51f0dfcf5c80398`。115,567 个原始24位UID，包含完整 mu/sigma；不是从网页取整分数反推模型。
- 旧项目当前16位标识是私密密钥 HMAC-SHA256 截断，不是普通 SHA256。未拿到原密钥前，不将其冒充原始UID，也不按昵称匹配。种子后的新数据来自已确认的 UID↔复盘匿名位置及本人积分证据。
- `export.py` 对身份库开启 `mode=ro`、`query_only` 和同一读事务。仅发布身份凭据与当前报告逐字段一致、无冲突、同局至少两名真人的记录。最终名次来自已确认的 `record-player-N` 位置，不从根 `battleRank`、命元或资料分猜测身份。
- 合并普通天梯/道心到同一 OpenSkill Plackett-Luce 模型；每次从固定种子按对局结束时间重建，迟到、修正和撤回证据不会重复计分。机器人不进入模型；单真人局不更新，与原模型口径一致。
- 这是已观测且可确认的记录，不代表全服、全历史或所有私密房覆盖。源暂未提供赛季ID，不推测新赛季，不凭日期添加赛季重置。API明确公开来源时间与覆盖不完整。
- 历史种子、事实导出、运行时评分与私钥仅在私有数据目录，不提交仓库。

## 接口

`GET /api/v1/ratings?uid=<24位小写hex>&uid=<另一UID>`：最多8人，返回按原始UID索引的资料字典。缺失者不返回，不能把默认3000当作已收录。MOD一次查询完整八人名单。

`GET /api/v1/status` 或 `/healthz`：模型、种子时间、源时间、已更新局数、过期状态与覆盖说明。

`GET /data/elo/p/<末两位>.json`：原始UID旧版MOD兼容接口。服务不提供静态站点页面。

## 运行

在独立虚拟环境安装 `requirements.txt`。`api.py --snapshot <snapshot.json>` 默认只监听回环8124，前置 Caddy 提供 HTTPS 和自动续期。

`update.py --seed <seed.json> --source-command <command.json> --output <snapshot.json>` 使用配置中的固定 argv 获取只读导出。生产通过专用、来源IP限制、forced-command SSH密钥运行 `export.py --database <身份库>`；该密钥没有交互shell或转发权限。不要复制采集器票据或原管理员私钥。建议独立 systemd oneshot 与15分钟 timer，失败保留上一完整快照。

`source-command` 是服务器私有配置，不接收网页参数。源库和采集程序不因本服务而启动、停止或更新。种子导入应先核对上述 SHA256，并使用禁止 `find_class` 的 data-only pickle 读取器；不要对未知文件直接执行 `pickle.load`。

验证：`python -m unittest discover -s server/elo -p test_elo.py`。原模型与 API 参考：[原项目说明](https://github.com/raymondwrw/yxp-card-stats/tree/main/pipeline/elo)、[OpenSkill 官方文档](https://openskill.me/en/v6.1.3/manual.html)。计算适配器为本项目独立实现，未复制原项目Python代码。
