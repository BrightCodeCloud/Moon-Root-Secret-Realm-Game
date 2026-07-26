# 《月根秘境》 / Moonroot Hollow

一款“农作物魔法 × 多房间动作 Roguelike”的原创 C# / Godot 游戏。

![《月根秘境》视觉方向稿](assets/moonroot-title.png)

## 当前可玩版本

当前版本严格对应设计文档的“春季垂直切片”，用于先验收地图探索、即时战斗、种植、收割/留根与 Boss 继承：

- 使用《多房间关卡与战斗扩展设计方案》3.2 节的示意地图拓扑，共 12 个节点。
- Boss 未抵达相邻房前显示为“未知根结”；无需钥匙；已清房不刷新敌人。
- 入口、战斗、事件、温室、商店、宝藏、精英、隐藏和 Boss 房均已接入。
- 莱芽、4 类春季普通敌人、苔冠刺萝卜精英与灯笼南瓜王使用 `assets/` 中的正式母图。
- 春季战斗、商店、温室和 Boss 房使用对应场景资产。
- HUD、菜单、月根地图、挑战状态、武器、植物、遗物、陷阱、交互物和 VFX 图集均在运行时绘制。
- 3 把主工具：芽枝杖、月牙镰、日棱喷壶。
- 4 种种子：豌豆、辣椒、南瓜、蒲公英；包含成长、成熟攻击和收割效果。
- 晴暗、小雨、月隙三种天气，以及湿润、肥沃、月照、腐化土地。
- 清房后必须在立即收割和留下根忆之间选择；根忆会改变相邻房间、路线与 Boss 场地。
- 4 个生态配方、12 个春季遗物、5 类房间契约和三档难度参数入口。

按照设计文档 13.2–13.3 节，完整四季、美术横向扩充和困难 Boss 第二形态不属于春季切片验收范围；不会在本 README 中把它们标记为已完成。

## 操作

| 行为 | 按键 |
| --- | --- |
| 移动 | WASD / 方向键 |
| 瞄准 | 鼠标 |
| 主工具攻击 | 鼠标左键 |
| 播种 | 鼠标右键 |
| 翻滚 | Space |
| 晨露圈 | Q |
| 处理植物 / 进入根门 | E |
| 切换主工具 | R |
| 切换种子 | 1–4 / 鼠标滚轮 |
| 查看地图 | Tab |
| 地图选择路线 | A/D、方向键、Enter |
| 为目标房切换契约 | K |
| 暂停 / 返回 | Esc |

配装界面使用 A/D 选择主工具、W/S 选择主种子、K 切换难度、Enter 出发。

## 开发、测试与导出

环境要求：.NET 8 SDK 与 Godot 4.7.1 .NET。

```powershell
dotnet build .\Moonroot.sln
.\tools\godot\editor\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe --path .
```

自动走完多房间流程并击败 Boss：

```powershell
.\tools\godot\editor\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe --headless --path . -- --design-smoke-test
```

成功时输出 `DESIGN_SMOKE_TEST_OK`。

导出 Windows x64：

```powershell
.\Scripts\build-windows.ps1 -Configuration Release
```

导出目录为 `builds/windows/`。

## 项目结构

| 路径 | 内容 |
| --- | --- |
| `Scenes/Main.tscn` | 游戏主场景 |
| `Scripts/Main.cs` | 春季切片的流程、战斗、种植、UI 与测试入口 |
| `Scripts/Design/DesignContent.cs` | 设计枚举、房间状态与示意地图拓扑 |
| `Scripts/Design/RuntimeAssets.cs` | 运行时视觉资产注册表 |
| `assets/` | 角色、Boss、场景、UI、玩法图集、VFX 与音频 |
| `docs/` | 游戏设计、数值、美术、技术和开发审计文档 |
| `Scripts/build-windows.ps1` | Windows 导出脚本 |

## 设计文档

- [游戏设计文档](docs/游戏设计文档.md)
- [多房间关卡与战斗扩展设计方案](docs/多房间关卡与战斗扩展设计方案.md)
- [首版内容与数值草案](docs/首版内容与数值草案.md)
- [美术与 UI 规范](docs/美术与UI规范.md)

第三方组件及相关说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
