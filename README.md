# Megabonk Multiplayer

This repository contains the BepInEx mod that adds co-op networking features to **Megabonk**.

## Structure

- `Megabonk.Multiplayer.csproj` – Core mod project targeting `net6.0`.
- `NetDriver*`, `InputDriver.cs`, `RemoteAvatar.cs` – Main networking, transport, and replication systems.
- `Patches/` – Harmony patches that synchronise map generation, RNG, and gameplay data across peers.
- `SkinPrefabRegistry.cs`, `PlayerModelLocator.cs` – Helpers that locate and clone player models for remote avatars.

## Building

```powershell
dotnet build -c Release
```

The compiled DLL will be in `bin\Release\net6.0\net6.0\` and can be copied into the game's `BepInEx/plugins` directory.

## Contributing

1. Fork the repo and create a feature branch.
2. Run `dotnet build` before opening a PR.
3. Include relevant log excerpts or reproduction steps when fixing multiplayer sync issues.
