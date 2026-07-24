# 《月根秘境》 / Moonroot Hollow

一款“农作物魔法 × 房间制 Roguelike”的原创 C# 像素动作游戏。

在被月光侵蚀的地下农庄中，用会生长、组合和收获的种子魔法清理随机房间，培育每局独一无二的战斗花园。

![《月根秘境》视觉方向稿](assets/moonroot-title.png)

## 项目状态

项目目前处于**可玩原型完成、春季垂直切片准备就绪**的阶段：

- Godot/C# 原型已经可以从标题界面完整游玩至 Boss 结算。
- 四季角色、场景、UI、武器、植物、陷阱、掉落物和特效的视觉源图与清单已经入库。
- 新增视觉资源主要用于占位、切分和生产参考，尚未全部接入当前原型。
- 固定网格角色动画、无缝 Tileset、字体授权和正式音频仍需在开发阶段完成。

## 当前可玩内容

- 5 个连续战斗房间，第 5 个房间为“灯笼南瓜王”Boss 战。
- 键鼠与基础手柄操作。
- 芽弹射击、翻滚和晨露圈技能。
- 播种、湿润加速、植物成长、自动攻击与范围收割。
- 泥团芽、刺萝卜、壳豆虫和 Boss 四类敌人。
- 房间奖励、资源拾取与三选一局内升级。
- 标题、HUD、暂停、失败和胜利界面。
- 程序化生成的复古游戏音效，无外部音频依赖。

> 当前战斗画面主要由 `Scripts/Main.cs` 程序化绘制；`assets/` 中的制作级视觉资源将从春季垂直切片开始逐步接入。

## 操作

| 行为 | 键鼠 | 手柄 |
|---|---|---|
| 移动 | WASD / 方向键 | 左摇杆 |
| 瞄准 | 鼠标 | 右摇杆 |
| 芽弹 | 鼠标左键 | 右肩键 |
| 播种 | 鼠标右键 | 左肩键 |
| 翻滚 | Space | A |
| 收割 / 进入根门 | E | X |
| 晨露圈 | Q | Y |
| 暂停 / 继续 | Esc | Start |
| 开始 / 重新开始 | Enter | A |
| 选择升级 | 鼠标或数字键 1–3 | 方向键 + A |

植物完全成熟后会自动攻击。靠近成熟植物按 `E` 可以收割，并对周围敌人造成范围伤害；湿润地块上的植物成长得更快。

## 快速开始

### 环境要求

- Godot 4.7.1 .NET（Mono 版本）
- .NET 8 SDK
- Visual Studio 2022、JetBrains Rider 或其他兼容 C# 的 IDE（可选）

先编译 C# 项目：

```powershell
dotnet build .\Moonroot.sln
```

然后使用 Godot .NET 打开仓库根目录中的 `project.godot`，按 `F6`/`F5` 运行；也可以通过命令行启动：

```powershell
& "C:\path\to\Godot_v4.7.1-stable_mono_win64_console.exe" --path .
```

如果 Godot 已按本仓库构建脚本约定放在 `tools/godot/editor/`，可以执行自动冒烟测试。测试会运行约 12 秒并自动退出：

```powershell
& ".\tools\godot\editor\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe" `
  --headless --path . -- --smoke-test
