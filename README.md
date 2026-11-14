# FM27ManagerPhoto

A small BepInEx 6 (IL2CPP) plugin for **Football Manager 27** that shows your own PNG as your manager's portrait wherever the game draws the manager closeup. Nothing else: no player faces, no UI changes. Under 200 lines, fully readable; see [SECURITY.md](SECURITY.md).

## Requirements

- Football Manager 27 (Unity 6, IL2CPP), Steam or Xbox Game Pass.
- **BepInEx 6 IL2CPP, win-x64, bleeding-edge build.** Install it yourself and start the game once so it generates `BepInEx/interop`. This repo does not include BepInEx.

## Install

The game folder is the one that contains `fm.exe`:
- Steam: usually `...\steamapps\common\Football Manager 27`
- Game Pass: usually `C:\XboxGames\Football Manager 27\Content`

1. Copy `FM27ManagerPhoto.dll` into `<game folder>\BepInEx\plugins\FM27ManagerPhoto\`.
2. Put your portrait there too as `manager.png` (a transparent-background cutout looks best, e.g. 512x512).
3. Start the game and open your manager profile once, then quit.
4. Open `BepInEx\LogOutput.log` and find `closeup requested for manager id NNNN`. If you opened other managers, yours is the id logged when you opened your own profile.
5. Edit `<game folder>\BepInEx\config\local.fm27.managerphoto.cfg` and set `ManagerUniqueId = NNNN`.
6. Start the game again.

The id stayed the same across game restarts in testing. It has not been tested across different saves or new careers: if the portrait disappears after starting a new save, repeat steps 3 to 5 and check whether the id changed.

## Config

| Entry | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch |
| `ManagerUniqueId` | `0` | Your manager's id. `0` = do nothing |
| `ManagerName` | empty | Experimental. Only for screens where the game asks for the closeup by name (log line `closeup requested for manager name '...'`). Untested: it never fired in my testing. |
| `Photo.Folder` | the plugin's folder | Where the PNG is |
| `Photo.FileName` | `manager.png` | PNG file name |

## Uninstall

Delete `BepInEx\plugins\FM27ManagerPhoto\` and `BepInEx\config\local.fm27.managerphoto.cfg`. Nothing else is modified.

## Tested versions and BepInEx updates

Built and tested with **BepInEx 6.0.0-be.793** (IL2CPP, win-x64), Il2CppInterop 1.5.3, Harmony 2.10.2, Unity 6000.3.18f1.

BepInEx 6 only ships "bleeding-edge" builds, and its API can change between them. If you update BepInEx and this plugin stops loading, the game still starts normally (BepInEx logs the plugin error and skips it). Either go back to build 793, or wait for a release rebuilt against the newer build. If you build it yourself, `dotnet build` compiles against whatever BepInEx is installed in your game folder.

## After a game update

BepInEx regenerates its interop DLLs by itself when the game changes. This plugin is built to fail safe:

- If the patched method or a type it uses is renamed or removed, the plugin logs one warning and does nothing, and the game runs normally with its own portrait.
- If something changes while the game runs, the plugin logs one warning and stops interfering.
- It never blocks the game from starting. Worst case, you see your manager's normal face again.

If that happens, check `BepInEx\LogOutput.log` for `FM27ManagerPhoto` lines and open an issue with the game version (it is printed in the `Ready.` line) and the log lines. The fix is usually a one-line change to the method name or signature.

## Known behaviour

- Photos larger than 16 MB or 4096x4096 are refused (with a log warning).
- The log may show `Il2CppInterop: Class::Init signatures have been exhausted`. That message comes from the interop layer, not this plugin.
- The closeup path used by `SetManagerCloseupImage` is tested (profile, interviews). The by-name path (`ManagerName`) is untested. Other places that draw the manager may still show the original; please open an issue with the screen name and the log.
- Steam installs, other game versions and other BepInEx builds are untested.

## Building

Needs the .NET SDK (the project targets `net6.0`). Game DLLs are referenced, never copied:

```
dotnet build -c Release -p:GameDir="C:\XboxGames\Football Manager 27\Content"
pwsh scripts/check-dll.ps1 -Dll bin/Release/net6.0/FM27ManagerPhoto.dll
```

## License

MIT, see [LICENSE](LICENSE).
