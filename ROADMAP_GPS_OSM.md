# Borderline – Path from Prototype to Real-World Map

## 1. OpenStreetMap: what's free and what isn't

- **OSM *data* is free** under the ODbL. You must show the attribution "© OpenStreetMap contributors" on the map.
- **The OSM Foundation's *tile servers* are not free for apps.** `tile.openstreetmap.org` is donation-funded and governed by a strict usage policy, and heavy or app use gets blocked. Don't hard-code it.
- Free or cheap ways to show OSM maps:
  - **Self-host vector tiles** with **Protomaps PMTiles**: one file on cheap object storage or a CDN, styled to match the game. This is the cheapest option at scale.
  - **Commercial OSM tile hosts** with free tiers (e.g. MapTiler, Stadia Maps). These are easiest to start with. Read each provider's free-tier limits and game/commercial terms.
- A 2D game rarely needs street-level detail. A stylized map (roads, water, parks, buildings) rendered from vector tiles looks better and is lighter.

## 2. The spatial grid is free (it's just math)

Replace the 15×15 board with a global hex index, e.g. **Uber H3**:
- Every point on Earth maps to a cell ID with no server call. Resolution 9 is about 0.1 km² (a city block), and resolution 10 is about 0.015 km² (a building or two).
- `HexGridManager` becomes `GlobalHexGrid`: Convert GPS to an H3 cell, load the ring of cells around the player, and draw their boundaries over the map.
- `HexTile` stays the same and gets its state from the server, keyed by H3 cell ID.

## 3. Server (must be authoritative before any real players)

Ownership, garrisons and dwell time live on a server, never on the phone. Options include Unity Gaming Services (Cloud Code + Cloud Save), Nakama, or Supabase/PostGIS. (Game Center / Play Games already keep the player's profile, achievements and leaderboards; they can't hold shared map state.)

- The client sends a check-in (`cellId`, timestamp, accuracy) and the server decides what it means.
- **Anti-spoofing:** Detect mock locations (Android), reject impossible travel speeds, rate-limit actions, and credit dwell only after plausible repeated check-ins.

## 4. Dwell fortification with real time

The prototype uses seconds. For real play, scale it to hours, e.g.:

| Setting | Prototype | Real game (example) |
|---|---|---|
| Base walls per second | 0.25 / s | ~10 / hour |
| Ramp (+1× rate) | every 60 s | every 8 h of total presence |
| Max multiplier | 6× | 6× |
| Free turret | every 45 s | every 24 h of total presence |

**Background location is the big decision.** Knowing someone stayed at home all night normally needs "Always" (iOS) or background location (Android). Both get heavy store review and scare users.
**Recommended alternative:** Have dwell accrue *between check-ins*. If two check-ins happen in the same cell X hours apart (opened at 8 am and again at 6 pm), the server credits up to X hours, capped. This works with "While Using" permission only.

## 5. Privacy design (important for this concept)

Heavily fortified hexes effectively mark where players live and work. Before launch:
- Never show *who* is currently in a hex or when they were last there. Show only ownership and strength.
- Consider hiding or delaying owner names on high-dwell hexes, and let players mark "private" cells that can't be inspected.
- Store H3 cell IDs rather than raw coordinates where possible, and keep only what gameplay needs.
- Update the privacy policy (https://sites.google.com/view/og-software/privacy-policy) to cover location collection, retention and deletion. Fill in Google Play's Data safety form and Apple's App Privacy section to match.
- Set the location permission strings: *Location Usage Description* in Player Settings for iOS, and request foreground location only on Android.

## 6. Suggested order of work

1. ✅ Hex prototype (this repo, v0.0.1)
2. Swap the board for H3 cells around a *fake* GPS position (keyboard-driven in the editor)
3. Add a vector-tile map underneath (PMTiles or a hosted provider)
4. Real GPS (Unity `Input.location` or a native plugin) + server check-ins
5. Move combat and dwell to the server, then add multiplayer (real rivals replacing `RivalAI`)
6. Closed beta in one city
