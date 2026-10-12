# Dice Hero — Spine 角色预览与换装

## 三人小队训练场（2026-10-12）

打开 `Assets/Scenes/DiceTraining.unity` 并 Play：战士、游侠、法师组成固定三人小队，各自自动攻击训练木桩。旧骰子操作已从入口移除。支持暂停、1×/2×、声音开关和重新训练；技能蓄能满后自动释放，木桩击破后自动进入下一轮。木桩不反击。

场景由前景、立体近景、中景、远景四组独立对象构成。战斗台面保留足够纵深，前沿有明显岩壁落差；独立符文石和植物位于近景地形后，底部被遮挡。镜头轻跟随战士前进，各层以不同速度产生视差。全套环境已按简洁手绘方向重绘，以大轮廓和明暗面表现体积，减少碎叶、石纹和颗粒噪点。使用现有 Spine 角色，界面与游戏状态相连。

布局说明、素材来源和验证方法见 `Docs/PartyTraining.md`。更新前场景和脚本保存在 `Backups/BeforePartyStage/`。

团结引擎 **1.10.4**（内部版本 `2022.3.62t16`），Spine 数据 **4.2.43**。

## 打开运行

1. 在团结 Hub 中添加本目录，使用 1.10.4 打开。
2. 首次打开并完成脚本编译后会自动生成预览场景。打开 `Assets/Scenes/HeroPreview.unity`，点击 Play。
3. 如果首次导入尚未生成场景，点击菜单 **Dice Hero → Create Preview Scene**，再点击 Play。

左侧选择职业，中间预览角色，右侧在“职业动作”和“外观搭配”之间切换。切换职业自动装备推荐外观与武器，播放对应待机；动作列表只显示本职业动作，隐藏 backup 动作。PL 职业、PL 武器和 PL 盔甲不出现在界面中。

| 职业 | 武器系列 | 推荐外观 | 动作数（不含 backup） |
|---|---|---|---:|
| 盾卫 | SHW | 金色护甲、整齐短发、旅行背包 | 115 |
| 重刃战士 | THW | 红披风护甲、蓬松发型、背剑 | 117 |
| 影刃 | DB | 深色长衣、斜分发型、暗色翼饰 | 103 |
| 武士 | Katana | 红黑服装、束发、背剑 | 120 |
| 游侠 | Bow | 森林绿服装、马尾、旅行背包 | 91 |
| 枪手 | Gun | 深色外套、利落发型、护目镜、弹药背饰 | 83 |
| 秘法师 | Staff | 紫色法衣、长发、单片眼镜、卷轴背饰 | 101 |
| 破阵者 | FistDrill / D7 | 厚甲、竖发、机械眼饰和背饰 | 84 |

职业名称是本项目的设计命名，不是对资源英文目录的逐字翻译。预设集中在 `HeroProfession.All` 中，可修改具体皮肤与配色。以上外观参考源图集部件选择，尚未逐套检查运行时遮挡。

每个职业独立保存到本机 PlayerPrefs；本次运行中切换职业也会保留搭配。旧版混合职业存档不导入。“恢复本职业推荐搭配”恢复预设外观与待机。普通攻击和技能默认播放一次后返回待机，可以勾选循环预览。死亡动作停留在末帧，点击待机恢复。

界面使用中文，具体部件与动作保留原始资源编号便于定位。当前实现为职业角色预览与换装，不包含战斗数值、技能伤害或职业 AI。本次只核对预设资源引用，未启动引擎运行验证。

## 原始资源中的缺失部件

贴图按 Straight Alpha 导入：Bilinear 采样、sRGB 开启、Alpha Is Transparency 开启、关闭压缩和 Mipmap，材质启用 Straight Alpha Texture。原始 PNG 未缩放或重绘。

JSON 引用了 atlas 中不存在的 `A_commonAppearance/Dagger/Main` 和 `A_commonAppearance/mouth/mouth_B_015`。工程中的 `Assets/Hero/LC2_Hero.atlas.txt` 为它们添加了透明占位，沿用原 atlas 的 `A_commonAppearance/Body/null` 区域，避免骨骼加载中断。这两个部件暂不显示。根目录的原始 atlas 保持原样；补齐实际贴图后应替换工程中的 atlas 和 PNG，移除对应透明占位。

## 文件

- `Assets/Scripts/HeroWardrobe.cs`：动态分类、组合皮肤、动画和预览控制。
- `Assets/Editor/WardrobeProjectSetup.cs`：贴图设置、生成 Spine 资源和场景。
- `Assets/Hero/`：角色资源副本，根目录原文件保留。
- `Assets/Spine/`：官方 Spine 4.2 运行库。

运行库来源：https://github.com/EsotericSoftware/spine-runtimes/tree/4.2

源码提交：`e7dc1435fa4a0083ab431f1b28e083c14a1f5c68`。许可保留在 `Spine-Runtime-LICENSE.txt`。