```

成功时控制台会输出 `SMOKE_TEST_OK`。

## Windows 构建

构建脚本要求：

- Godot .NET 位于 `tools/godot/editor/Godot_v4.7.1-stable_mono_win64/`。
- 已安装对应版本的 Windows 导出模板。
- 需要生成安装程序时，额外安装 Inno Setup 7。

执行 Release 导出：

```powershell
.\Scripts\build-windows.ps1 -Configuration Release
```

同时生成安装程序：

```powershell
.\Scripts\build-windows.ps1 -Configuration Release -Installer
```

产物位于：

- `builds/windows/`：Godot Windows x64 导出目录。
- `builds/installer/MoonrootSetup-x64-v0.1.0.exe`：简体中文 / 英文安装程序。

`tools/`、`.godot/` 和 `builds/` 均为本地生成目录，不纳入版本控制。

## 视觉资产说明

仓库已经包含四季角色与 Boss、四季战斗房和 Boss 房、春季商店与温室、UI、弹幕、近战、激光、预警、掉落物、交互物、四季陷阱、植物、武器、遗物和生态特效等资源。

资产接入时请以清单中的路径、用途和切分说明为准：

- [`assets/characters/character-assets.json`](assets/characters/character-assets.json)：角色、敌人、Boss、NPC 与动画关键姿势参考。
- [`assets/environments/environment-assets.json`](assets/environments/environment-assets.json)：四季场景与春季模块化场景参考。
- [`assets/gameplay-visual-assets.json`](assets/gameplay-visual-assets.json)：UI、战斗、交互、植物、武器、遗物与特效图集。

需要特别注意：

- 角色主图是设计母图或运行时占位图，不是固定网格逐帧动画。
- 春季模块板是 Tileset 生产参考，不是可直接使用的无缝 Tileset。
- 大图集需要按清单重打包或显式设置 `AtlasTexture` 区域，不能直接假设为等分 `Hframes`。
- `source/` 下的色键中间图由 `.gdignore` 排除，仅用于后续重切和清稿。

## 项目结构

| 路径 | 内容 |
|---|---|
| `Scenes/Main.tscn` | 当前主场景 |
| `Scripts/Main.cs` | 原型玩法、绘制、输入、UI 和程序化音效 |
| `assets/` | 视觉资源、源图与资产清单 |
| `docs/` | 游戏设计、数值、美术、技术与开发准备文档 |
| `Scripts/build-windows.ps1` | Windows 导出与安装包构建脚本 |
| `installer/Moonroot.iss` | Inno Setup 安装脚本 |
| `export_presets.cfg` | Godot Windows 导出配置 |

## 文档索引

### 核心设计

- [游戏设计文档](docs/游戏设计文档.md)：产品定位、核心循环、战斗、成长、关卡与制作范围。
- [多房间关卡与战斗扩展设计方案](docs/多房间关卡与战斗扩展设计方案.md)：四季关卡、地图、Boss、武器、存档、难度与排行榜。
- [首版内容与数值草案](docs/首版内容与数值草案.md)：角色、武器、种子、敌人、遗物、房间和基础公式。
- [美术与 UI 规范](docs/美术与UI规范.md)：像素规格、色彩、角色、场景、动效和界面布局。

### 资产与开发准备

- [开发前准备审计报告](docs/开发前准备审计报告.md)：当前准备度、已补缺口、开发门槛和建议接入顺序。
- [开发前视觉资源准备清单](docs/开发前视觉资源准备清单.md)：UI、战斗、交互、陷阱与春季垂直切片资源。
- [角色视觉资产清单](docs/角色视觉资产清单.md)：四季角色、敌人、Boss、NPC 与动画生产建议。
- [关卡背景视觉资产清单](docs/关卡背景视觉资产清单.md)：四季场景交付范围与 Godot 导入建议。
- [开发前补全资产生成提示词](docs/开发前补全资产生成提示词.md)：新增资源的生成、派生与风格回归提示词。

### 技术方案

- [C# 桌面版可行性与工具选型](docs/CSharp桌面版可行性与工具选型.md)：引擎选择、开发环境、Windows 导出、安装包和技术风险。

## 下一阶段

1. 接入春季战斗房、莱芽静态占位、芽枝杖、4 种植物和基础弹体。
2. 完成“播种—成长—收割 / 留根—跨房影响”的春季核心循环。
3. 接入春季敌人、精英、商店、温室、安全存档和基础结算。
4. 将角色、场景与大图集清稿为可直接运行的动画和 Tileset。
5. 验证春季 9–11 房垂直切片后，再扩展夏、秋、冬三季。

## 第三方说明

第三方组件及相关说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
