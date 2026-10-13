# Yuen-Zoku Cutscene Skipper

[English](README.en.md)

## 1. 这是什么

本插件面向 **淫縛の館**（Yuen-Zoku / 同人社团 DoubleSoftCream）的 **剧情演出与进度** 相关功能，
提供过场跳过、对话快进，以及一键进入「结局后」状态。~~我们可以跳过自己的人生~~

- 游戏购买链接：[優艶の花 続 淫縛の館](https://www.dlsite.com/maniax/work/=/product_id/RJ283778.html)
- 运行环境：Unity 2017.4.36f1 / Mono / x64（需 BepInEx 5.4.x x64）

## 2. 功能

| 按键 | 功能 |
|---|---|
| `Tab` | 跳过当前这段过场动画，碰到对话会自动解除。 |
| `F10` | 对话快进：所有对话立即推进，过场动画照常播放。 |
| `F11` | 一键「结局」：跳过全部剧情，直接解锁全部可玩内容。 |

## 3. 安装方法

1. 将压缩包内所有文件解压到游戏根目录（即 `Yuen-Zoku.exe` 所在目录），保持目录结构不变。
2. 启动游戏。

首次启动后会自动生成配置文件 `BepInEx\config\com.yuen.skipcutscene.cfg`。

> 如果游戏目录中已安装过 BepInEx，只需覆盖 `BepInEx\plugins\YuenSkip.dll` 即可。

### 卸载

删除插件本体 `BepInEx\plugins\YuenSkip.dll` 即可。
若安装本包时一并装入了 BepInEx，且游戏内没有其他插件，可再删除以下内容恢复原状：

```
winhttp.dll
doorstop_config.ini
.doorstop_version
changelog.txt
BepInEx\
```

### 从源码构建

```
dotnet build -c Release -p:GameDir="<游戏目录>" -p:BepInExDir="<游戏目录>\BepInEx\core"
```

单元测试（纯逻辑，不需要游戏文件）：

```
dotnet run --project src\RulesTest\RulesTest.csproj
```

## 4. 开源协议

本项目采用 **MIT License**，详见 `LICENSE`。

本包内附带的 **BepInEx 5.4.23.5** 版权归其作者所有，遵循其自身协议（LGPL-2.1）；
**HarmonyX / MonoMod** 相关组件遵循其各自协议。上述第三方组件均未作任何修改。
