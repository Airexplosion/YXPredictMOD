# 0.7.1 恢复图2原生仙命槽位（2026-10-24）

用户图1为当前错误：图标被缩小并聚在人物上方及右侧；图2指定原生围绕人物左侧的境界槽位。本次移除自定义角度环绕、0.62 wrapper与额外横排名，不再重复自制几何。

重新yx-patch refs，当前HotUpdate ef5544c18c3a；本机反编译核对BattleTalentIconItem.Zoom(bool)/baseScale/basePos及PlayerSelfInfoItem现有原生列表。UnityPy从当前206108f646435a501aef17a727259acb.bundle的PlayerSelfInfoItem预制体读取Native BattleTalentsLayout/TalentRoot/Talents和六槽锚点/位置/缩放：以左下270,182的528容器为基准，使用原生128图标及六个逐境界缩放。原始预制体数据仅refs/talents-prefab-current.json与本机refs代码，未提交或分发厂商材料。

自己的仙命界面不再改坐标/缩放/名称，由原生BattleTalentsLayout处理。仅对可见观战OpponentInfoPanel已初始化m_Talents列表应用相同原生收起槽位；保留原顺序与锁定空槽，不因隐藏跳过导致后续境界前移。无图标reparent、新GameObject、包装节点、自制caption或新字体。调用图标自身Zoom(false)，隐藏其原生展开名称并使用原生baseScale，悬停说明仍走原入口；basePos用世界坐标。保存容器/图标所有rect属性、baseScale/basePos/原m_IsZoom、LayoutGroup状态，离开、战斗、停用、实例切换还原，不改协议或游戏状态。

NativeTalentLayout生产源码直接编入离线替身测试，60检查覆盖六槽位置和缩放、父级不变、原生刷新Zoom后收起、重复应用/恢复无漂移、换面板恢复、空槽不改变序号、不碰自己布局、非观战/进入战斗/停用恢复。完整build122C#、32Node规则、9原生入口、53头像模型、ILRuntime、Release0warning0error、MOD静态0/0通过。ZIP CRC/DLL/版本/厂商引用排除/控制字节[]及无SetParent/自造caption/角度计算检查通过。不能把离线几何与替身当作真实渲染验收，未声称像素级一致。

用户已有YiXianPai35556在运行，本轮未启动/附加/操作/关闭，也未覆盖当前安装档、配置或存档。0.7.1安装后需重启，旧版已改过的实例不做热恢复。新外观待用户实际确认；服务器、SDK、原生UI其他部分/权益规则无修改，正式Release清单不改。
