# Security

## What the plugin does

- Registers one Harmony **prefix** on `FM.UI.ManagerCloseupShotGenerator.SetManagerCloseupImage`.
- Also registers an optional second prefix on `SetManagerCloseupImageByName`, active only if you set `ManagerName`.
- When the game asks for the closeup of the manager id (or name) you configured, it calls `SIImage.SetImage` with a texture decoded from **one PNG file** and skips the original call. For every other id, and after any error, the game's own code runs unchanged.
- Reads its BepInEx config values and one PNG file. Writes only BepInEx's own config file (`BepInEx/config/local.fm27.managerphoto.cfg`).

## What it does not do

- No networking: no `System.Net.*`, no sockets, no URLs.
- No launching of processes, no registry access, no loading of other assemblies, no P/Invoke.
- Does not read or write save games or any other game file.
- No obfuscation. The whole plugin is `Plugin.cs` (under 200 lines).
- It bundles no BepInEx and no game DLLs.

`scripts/check-dll.ps1` enforces the first two points: it scans the source and the built DLL for
`System.Net`, `HttpClient`, `Socket`, `System.Diagnostics.Process`, `Microsoft.Win32`, `Assembly.Load`, `DllImport` and URLs, and fails if any is found. CI runs it on every release.

## Verifying a release DLL

1. Download `FM27ManagerPhoto.dll` and `FM27ManagerPhoto.dll.sha256` from the release.
2. Compare: `(Get-FileHash FM27ManagerPhoto.dll -Algorithm SHA256).Hash` must match the `.sha256` file.
3. Optionally build it yourself and compare (see README, "Building"): check out the release tag and build. For v1.0.1 this was checked once: a fresh clone of the tag, built in a different folder with .NET SDK 10 against the same game files, produced the identical SHA-256. The build embeds the git commit id, so build from the tagged commit. The game's interop DLLs differ between game versions, so a hash difference alone is not proof of tampering; in that case read the source, run `check-dll.ps1`, and inspect the DLL with a decompiler such as ILSpy.

## Reporting a problem

Open a GitHub issue. For something sensitive, use GitHub's private vulnerability reporting on this repository.
