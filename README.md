# Megabonk Multiplayer

This repository contains the BepInEx mod that adds co-op networking features to **Megabonk**.

## Layout

- `src/Megabonk.Multiplayer/` – Core BepInEx plug-in (project file, source, and local tooling).
- `src/Megabonk.Multiplayer/External/` – Native/managed shim libraries required at build time.
- `src/Megabonk.Multiplayer/Patch_*.cs` – Harmony patches that synchronise RNG, map generation, and gameplay state.
- `src/Megabonk.Multiplayer/NetDriver*.cs`, `InputDriver.cs`, `RemoteAvatar.cs` – Networking entry points and replication logic.
- `src/Megabonk.Multiplayer/SkinPrefabRegistry.cs`, `PlayerModelLocator.cs` – Helpers that locate, clone, and register player models.

## Building

```powershell
dotnet build -c Release
```

Output is written to `src/Megabonk.Multiplayer/bin/Release/net6.0/net6.0/`.
Copy `Megabonk.Multiplayer.dll` from that directory into each game's `BepInEx/plugins` folder.

## Contributing

1. Fork the repo and create a feature branch.
2. Run `dotnet build` before opening a PR.
3. Include relevant log excerpts or reproduction steps when fixing multiplayer sync issues.
