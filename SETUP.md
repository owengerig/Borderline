# Borderline – Prototype Setup (v0.1.0)

A self-contained hex-grid prototype of the core loop: claim, reinforce, attack, fortify by dwelling, and fight an AI rival. There's no map or GPS yet; see `ROADMAP_GPS_OSM.md` for that.

```
Assets/Borderline/Scripts/
  Core/      HexCoord.cs  HexMesh.cs  GameTypes.cs  AppInfo.cs
  Platform/  GameServices.cs (profile, achievements, leaderboards, cloud save)
  Grid/      HexGridManager.cs  HexTile.cs  PresenceMarker.cs
  Gameplay/  GameManager.cs  RivalAI.cs  Combat.cs
  Input/     CameraController.cs
  UI/        HUDController.cs
```

---

## 1. Install Unity

1. Install **Unity Hub**, then **Unity 6 LTS** (any `6000.x` LTS).
2. In the install dialog, add these modules:
   - **Android Build Support**, including *OpenJDK* and *Android SDK & NDK Tools*
   - **iOS Build Support**, which is needed to generate the Xcode project (works on Windows too)

## 2. Create the project inside this folder

Unity Hub won't create a project in a folder that already has files in it (this one has `.git`), so:

1. In Hub, go to **New project**, pick the **Universal 2D** template, and create it somewhere temporary, e.g. `C:\Users\oweng\Borderline_tmp`.
2. Close Unity. Copy the `Assets`, `Packages` and `ProjectSettings` folders from `Borderline_tmp` into `C:\Users\oweng\Borderline`. `Assets` merges with the `Assets/Borderline` folder that's already there.
3. In Hub, choose **Add > Add project from disk** and pick `C:\Users\oweng\Borderline`. Open it, and then you can delete `Borderline_tmp`.

## 3. One-time project settings

**Edit > Project Settings > Player**
| Setting | Value |
|---|---|
| Company Name | `OG Software` |
| Product Name | `Borderline` |
| **Version** | **`0.1.0`** (the footer shows `Created by OG Software v0.1.0`, like every OG Software app) |
| Package / Bundle ID (Android + iOS) | `com.ogsoftware.borderline` (the Game Center / Play Games IDs in `GAME_SERVICES.md` use it) |
| Default Orientation (Resolution and Presentation) | Portrait |
| Other Settings > Active Input Handling | `Input System Package (New)` or `Both` |

**TextMeshPro:** Choose **Window > TextMeshPro > Import TMP Essential Resources**. If you skip this, labels won't render.

## 4. Create the hex material

1. Right-click `Assets/Borderline`, choose **Create > Folder**, and name it `Materials`.
2. In that folder, choose **Create > Material** and name it `HexMaterial`.
3. Set the shader:
   - URP project (Universal 2D template): `Universal Render Pipeline/2D/Sprite-Unlit-Default`
   - Built-in pipeline: `Sprites/Default`

   The tiles are colored through vertex colors, so leave the material's color white.

## 5. Build the HexTile prefab

1. In the Hierarchy, choose **Create Empty**, name it `HexTile`, and leave its Transform at 0,0,0.
2. **Add Component > Hex Tile**. MeshFilter and MeshRenderer are added automatically.
3. Right-click `HexTile` and choose **3D Object > Text - TextMeshPro**. Rename the new child to `Label`. Use the 3D TextMeshPro, not the UI version.
4. Select `HexTile` and drag `Label` into the **Label** field of Hex Tile.
5. Create the folder `Assets/Borderline/Prefabs`, drag `HexTile` into it to make a prefab, then delete it from the scene.

> The prefab is optional. If the field is left empty, the grid builds identical tiles in code. Using the prefab lets you restyle the label (font, size) in one place.

## 6. Wire up the scene

1. **File > New Scene** using the *Basic 2D* or *Empty* template. Save it as `Assets/Borderline/Scenes/Main.unity`.
2. **Main Camera**
   - Projection: **Orthographic**, Size `6`, Position `(0, 0, -10)`
   - Background: Solid Color `#121418` (on URP, this is under Environment > Background Type)
   - **Add Component > Camera Controller**
3. **Hex grid:** Create an empty object named `HexGrid` at `(0,0,0)` and add **Hex Grid Manager**.
   - Width `15`, Height `15`, Cell Radius `0.5`
   - Tile Prefab: `HexTile` prefab
   - Tile Material: `HexMaterial`
