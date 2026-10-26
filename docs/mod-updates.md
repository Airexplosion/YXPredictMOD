# 管理器不显示更新的排查与修复

## 2026-10-25 当前状态

天机赌马已加入管理器默认公共清单 `https://raw.githubusercontent.com/Airexplosion/yixianpai-mods/main/mods.json`，指向上游正式版0.7.9及其完整性校验值。无需额外添加仓库，刷新「浏览」即可查到；已安装0.7.6可检查更新。上游0.7.10目前仍为测试包，没有推成默认正式更新。

以下为此前0.3.6发布时的历史排查记录；其中“未进入默认公共清单”的结论已被上述更新替代。

## 实际原因

本机管理器1.0.2安装的本模组为0.3.4，updateUrl正确指向本仓库。检查时GitHub最新正式Release仍为v0.2.9：0.3.0～0.3.6此前仅将源码和测试ZIP提交到main，没有更新正式发布指针。因此GitHub返回的版本比本机旧，管理器不会显示可更新。

截图中的「浏览」只列出设置repos中的mods.json。本机仅配置默认公共清单`https://raw.githubusercontent.com/Airexplosion/yixianpai-mods/main/mods.json`，该清单未收录本模组；仓库公开、配置updateUrl或发布Release本身不会自动加入它。[公共清单的格式与收录说明](https://github.com/Airexplosion/yixianpai-mods#readme)也明确要求新增清单项。

另外，旧release.yml只允许manifest、DLL、README三个ZIP文件，拒绝整合马图后的十一文件包；标题还固定写旧版的淘汰结算修复。本次修复此发布阻碍，不改已验证的0.3.6游戏DLL或安装ZIP。

## 本次修复

发布指针改为v0.3.6及已测ZIP的SHA-256；补齐对应版本说明和动态发布标题。`scripts/verify-release.py`明确允许七张马图和NOTICE，并逐一比对归档内的说明、图标和源码；说明文件仅归一化UTF-8 BOM和CRLF/LF，图片及整包继续严格检查字节和哈希。继续拒绝重复文件、未知文件、路径越界、错误校验值及manifest不一致。十项发布回归覆盖新布局、跨平台换行及故障情况。

根目录新增`mods.json`，与正式Release的版本、附件链接、作者、描述及哈希一致。工作流在发布前检查清单，避免以后清单仍指向旧版本或错误安装包；先上传草稿、下载附件复核SHA-256，再公开正式版并设置latest，原v0.2.9不覆盖。

## 在管理器里查看

- 旧版已安装：在「已安装」点击「检查更新」，从Release取得高于本机的版本。
- 要显示在「浏览」：在「设置 → 在线仓库」新增`https://raw.githubusercontent.com/Teraen/YXPredictMOD/main/mods.json`，保留默认清单，再刷新「浏览」。
- 尚未进入默认公共清单。加入默认源需要向其维护者提交收录变更，本次没有代替用户联系维护者或修改第三方仓库。

本次没有改动本机管理器设置或正在使用的MOD文件；只读核对了设置、已装manifest和管理器程序中的更新入口。未声称已经在管理器UI实测下载/安装。

## 后续正式版本

先完成源码检查和打包，再同步修改`.github/releases/latest.json`及`mods.json`的版本、标签、下载地址和哈希，提供版本说明。执行：

```powershell
python -B -m unittest discover -s tests -p test_release_package.py
python -B scripts/verify-release.py
```

正式发布工作流只在发布指针改变或手动触发时运行，普通源码同步不会替代正式发布。GitHub Actions成功后，核对`releases/latest`返回的新版本以及附件哈希，再通知用户检查更新。
