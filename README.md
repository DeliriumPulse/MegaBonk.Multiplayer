# Megabonk Multiplayer Mod

Welcome to my first mod project! **Megabonk Multiplayer** is a BepInEx plug-in that brings online co‑op to Megabonk with deterministic map generation, full character replication, and a pluggable transport layer. The codebase is built around Harmony patches, remote avatar shims, and a slim networking core so we can iterate quickly without touching the base game binaries.

---

## Highlights

- **Synchronized worlds** – RNG hooks keep procedural tiles, seeds, and Unity random state identical for every peer from the title screen to end‑game.
- **Remote avatars that behave** – Player skins, materials, animators, and transforms are mirrored over the network, with damage flash guards that stop remote models from getting stuck in debug magenta.
- **Network abstraction** – LiteNetLib transport is in-tree today, with a Steamworks transport shim ready to slot in. The `NetDriverCore` handles peers, message fan-out, and handshakes irrespective of transport.
- **Appearance pipeline** – `SkinPrefabRegistry` and `PlayerModelLocator` reconstruct character prefabs on remote machines, ensuring abilities stay local-only while visuals stay in sync.
- **Patch suite** – Harmony patches harden everything from scene loading to RNG seeding, letting us layer multiplayer safely on top of Megabonk’s IL2CPP build.

---

## Getting Started

### Prerequisites

- Megabonk (latest retail build)
- BepInEx 6 (IL2CPP edition) installed in the game directory
- .NET 6 SDK for building from source

### Quick Install

1. Clone or download this repository.
2. Run `dotnet build -c Release` from the repo root.
3. Copy the generated `Megabonk.Multiplayer.dll` from `src/Megabonk.Multiplayer/bin/Release/net6.0/net6.0/` to each player’s `Megabonk/BepInEx/plugins/` folder.
4. Launch Megabonk with BepInEx. The log will show `[Megabonk Multiplayer]` entries once the mod loads.

> Tip: keep both host and client DLLs identical to avoid desyncs. The build timestamp is logged at startup for easy comparison.

### Hosting & Joining

1. Start the game on the machine that will host and choose “Host” in the mod’s configuration (BepInEx config or in-game UI, once available).
2. Launch the second instance with the config set to “Client”, pointing to the host’s IP/SteamID.
3. Watch `BepInEx/LogOutput.log` for `[NetDriverCore]` messages that confirm the handshake and appearance sync.

---

## Project Layout

```
src/Megabonk.Multiplayer/
├── Core/                  # Entrypoint plug-in & bootstrap logic
├── Runtime/               # Player-facing behaviours (avatars, skins, anim sync, etc.)
├── Networking/            # Transports, drivers, and message plumbing
├── Patches/               # Harmony patches (subfolders for RNG, map generation, player, UI, system)
├── Utility/               # Shared helpers (IL2CPP reflection, type dumps)
├── External/              # Bundled third-party libraries required at build time
├── Megabonk.Multiplayer.csproj
└── README.md
```

Supporting scripts (Ghidra exporters, logs) live outside `src/` and are intentionally ignored to prevent noise in the repo.

---

## Building From Source

```powershell
dotnet build          # Debug build
dotnet build -c Release
```

Artifacts land in `src/Megabonk.Multiplayer/bin/<Configuration>/net6.0/net6.0/`. Only the DLL needs to be distributed; all other files are build intermediates.

### Running Tests or Validation

- **Manual smoke test**: Launch a host and a client locally, hit each other once, and confirm `[DamageGuard]` logs show remotes skipping the magenta flash.
- **Sync validation**: Use `typelist.txt` exports and RNG trace patches to confirm both peers stay deterministic across map loads.

---

## Troubleshooting

| Symptom | What to Check |
| --- | --- |
| Remote player turns magenta | Ensure both peers are running the same build. Look for `[DamageGuard]` logs; if absent, verify `Patch_PlayerRenderer` is applied (check the BepInEx log at startup). |
| Characters spawn with wrong abilities | Confirm `RemoteStatScope` entries appear when skins initialize. If not, restart both clients to clear residue singleton state. |
| Network connect fails | Review `[LiteNetTransport]` or `[SteamP2PTransport]` warnings. Firewalls often block UDP 28960 (default). |
| Map layouts diverge | Make sure both players joined before map generation started. RNG guard patches must initialize prior to scene load; restart the session if they didn’t. |

When in doubt, attach `BepInEx/LogOutput.log` snippets to bug reports—Harmony patches log every key decision with context tags like `[LocatorRegister]` and `[RemoteAvatar]`.

---

## Roadmap

- ✅ Deterministic terrain/interactable seeding: RNG patches now cover shrines, rails, landscape passes, etc.
- 🔄 Align remaining RNG consumers (PlayerRenderer / ruin & pillar placement) so structures match 1:1 across peers.
- Steam transport polish with NAT punch-through helpers.
- UI surface for quick role switching and IP entry.
- Snapshot compression for lower bandwidth usage.
- Dedicated co-op lobby flow (persisting appearance selections across sessions).

Have ideas? Open an issue or reach out—feedback is especially welcome while this first mod project is taking shape.

---

## Credits & Thanks

- **You** – for trying out my first Megabonk mod and offering feedback while it evolves.
- The broader Megabonk community for reverse-engineering tips, IL2CPP exports, and inspiration.
- BepInEx, HarmonyX, LiteNetLib, and Steamworks.NET maintainers for the tooling that makes projects like this possible.

If you build something on top of this, please share screenshots or videos—I’d love to see the chaos you create.