4. **Game:** Create an empty object named `GameManager` and add **Game Manager**. Drag `HexGrid`, `Main Camera` and `RivalAI` (next step) into its reference fields. If you leave them empty, they're found automatically.
5. **Rival:** Create an empty object named `RivalAI` and add **Rival AI**.
6. **HUD:** Create an empty object named `HUD` and add **HUD Controller**. Drag `GameManager` into the Game field. The canvas, buttons, footer and EventSystem are all created at runtime.
7. Open **File > Build Profiles** (called *Build Settings* in older Unity versions), click **Add Open Scenes**, and make sure `Main` is listed at index 0. The *Play again* button needs this.
8. In the Game view, pick a phone resolution (e.g. 1080×1920 Portrait), or use **Window > General > Device Simulator** to check iPhone notches and safe areas.

Press **Play**.

---

## 7. How it plays

| Action | Input | Rule |
|---|---|---|
| Claim | Tap a **gray** hex bordering blue | 2 energy, becomes yours with 5 troops |
| Reinforce | Tap a **blue** hex | Uses the selected build button: Troops +3 (2), Vehicle (8), Turret (12) |
| Attack | Tap a **red** hex bordering blue | Spends 25/50/100% of energy (selected button) |
| Set Location | Tap **Set Location**, then a hex | Simulates standing there (later this will come from GPS) |
| Pan / Zoom | Drag; pinch or mouse wheel | Clamped to the board |

**Energy:** +1 per second, max 50 (`GameManager > Player Energy`).

**Combat:**
`AttackPower = energy spent + 3 × friendly vehicles bordering the target (max 6)`
`EffectiveDefense = DefensePower × 1.2`
- If AttackPower > EffectiveDefense, you capture the hex, and `AttackPower − EffectiveDefense` survivors (at least 1) garrison it.
- Otherwise the defender loses `AttackPower ÷ 1.2` defense.

**Defense power** = Troops ×1 + Vehicles ×4 + Turrets ×6 + Walls.
Damage is removed in this order: troops, then vehicles, then turrets, then walls. The label shows the total, plus `V` vehicles, `G` turrets (guns) and `W` walls.

**Dwell fortification (the "home/work" mechanic):**
While your yellow ring sits on a hex you own, that hex gains walls continuously. The rate grows with the total time you've spent there (+1× per 60 s, up to 6×), and every 45 s your crew builds a free turret (max 4).
The time is stored per hex, so returning to "home" picks up where the ramp left off. The AI commutes between a home hex and a work hex, so its two strongholds harden the same way.

**Automated defenses:** Every 3 s, each turret deals 1 damage to the weakest adjacent enemy hex. Turrets suppress (they can drop a hex to 1) but never capture.

**Win / lose:** Wipe out every red hex to win; lose your last blue hex and you're defeated.

## 8. Tuning cheat-sheet

| Where | Field | Effect |
|---|---|---|
| GameManager | `Dwell > Base Fortification Per Second`, `Ramp Seconds`, `Max Rate Multiplier` | How fast "being there" builds walls |
| GameManager | `Dwell > Seconds Per Auto Turret` | Free turret cadence (0 turns it off) |
| GameManager | `Defender Bonus`, `Vehicle Support Power` | Combat balance |
| GameManager | `Turret Fire Interval`, `Turret Damage Per Turret` | Strength of automated defenses |
| RivalAI | `Action Interval` (2 s), `Attack Chance`, `Commute Interval` | AI aggression |
| UnitStats (GameTypes.cs) | power / cost / max per tile | Unit balance |

## 9. Troubleshooting

- **Hexes are pink or magenta:** The material uses the wrong shader for your render pipeline (step 4).
- **No numbers on hexes or HUD:** TMP Essentials aren't imported (step 3).
- **Taps do nothing / Input System errors in the Console:** Set *Active Input Handling* to `Input System Package (New)` or `Both`, then let Unity restart.
- **Taps go through buttons onto the map:** Make sure only one EventSystem exists. The HUD creates one if there's none.
- **"Scene couldn't be loaded" on Play again:** Add `Main` to Build Profiles (step 6.7).

## Game Center / Google Play Games

Shared OG Software code lives in `Assets/OGPlatform` with native bridges in `Assets/Plugins/iOS/OGPlatform` and
`Assets/Plugins/Android/OGPlatform`; Borderline's part is `Scripts/Platform/GameServices.cs`. It keeps a lifetime
profile (matches, wins, hexes claimed / captured, turrets, fastest win), backs it up to Game Center / Play Games saved
games, and drives 7 achievements and 2 leaderboards. The Xcode and Gradle setup is added at build time. Store setup:
[`GAME_SERVICES.md`](GAME_SERVICES.md).
