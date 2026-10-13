# Yuen-Zoku Cutscene Skipper

[中文说明](README.md)

## 1. About

A BepInEx plugin for **淫縛の館** (Yuen-Zoku / doujin circle DoubleSoftCream) that adds cutscene
skipping, dialogue fast-forwarding, and a one-key jump to the post-ending state. ~~We can skip our own lives~~

- Purchase the game: [優艶の花 続 淫縛の館](https://www.dlsite.com/maniax/work/=/product_id/RJ283778.html)
- Runtime: Unity 2017.4.36f1 / Mono / x64 (requires BepInEx 5.4.x x64)

## 2. Features

| Key | Function |
|---|---|
| `Tab` | Skip the current cutscene. Disengages automatically when dialogue is reached. |
| `F10` | Toggle dialogue fast-forward: all dialogue advances immediately, cutscenes play normally. |
| `F11` | One-key "Ending": skip all story and directly unlock every playable piece of content. |

## 3. Installation

1. Extract every file in the archive into the game's root folder (the folder containing `Yuen-Zoku.exe`), keeping the directory structure intact.
2. Launch the game.

A config file is generated on first launch at `BepInEx\config\com.yuen.skipcutscene.cfg`.

> If BepInEx is already installed in the game folder, simply overwrite `BepInEx\plugins\YuenSkip.dll`.

### Uninstallation

Delete the plugin itself, `BepInEx\plugins\YuenSkip.dll`.
If you also installed BepInEx from this package and have no other plugins, you may additionally delete the following to restore the original state:

```
winhttp.dll
doorstop_config.ini
.doorstop_version
changelog.txt
BepInEx\
```

### Building from source

```
dotnet build -c Release -p:GameDir="<game folder>" -p:BepInExDir="<game folder>\BepInEx\core"
```

Unit tests (pure logic, no game files required):

```
dotnet run --project src\RulesTest\RulesTest.csproj
```

## 4. License

This project is licensed under the **MIT License**; see `LICENSE`.

The bundled **BepInEx 5.4.23.5** is copyright its authors and is distributed under its own license (LGPL-2.1);
**HarmonyX / MonoMod** components follow their respective licenses. None of these third-party components have been modified.
