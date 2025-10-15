# Megabonk Multiplayer Mod

Megabonk Multiplayer is a BepInEx plug-in that layers deterministic online co-op on top of Megabonk. Harmony patches drive the game-side hooks, while a slim networking core (LiteNetLib today, Steamworks tomorrow) keeps peers in sync without touching the base IL2CPP binaries.

---

## Highlights

- **Synchronized worlds** – RNG hooks keep procedural tiles, seeds, and Unity random state identical for every peer from title screen to end-game.
- **Remote avatars that behave** – Skins, materials, animators, and transforms replicate over the network, with damage-flash guards so remote models never freeze in debug magenta.
- **Pluggable transport** – `NetDriverCore` abstracts peer management; LiteNetLib ships in-tree and a Steamworks shim can slot in without code churn.
- **Prefab reconstruction** – `SkinPrefabRegistry` and `PlayerModelLocator` rebuild character models on remote machines so gameplay stays authoritative but visuals line up.
- **Patch suite** – Harmony patches harden scene loading, RNG seeding, UI shims, and player systems so multiplayer rides on top of the existing game safely.

---

## Getting Started

### Prerequisites

- Megabonk (latest retail build)
- BepInEx 6 IL2CPP edition installed in the game directory
- .NET 6 SDK for building from source

### Quick Install

1. Clone or download this repository.
2. Run `dotnet build -c Release` from the repo root.
3. Copy `src/Megabonk.Multiplayer/bin/Release/net6.0/net6.0/Megabonk.Multiplayer.dll` to each player's `Megabonk/BepInEx/plugins/` folder.
4. Launch Megabonk with BepInEx. Look for `[Megabonk Multiplayer]` in `BepInEx/LogOutput.log` to confirm the plug-in loaded.

> **Tip:** keep host and client DLLs identical to avoid desyncs. The build timestamp is printed at startup for easy comparison.

### Hosting & Joining

1. On the host machine, set the config entry `Multiplayer.Role=Host` (via `BepInEx/config/vettr.megabonk.multiplayer.cfg` or the upcoming in-game UI) and start the game.
2. On each client, set `Multiplayer.Role=Client` and point `HostAddress` (or `HostSteamId` once Steam transport lands) at the host.
3. Watch the log for `[NetDriverCore]` messages confirming the handshake and appearance sync.

---

## Project Layout

```
src/Megabonk.Multiplayer/
├── Core/                  # Entrypoint plug-in & bootstrap logic
├── Networking/            # Transports, driver, wire protocol helpers
├── Runtime/               # Player-facing behaviours (avatars, stats, FX)
├── Patches/               # Harmony patch families (Rng, Player, Map, UI, System)
├── Utility/               # Shared IL2CPP helpers, type dumps, logging
├── External/              # Third-party libs referenced at build time
└── Megabonk.Multiplayer.csproj
```

Auxiliary tooling (Ghidra exports, log scrapers) lives outside `src/` and is intentionally ignored to keep the repo tidy.

---

## Building From Source

```powershell
dotnet build             # Debug build
dotnet build -c Release  # Release build
```

Artifacts land in `src/Megabonk.Multiplayer/bin/<Configuration>/net6.0/net6.0/`. Only the DLL needs to be distributed.

### Validation Tips

- **Manual smoke test:** Start a host and a client locally, exchange damage, and confirm `[DamageGuard]` logs show remote avatars skipping the magenta flash.
- **Sync verification:** Use `typelist.txt` exports and `[JOBRNG]` traces to ensure both peers stay deterministic through map generation and loot rolls.

---

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Remote avatar turns magenta or sticks in T-pose | Confirm both peers are running the same DLL. Look for `[DamageGuard]` / `[RemoteAvatar]` logs; if absent, the player patches did not apply (restart with a clean log). |
| Ruins or pillars differ between peers | Ensure every player joined before map generation started. Compare `[JOBRNG] PlayerRenderer.Update` lines—if the call counts differ, send both logs so we can extend the RNG coverage. |
| Chest loot diverges | Grab the host/client sections around `[JOBRNG] InteractableChest.Start` and share them—those lines should match exactly once both sides run the latest DLL. |
| Cannot connect to host | Check the config (`Role`, `HostAddress`, `Port`). Inspect the log for `[LiteNetTransport]` warnings—firewalls commonly block UDP 28960 (default). |
| Stutter or packet loss | Try a Release build (smaller logs), disable verbose network tracing (`Debug.VerboseNetworkPackets=false`), and verify both machines are on stable connections. |
| Crash or assertion | Zip `BepInEx/LogOutput.log` (and `ErrorLog.log` if present) plus the latest `typelist.txt` and attach them to a GitHub issue. |

When in doubt, send logs—Harmony tags every major action (`[LocatorRegister]`, `[JOBRNG]`, `[RemoteAvatar]`, etc.) so we can track the sequence quickly.

---

## Roadmap

### ✅ Recently Landed
- Deterministic terrain/interactable seeding: shrines, rails, rocks, trees, and landscape passes now match across peers.
- `Patch_DumpAndForceJobRNGs` expanded to every RNG-heavy managed entry point (projectiles, abilities, item procs, PlayerRenderer, etc.).
- Legacy RNG guards removed; all reseeding now flows through scoped Harmony prefixes and `UnityRandomScope`.

### 🛠 In Progress
- Align remaining RNG consumers (especially `PlayerRenderer.Update`) so ruin/pillar structures, loot rolls, and late-spawn props stay deterministic.
- Add targeted logging for chest/loot pipelines and confirm InteractableChest is fully deterministic across host/client.
- Automate “first divergence” diffing between host/client logs to shorten debugging loops.

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

- **You** – for trying out the mod and sending feedback while it evolves.
- The broader Megabonk community for reverse-engineering tips, IL2CPP exports, and inspiration.
- The BepInEx, HarmonyX, LiteNetLib, and Steamworks.NET maintainers for the tooling that makes projects like this possible.

If you build something on top of this, please share screenshots or videos—we’d love to see the chaos you cook up.
