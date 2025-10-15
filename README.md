# Megabonk Multiplayer Mod

Megabonk Multiplayer is a BepInEx plug-in that brings deterministic online co-op to Megabonk. Harmony patches, remote avatar shims, and a lean networking core let us layer multiplayer on top of the IL2CPP build without touching the base game binaries.

---

## Highlights

- **Synchronized worlds** – RNG hooks keep procedural tiles, seeds, and Unity random state identical for every peer from the title screen to end-game.
- **Remote avatars that behave** – Player skins, materials, animators, and transforms replicate over the network, with damage flash guards that stop remote models from getting stuck in debug magenta.
- **Network abstraction** – LiteNetLib transport is in-tree today, with a Steamworks transport shim ready to slot in. `NetDriverCore` handles peers, message fan-out, and handshakes irrespective of transport.
- **Appearance pipeline** – `SkinPrefabRegistry` and `PlayerModelLocator` reconstruct character prefabs on remote machines, keeping abilities local while visuals stay in sync.
- **Patch suite** – Harmony patches harden everything from scene loading to RNG seeding, letting us iterate quickly while the base game keeps evolving.

---

## Getting Started

### Prerequisites

- Megabonk (latest retail build)
- BepInEx 6 (IL2CPP edition) installed in the game directory
- .NET 6 SDK for building from source

### Quick Install

1. Clone or download this repository.
2. Run `dotnet build -c Release` from the repo root.
3. Copy `src/Megabonk.Multiplayer/bin/Release/net6.0/net6.0/Megabonk.Multiplayer.dll` to each player's `Megabonk/BepInEx/plugins/` folder.
4. Launch Megabonk with BepInEx. The log will show `[Megabonk Multiplayer]` entries once the mod loads.

> **Tip:** keep host and client DLLs identical to avoid desyncs. The build timestamp is logged at startup for easy comparison.

### Hosting & Joining

1. Start the game on the machine that will host and choose “Host” in the mod configuration (BepInEx config or an in-game UI once available).
2. Launch the second instance with the config set to “Client”, pointing to the host's IP/SteamID.
3. Watch `BepInEx/LogOutput.log` for `[NetDriverCore]` messages that confirm the handshake and appearance sync.

---

## Project Layout

```
src/Megabonk.Multiplayer/
├── Core/                  # Entrypoint plugin & bootstrap logic
├── Runtime/               # Player-facing behaviours (avatars, skins, anim sync, etc.)
├── Networking/            # Transports, drivers, and message plumbing
├── Patches/               # Harmony patches (Rng, Player, Map, UI, System)
├── Utility/               # Shared helpers (IL2CPP reflection, type dumps)
├── External/              # Bundled third-party libraries required at build time
└── Megabonk.Multiplayer.csproj
```

Supporting scripts (Ghidra exporters, logs) live outside `src/` and are intentionally ignored to keep the repo tidy.

---

## Building From Source

```powershell
dotnet build             # Debug build
dotnet build -c Release  # Release build
```

Artifacts land in `src/Megabonk.Multiplayer/bin/<Configuration>/net6.0/net6.0/`. Only the DLL needs to be distributed; all other files are build intermediates.

### Validation Tips

- **Manual smoke test:** Launch a host and a client locally, land a hit, and confirm `[DamageGuard]` logs show remotes skipping the magenta flash.
- **Sync validation:** Use `typelist.txt` exports and `[JOBRNG]` trace patches to confirm both peers stay deterministic across map loads.

---

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Remote player turns magenta | Ensure both peers are running the same build. Check for `[DamageGuard]` logs; if absent, verify `Patch_PlayerRenderer` is applied. |
| Characters spawn with wrong abilities | Confirm `RemoteStatScope` entries appear when skins initialize. If not, restart both clients to clear residual singleton state. |
| Network connect fails | Review `[LiteNetTransport]` or `[SteamP2PTransport]` warnings. Firewalls often block UDP 28960 (default). |
| Map layouts diverge | Make sure both players joined before map generation started. RNG patches must initialize prior to scene load; restart the session if they didn’t. |

When in doubt, attach `BepInEx/LogOutput.log` snippets to bug reports—Harmony patches log every key decision with context tags like `[LocatorRegister]` and `[RemoteAvatar]`.

---

## Roadmap

### ✅ Recently Landed
- Deterministic terrain/interactable seeding: shrines, rails, rocks, trees, and landscape passes now match across peers.
- `Patch_DumpAndForceJobRNGs` expanded to every RNG-heavy managed entry point (projectiles, abilities, item procs, PlayerRenderer, etc.).
- Legacy RNG guards removed; all reseeding now flows through scoped Harmony prefixes and `UnityRandomScope`.

### 🛠 In Progress
- Align remaining RNG consumers (especially `PlayerRenderer.Update`) so ruin/pillar structures, loot rolls, and late-spawn props stay deterministic.
- Add targeted logging for chest/loot pipelines and confirm host/client seeding covers every InteractableChest variant.
- Automate “first divergence” diffing between host/client logs to speed up multiplayer debugging.

### 🎯 Next Milestones
- Steam transport polish with NAT punch-through helpers and automatic peer discovery.
- In-game multiplayer UX: role switching, IP/host entry, join codes, ready checks, and status indicators.
- Snapshot compression & delta sync to lower bandwidth and reduce stutter on busy maps.
- Dedicated co-op lobby flow that persists appearance/skin selections between sessions.
- Replay/spectator-safe architecture so observers can join after the map is seeded.
- Save-state reconciliation hooks (stats, unlocks) to keep campaign progression aligned across machines.
- Host migration and reconnect support so clients can rejoin an in-progress run without restarting the map.
- Modding surface for community events (shared challenges, weekly seeds, seasonal modifiers).

---

## Credits & Thanks

- **You** – for trying out my first Megabonk mod and offering feedback while it evolves.
- The broader Megabonk community for reverse-engineering tips, IL2CPP exports, and inspiration.
- BepInEx, HarmonyX, LiteNetLib, and Steamworks.NET maintainers for the tooling that makes projects like this possible.

If you build something on top of this, please share screenshots or videos—I’d love to see the chaos you create.
