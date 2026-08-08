# Art direction — cabin & interrogation dressing

**Status:** brief + shopping list, not yet executed. Nothing in this document has been placed in a
scene. Written to answer "what should I go download, and what should it look like" before any
dressing pass starts.

**Scope call made 8 Aug 2026:** photoreal PBR (matches the existing `Cabin_v2` Poly Haven texture
work and the concept art, not a stylized pack), **free/CC0 sources only**, cabin scenes prioritized
over the interrogation room. This supersedes the paid-pack recommendation in
[`Assets/_Project/ASSETS_TODO.md`](../Assets/_Project/ASSETS_TODO.md) — see the note at the top of
that file.

Every asset below was checked by opening its source page and reading the license, not recalled
from memory. Where nothing verifiable turned up, that's written down as a gap with a search term,
not papered over with a guessed link. **Sketchfab renders its license badge client-side and a few
entries below could only be confirmed from a search snippet** — those are flagged
**[verify in browser]** and should not be treated as cleared until someone opens the page directly.

---

## 1. Tone targets

**Cabin, night — cozy.** Reference:
[`Assets/_Project/Art/Characters/NobodyWentOut/Concepts/full-cast-ensemble.png`](../Assets/_Project/Art/Characters/NobodyWentOut/Concepts/full-cast-ensemble.png).
A stone hearth throws warm orange light from frame left across dark wood and a patterned rug; cold
blue snow-light comes through the windows behind the group. Dark, warm, close — the room should
feel like the only lit thing for miles. Mostly wood, per your direction: walls, beams, furniture,
floor all timber, broken up by the stone fireplace and one rug.

**Cabin, morning — eerie.** Same room, same furniture, drained. Flat grey overcast light, low
contrast, desaturated. Not darker — *flatter*. The fire has burned to embers. One hard, cold shaft
of raking light comes through the broken window pane, the only strong directional cue in an
otherwise shadowless room. The wrongness is in the stillness and the flatness, not in shadow.

**Interrogation — cold Soviet.** Already partly canon —
[`docs/superpowers/specs/2026-08-04-nobody-went-out-interrogation-trailer-design.md`](superpowers/specs/2026-08-04-nobody-went-out-interrogation-trailer-design.md)
specifies a Russian-accented interrogator (Officer Spassky), a metal table, a small red recording
light. Institutional, bare, one hard warm pendant over the table and nothing else warm in the room
— cold fluorescent or window-light fill, worn paint, bare metal, concrete or worn linoleum. The
player should feel *processed*, not threatened by violence — threatened by procedure.

---

## 2. Palette

Extends the palette already committed in
[`Assets/_Project/CabinNight/Materials/`](../Assets/_Project/CabinNight/Materials/) — new material
work should sit inside these ranges, not introduce a new one. Values are the linear RGB already in
the `.mat` files (what you'd type into Unity's color picker), with an approximate sRGB hex for
mood-boarding.

| Material (existing) | Linear RGB | ≈ sRGB hex | Read |
|---|---|---|---|
| `WoodDark` | 0.105, 0.065, 0.038 | `#5C4A39` | near-black plank shadow |
| `WoodWarm` | 0.24, 0.115, 0.052 | `#896143` | mid brown, the dominant wood tone |
| `BlackMetal` | 0.035, 0.04, 0.045 (metallic 0.72, smooth 0.58) | `#36393C` | hardware, grille, hinges |
| `Rug` | 0.16, 0.035, 0.025 | `#6F362F` | oxblood accent |
| `SofaCloth` | 0.12, 0.075, 0.055 | `#634E43` | dark upholstery |
| `Snow` | 0.78, 0.86, 0.92 | `#E3EDF5` | cold blue-white |
| `Stone` | 0.19, 0.205, 0.22 | `#787C80` | fireplace masonry |
| `FireGlow` (emissive) | HDR 2.3, 0.34, 0.04 | — | the one hot orange in the scene |

**Gap to fix while you're in there:** the eight textured `*_URP.mat` materials (`Bed_URP`,
`Cabin_URP`, `Cabinet_URP`, `Candle_URP`, `Door_URP`, `Lantern_URP`, `Stool_URP`, `Table_URP`) are
all still sitting at Unity's default **smoothness 0.5** — every hand-tuned material in the project
is 0.05–0.30. Pull these down to ~0.15–0.25 to match; at 0.5 they'll look plasticky next to
everything else once real lighting hits them.

**Interrogation palette (new — nothing exists yet):** cold institutional. Worn pale-green or grey
plaster (`~#8A9A8C`-ish, desaturated), bare or painted-and-peeling steel (`#4A4E4E` to rusty
`#5C4A3A`), pale worn concrete floor (`#6B6B66`), warm tungsten pendant at ~3000K
(`~#FFCE94`), sickly fluorescent fill at ~5500K with a green cast (`~#C8E0CE`).

---

## 3. Lighting — concrete values to type in

### Cabin night (cozy)

The cozy read comes from **firelight actually casting shadows**, which it currently doesn't.

| Light | Change |
|---|---|
| `Firelight` (point, in `BO_Fireplace`) | Shadows: **None → Soft**. Keep intensity 3.2 / range 6 / color (1, 0.55, 0.22) — it's already warm and correctly placed, it just isn't casting. |
| `Cold Moonlight` (directional) | Intensity **0.52 → ~0.15–0.20**. It should read as cold light leaking in around the windows, not as a light source for the room. Keep color and rotation. |
| New: table lantern (point) | ~intensity 1.0, range 2.5 m, color (1, 0.75, 0.45) ≈2700K, near the dressing cluster by the table. Shadows optional — you have budget: `PC_RPAsset` allows 4 shadowed additional lights at a 2048 atlas. |
| New: mantel candle (point) | ~intensity 0.4, range 1 m, same warm color, beside `Prop_MantelClock`. |
| Fog | `RenderSettings.fogDensity` is currently **0.011 in both scenes** (see blocker #2) — for night, keep it low, ~0.006–0.008, so it doesn't wash out the warm interior. |

Warm/cool split is the whole effect: hearth is the only strong warm source, the windows are the
only cold one, everything else is fill.

### Cabin morning (eerie)

Fix blocker #2 first — the morning scene currently inherits the *night* fog and ambient values
wholesale.

| Setting | Value |
|---|---|
| `Grey Morning Light` rotation | Consider dropping elevation from the current 35° to **~15–18°**, keeping the yaw pointed roughly at the broken window (currently 300°) — a low raking angle reads colder and gives you the one hard shaft the tone target calls for. |
| Fog color | **grey-white**, ~(0.62, 0.66, 0.70) linear, replacing the inherited (0.055, 0.075, 0.12) night blue. |
| Fog density | ~0.018–0.022 — enough haze to flatten depth without hiding the room. |
| Ambient gradient | Raise and desaturate: sky ~(0.35, 0.36, 0.38), equator ~(0.28, 0.28, 0.29), ground ~(0.12, 0.12, 0.13). |
| Skybox | Swap `StormSky.mat` for an overcast HDRI — see §4 exterior list, **Poly Haven "Snowy Forest"** is a strong match. |
| `Firelight` | Burn down to ~1.0–1.3 intensity, shift slightly cooler/dimmer (~0.9, 0.4, 0.15) — it's been going all night and nobody's fed it. |

Post-processing target: heavy desaturation (−40 to −60%), lifted shadows, low contrast, cool-
neutral balance, light grain, little or no vignette. Eerie here is **flat and lifeless**, not dark.

### Interrogation (cold Soviet)

The room currently has one default white directional light with shadows off, no ceiling, and its
authored dark ambient gradient is inert because `m_AmbientMode` is set to Skybox instead of
Gradient.

| Change | Detail |
|---|---|
| Remove the default `Directional Light` | It's a windowless interior — a directional key makes no sense once it has a ceiling. |
| Add a ceiling | The room has none today; this blocks any overhead fixture from reading correctly. |
| New: caged pendant (point or spot) | Above the table at roughly (0, 2.6, 0.6). Warm tungsten (1, 0.82, 0.55) ≈3000K, intensity ~1.5–2.0, range ~3 m, **shadows on (soft)** — this is the one light that should throw hard shadows across the table and both faces. |
| New: fluorescent strip(s) | Cold green-cyan (0.75, 0.95, 0.85), low fill intensity ~0.3–0.5, mounted at the new ceiling. Institutional flicker is a nice-to-have (a simple intensity-noise script), not required. |
| `RenderSettings.ambientMode` | **0 (Skybox) → 1 (Gradient/Trilight)** — this single flip activates the dark gradient that's already authored (sky 0.212/0.227/0.259, equator 0.114/0.125/0.133, ground 0.047/0.043/0.035). Consider darkening it further once the pendant is in. |
| Fog | Add light interior haze, density ~0.03–0.05, desaturated dark grey-blue (0.05, 0.055, 0.06) — this is what makes the pendant's light read as a visible cone rather than a flat pool. |

Post-processing target: cold grade (cyan/teal shadows, pushed-down saturation), stronger vignette
than the other two scenes, higher contrast, light grain.

**All three scenes share one blocker:** none of this post-processing takes effect until §7's
defect #3 is fixed (`Volume.sharedProfile` is never actually assigned, and the profile asset is
empty regardless).

---

## 4. Cabin shopping list

### Ready to download now — 12 verified CC0 assets

Don't skim past this list into the per-prop tables below — those tables mix confirmed downloads
in with the honest gaps, and it's easy to read them as "nothing found." It's the opposite: this is
a real dressing pass, all Poly Haven unless noted, all license-confirmed:

| Asset | Replaces |
|---|---|
| [Mantel Clock 01](https://polyhaven.com/a/mantel_clock_01) | `Prop_MantelClock` |
| [Wine Bottles 01](https://polyhaven.com/a/wine_bottles_01) | `Prop_Bottles` (imperfect — reads as wine, not beer) |
| [Portable Cassette Player](https://polyhaven.com/a/portable_cassette_player) | `Prop_Radio` (imperfect — reads as cassette deck, not tube radio) |
| [Sofa 03](https://polyhaven.com/a/sofa_03) | `BO_Sofa` grey blockout |
| [Arm Chair 01](https://polyhaven.com/a/ArmChair_01) | new dressing |
| [Vintage Cabinet 01](https://polyhaven.com/a/vintage_cabinet_01) | new — kitchen nook |
| [Wooden Lantern 01](https://polyhaven.com/a/wooden_lantern_01) | new — table lantern |
| [Wooden Candlestick](https://polyhaven.com/a/wooden_candlestick) | new — mantel candle |
| [Wicker Basket 01](https://polyhaven.com/a/wicker_basket_01) | new — log/storage basket |
| [Pine Tree 01](https://polyhaven.com/a/pine_tree_01) | the 14 magenta `Tree.prefab` instances (fixes blocker #1) |
| [Snow 01](https://polyhaven.com/a/snow_01) or [Snow004 (ambientCG)](https://ambientcg.com/view?id=Snow004) | `SnowTerrainLayer.terrainlayer` ground texture |
| [Snowy Forest HDRI](https://polyhaven.com/a/snowy_forest) | `StormSky.mat` for the morning scene |

The gaps below (five matching mugs, the coat, curtains, fireplace tools, firewood, the woodshed
structure, rocks/logs) are genuinely thinner on CC0 sources and are mostly small or background
items — written up honestly rather than filled with a fake link, but they're the minority of the
list, not the whole thing.

### Story-critical prop replacements

These aren't decoration — they're the clue ledger (`docs/STORY_SCRIPT.md` §9). Keep the same
silhouette and footprint; dimensions below are the *actual* baked `BoxCollider` size on the
existing blockout (from `MemorySceneDressing.cs` / the scene YAML), not the builder's requested
size — match these, not the nominal ones.

| Prop | Position (world) | Real size | Sets flag | Replacement |
|---|---|---|---|---|
| `Prop_MantelClock` | (0.35, 1.45, 3.45) | 0.20 × 0.25 × 0.117 m | `saw_clock` | **[Mantel Clock 01](https://polyhaven.com/a/mantel_clock_01)** — Poly Haven, CC0. Ornate carved wood shelf clock, brass bezel, aged face. Ships at 0.5 m — scale down ~2.5×. |
| `Prop_DoorKey` | (−1.4, 1.45, −4.10 in morning) | 0.05 × 0.071 × 0.05 m | `found_key_inside` | **[CC0 - Keys](https://sketchfab.com/3d-models/cc0-keys-39300ae42e5a4c4ab962be13c3c0d730)** by plaggy — old iron keys with PBR normals. **[verify in browser]** — page shows a CC0 badge but also a conflicting CC-Attribution badge; confirm before use. |
| `Prop_CoatOnChair` | (−1.4, 1.45, −4.10) | 0.49 × 0.52 × 0.155 m | `saw_coat_swap` | **Gap.** No CC0 heavy-parka model found. Recommend keeping the existing low-poly blockout and giving it a cloth-normal material rather than chasing a download. |
| `Prop_Bottles` | (−2.7, 0.82, 2.40) | 0.11 × 0.30 × 0.05 m each | none | **[Wine Bottles 01](https://polyhaven.com/a/wine_bottles_01)** — Poly Haven, CC0. Cluster of 4, realistic glass, foil/wax tops. Reads as wine/champagne rather than beer — pull individual bottles from the cluster and rescale; not a perfect match but usable. |
| `Prop_Radio` | (−0.35, 1.35, 3.45) | 0.30 × 0.37 × 0.21 m | drives `RadioTuner` | **[Portable Cassette Player](https://polyhaven.com/a/portable_cassette_player)** — Poly Haven, CC0. Weathered teal, radio dial, tactile buttons. Reads more cassette-deck than tube radio — flagged as an imperfect stand-in, not a period radio. |
| `Prop_FiveCups` | (−3.0, 0.78, 2.15) | 0.34 × 0.075 × 0.34 m | `saw_five_cups` | **Gap.** No CC0 rustic mug/cup set found (only paid CGTrader results). Search term: `"mug" OR "cup" site:polyhaven.com`, or a Sketchfab CC0 search filtered to "mug". |
| `Prop_BlockedStairs` | (3.9, 0.20, −1.20) | 0.61 × 0.62 × 0.07 m | none | **Gap.** Not sourced this pass — low priority, it's a background silhouette. |
| `Prop_FrontWindow` (night) | (2.3, 1.60, −4.95) | 1.00 × 0.90 × 0.12 m | none | **Gap.** Not sourced — a curtained night window is small enough to keep as dressing over the existing `Cabin_v2` window opening rather than a standalone download. |
| `Prop_BrokenPane` (morning) | (2.3, 1.6, −5.0) | 0.85 × 0.90 × 0.20 m | `saw_glass_inside` | **Gap.** No CC0 shattered-pane prop found; the existing blockout with a glass-shard material is likely the pragmatic choice here. |
| `Prop_NickBody` (morning) | (2.3, 0.1, −6.3) | 1.75 × 0.26 × 0.48 m | `saw_body` | **Deliberately not sourced.** Keep the existing low-poly blockout — do not source a realistic human-remains asset. |

### General interior dressing — the "cozy, mostly wood, lived-in" layer

| Item | Replacement | Notes |
|---|---|---|
| Sofa (replaces `BO_Sofa`, untextured grey blockout, 3.5 × 1.0 m footprint) | **[Sofa 03](https://polyhaven.com/a/sofa_03)** — Poly Haven, CC0. Vintage leather, carved wood frame, scrolled arms, patterned cushions. 2.7 m wide. Smaller alternatives if the footprint's too big: [Sofa 01](https://polyhaven.com/a/Sofa_01) (1.6 m) or [Sofa 02](https://polyhaven.com/a/sofa_02) (1.8 m), both CC0. |
| Armchair | **[Arm Chair 01](https://polyhaven.com/a/ArmChair_01)** — Poly Haven, CC0. Varnished carved wood, upholstered seat, lodge read. |
| Kitchen cabinet | **[Vintage Cabinet 01](https://polyhaven.com/a/vintage_cabinet_01)** — Poly Haven, CC0. Dark varnished wood, glass-front upper doors, brass knobs. |
| Table lantern | **[Wooden Lantern 01](https://polyhaven.com/a/wooden_lantern_01)** — Poly Haven, CC0. Worn maritime-style, glass panels, candle prop. Alternatives: [Lantern 01](https://polyhaven.com/a/Lantern_01) (brass hurricane lantern) or [Brass Diya Lantern](https://polyhaven.com/a/brass_diya_lantern), both CC0. |
| Mantel candle | **[Wooden Candlestick](https://polyhaven.com/a/wooden_candlestick)** — Poly Haven, CC0. |
| Log/storage basket | **[Wicker Basket 01](https://polyhaven.com/a/wicker_basket_01)** — Poly Haven, CC0. Not firewood-specific; pair with separately modeled logs. |
| Ashtray | **[CC0 - Ashtray](https://sketchfab.com/3d-models/cc0-ashtray-313192b7abae47ffa82e4ba24e947f70)** by plaggy. **[verify in browser]**. |
| Playing cards / tabletop clutter | Not verified this pass — several candidates surfaced but none had a confirmable license. |
| Curtains, fireplace tools (poker/tongs), firewood stack | **Gaps.** Nothing CC0-confirmed for any of these three. Firewood and fireplace irons are simple enough to model directly in the existing `ArtSource/Cabin_v2.blend` pipeline. |

### Exterior

| Item | Replacement | Notes |
|---|---|---|
| Conifer trees (replace the magenta `Tree.prefab` ring, 14 instances, scale 0.72–1.02) | **[Pine Tree 01](https://polyhaven.com/a/pine_tree_01)** — Poly Haven, CC0. Full PBR, 3 trunk variants, **ships with LOD variants — use those, not the 17M-tri base mesh**, for a 14-instance ring. No snow-dusted variant exists; add snow via a top-facing-normal mask material pass or light vertex paint in Unity. | This also fixes blocker #1 — none of Poly Haven's meshes are pipeline-locked, so it shades correctly under URP/Lit once imported. |
| Woodshed (replace the non-uniformly-scaled `Garage.prefab`, target ~4 × 3 m) | **Gap — build it.** No standalone CC0 shed structure was found (Poly Haven's "The Shed" is a scattered prop collection, not a building). Model a simple pitched-roof shed in the existing Blender pipeline and skin with **[WoodSiding001](https://ambientcg.com/view?id=WoodSiding001)** or **[WoodSiding010](https://ambientcg.com/view?id=WoodSiding010)** — both ambientCG, CC0, weathered/mossy plank textures rougher than the main cabin's siding. | |
| Rocks / fallen logs / underbrush | **Gap.** Poly Haven's rock/nature category listings didn't render for automated fetch this pass — worth a direct in-browser look at `polyhaven.com/textures/rock` and `polyhaven.com/models`. Fallback: simple Blender blockouts skinned with an ambientCG CC0 rock texture. | |
| Snow ground material | **[Snow 01](https://polyhaven.com/a/snow_01)** — Poly Haven, powdery snow with footprints/trail ruts. **[Snow 04](https://polyhaven.com/a/snow_04)** for trampled/disturbed snow near the door and body-carry path. Independently confirmed alternative: **[Snow004](https://ambientcg.com/view?id=Snow004)** — ambientCG, CC0 explicitly confirmed on-page, clean white procedural snow, JPG/PNG (may integrate more easily into a `.terrainlayer` than Poly Haven's EXR set). | Pair with the existing `SnowTerrainLayer.terrainlayer` (currently 6×6 m tile, diffuse+normal only). Blocked on the corrupt `CabinNightTerrain.asset` landing (blocker #8). |
| Overcast morning sky | **[Snowy Forest](https://polyhaven.com/a/snowy_forest)** — Poly Haven, CC0. Overcast, soft cool diffuse light, low contrast, mossy floor with snow patches. This is the strongest single match for the eerie-morning tone target in this whole list. | Use directly as the morning skybox/reflection source. |
| Night/storm sky | No exact CC0 "snowy forest, night, storm" HDRI exists. **Recommend keeping the current procedural approach** (directional moonlight + a plain dark gradient or starfield skybox) rather than chasing a literal storm HDRI that isn't out there. [Winter Evening](https://polyhaven.com/a/winter_evening) (Poly Haven, CC0) is a fallback reference if a lit-window night HDRI is wanted instead, but it reads as calm rather than stormy. | |
| Snow particle sprite | Not sourced — and likely unnecessary. `Assets/_Project/CabinNight/Data/Snowflake.png` already exists and is simply never wired to `Windblown Snow`'s particle material (blocker #4). That's a one-line fix, not a sourcing gap. | |

---

## 5. Interrogation shopping list

Lower priority per your call, but worth noting up front: per `Assets/_Project/ASSETS_TODO.md`,
only `PlayerSeatAnchor` and the `Cop` transform are functionally load-bearing — the placeholder
cubes are purely cosmetic and safe to fully replace. Also worth fixing while dressing this room:
the cop currently **stands** and clips through his own chair placeholder, and the seated camera
sits level at 1.2 m, which frames his chest rather than his face (§2 of the earlier scene survey)
— seat him properly once a real chair exists.

| Item | Replacement | Notes |
|---|---|---|
| Table (replaces the 1.2 × 0.75 × 0.7 m slab placeholder) | **[Metal Office Desk](https://polyhaven.com/a/metal_office_desk)** — Poly Haven, CC0. Worn grey industrial desk, dual pedestal drawers, chrome handles, tapered legs. Ships at 2 m wide — either scale down to 1.2 m or use as a modeling reference for a smaller slab built in the existing Blender pipeline. | |
| Chairs (×2, replace the 0.5 × 0.9 × 0.5 m boxes) | **Gap.** No CC0 tubular-steel institutional chair found — Poly Haven's `ArmChair_01` is the wrong register (Victorian upholstered). Recommend a simple bent-tube-frame + thin pad build using the metal textures below; genuinely quick geometry. | |
| Pendant light fixture | **[Security Light](https://polyhaven.com/a/security_light)** — Poly Haven, CC0. Weathered industrial fixture, fluted metal shade, frosted glass. Not a classic wire-cage bulb, but reads institutional. A built wire-cage-and-bulb primitive is an equally valid alternative. | |
| Fluorescent ceiling fixtures | **[Mounted Fluorescent Lights](https://polyhaven.com/a/mounted_fluorescent_lights)** — Poly Haven, CC0. Grey metal housings, end caps, chain-hung, subtle wear. Strong match. | |
| Radiator | **Gap.** Nothing CC0-confirmed. Recommend a simple ribbed-panel primitive skinned with the painted-metal texture below — radiators are repetitive enough geometry that a blockout reads fine at interrogation-room distance. | |
| Filing cabinet | **Gap.** Nothing individually confirmed CC0, though [plaggy's CC0 collection on Sketchfab](https://sketchfab.com/plaggy/collections/cc0-public-domain-free-models-c1af6539a9ee49f4b3d51fabd6c25a85) is the right hunting ground and wasn't fully enumerated. Fallback: boxy primitive + drawer strip + painted metal. | |
| Door with observation slot | **Gap.** No CC0 model found. The project's own `Assets/_Project/Art/Cabin_v2/Door.fbx` Blender workflow adapts directly — a flat steel door is simpler geometry than the plank-and-batten cabin door already built. | |
| One-way mirror / observation panel | Not a modeling problem — a dark/reflective glass URP material on a wall-recessed primitive pane is sufficient. No asset needed. | |
| Reel-to-reel recorder | **Gap, and a licensing trap.** Several period-correct museum-piece models exist on Sketchfab (Melodia, MTV-10, ZK-140 T) but search results explicitly flagged that some reel-to-reel results there are **CC-BY-NC-SA, not CC0** — do not use any without opening the page and reading the badge yourself. A simpler period-plausible box-recorder shape is lower risk if nothing clears. | |
| Enamel mug | **[CC0 - Mug 7](https://sketchfab.com/3d-models/cc0-mug-7-2cb9f4ffe72749c5841c164a857e7d78)** by plaggy. **[verify in browser]**. | |
| Papers / folder | **[CC0 - Paper](https://sketchfab.com/3d-models/cc0-paper-b0776948a05f4766a03856223344b264)** by plaggy. **[verify in browser]**. | |
| Wall material — peeling green paint | **[Painted Plaster 003](https://ambientcg.com/view?id=PaintedPlaster003)** — ambientCG, CC0 confirmed. Worn green plaster, scratches, aged. | |
| Metal surfaces — weathered painted (door/radiator/cabinet) | **[Painted Metal 006](https://ambientcg.com/view?id=PaintedMetal006)** — ambientCG, CC0 confirmed. Green-to-rust patina. | |
| Metal surfaces — bare scratched steel (table/chairs) | **[Metal 038](https://ambientcg.com/view?id=Metal038)** — ambientCG, CC0 (site-wide license). | |
| Metal surfaces — black powder-coat (chair frames) | **[Metal 028](https://ambientcg.com/view?id=Metal028)** — ambientCG, CC0 (site-wide license). | |
| Floor | **Gap on linoleum specifically** — only non-CC0-confirmed third-party hits turned up. Recommend worn bare concrete instead, which fits "cold Soviet" better anyway: **[Concrete012](https://ambientcg.com/view?id=Concrete012)** or **[Concrete034](https://ambientcg.com/view?id=Concrete034)**, both ambientCG CC0. | |

---

## 6. Import checklist

1. **Render pipeline.** Anything pulled from a non-URP source (most of the existing `Assets/Cabin/`,
   `Assets/TDG Storage Solutions/` packs are Built-in/Standard) needs
   **Window → Rendering → Render Pipeline Converter → Built-in to URP** after import. Poly Haven
   and ambientCG assets are pipeline-neutral and don't need this step.
2. **Texel density.** Match the rule already documented in
   [`Assets/_Project/Art/Cabin_v2/README.md`](../Assets/_Project/Art/Cabin_v2/README.md): one wood
   texture tile = 2.0 m on cube-projected surfaces, so new wood matches the existing floor/wall/
   stair scale.
3. **Roughness → Smoothness.** ambientCG and Poly Haven ship *Roughness* maps; URP/Lit wants
   *Smoothness*. Invert the channel when wiring the material, same as `Cabin_v2/README.md` already
   flags for its own textures.
4. **`.glb`/`.gltf` are not LFS-tracked.** `cop.glb` (3.3 MB) is already sitting in the repo as a
   raw blob because of this gap. Add `*.glb lfs` and `*.gltf lfs` to `.gitattributes` before
   importing any glTF download.
5. **Where to commit.** CC0 assets are redistributable, unlike the existing gitignored Asset Store
   packs (`Assets/Cabin/`, `Assets/TDG Storage Solutions/`, etc., which are ignored under a
   "re-download instead of committing" policy — and that policy is exactly why `Assets/o3n/` is
   currently missing and breaking all five character prefabs, blocker #5). Commit new CC0 downloads
   under `Assets/_Project/Art/Sourced/<Cabin|CabinExterior|Interrogation>/` instead.
6. **Disclosure.** Third-party asset disclosure is a scored submission requirement (G4), and task
   Z4 is already open — the README's third-party table is missing rows for `o3n`, `IL3DN`, `TDG`,
   and the `Cabin` pack. Every asset actually placed from this document needs its own row: name,
   source URL, license, what it's used for.
7. **Sourcing policy** (already stated in `ASSETS_TODO.md`, restated here since it governs
   everything above): only Unity Asset Store (including free tier), syntystore.com, itch.io CC0
   packs, Sketchfab filtered to CC0, ambientCG, Poly Haven, or Mixamo. Not the piracy-mirror
   reuploads of any of these that turn up in a plain web search.

---

## 7. Blockers

Things that will sabotage any art pass — or make it impossible to judge — until fixed. Not art
tasks; code/data fixes.

| # | Defect | Evidence |
|---|---|---|
| 1 | **The 14 pine trees render magenta.** `Assets/Cabin/Terrain/Tree/Tree.prefab`'s `Bark.mat`/`Leaves.mat` use Built-in `Nature/Tree Soft Occlusion` shaders (`fileID: 10600`/`10606`), which URP cannot render. Confirmed visually via scene-view capture. Fixed by the §4 tree replacement, or by running the Built-in→URP converter on the existing materials. | |
| 2 | **The morning scene runs on night fog, night ambient, and the night skybox.** Only the directional light differs between the two scene files — fog color `(0.055, 0.075, 0.12)` and the ambient gradient are identical in both. This is the single biggest reason morning doesn't currently read as morning. | both scene YAMLs |
| 3 | **Post-processing never applies, in any scene.** `MemorySceneBuilderV2.cs:284` assigns the volume profile via `GetProperty("sharedProfile")`, but `Volume.sharedProfile` is a public *field*, not a property — the `GetProperty` call returns null and the `?.` silently no-ops. Separately, `CabinNightVolume.asset` itself has `components: []` — even a correct assignment would apply nothing today. Both need fixing before any of §3's grading values do anything. | |
| 4 | **The snowfall renders invisible.** `Windblown Snow`'s `ParticleSystemRenderer.m_Materials` is `[{fileID: 0}]` while `Assets/_Project/CabinNight/Data/Snowflake.png` sits unused. One-line fix: assign a particle material referencing that sprite. | |
| 5 | **`Assets/o3n/` is missing**, so all five character prefabs (Aaron, Ivy, Nick, Priya, and the player body) have broken mesh references. It's gitignored under the "Asset Store packs, re-download instead of committing" policy and was never re-fetched on this machine. | `CabinNightCharacterBuilder.cs` hardcodes `Assets/o3n/...` paths |
| 6 | **The interrogation scene's dark ambient gradient is authored but inert** — `m_AmbientMode: 0` (Skybox) means the bright default procedural sky drives ambient instead of the gradient that's already sitting in the scene file. One-line fix, folded into §3's interrogation lighting steps above. | |
| 7 | Morning has both a standing `Nick Vlahos` character prefab **and** `Prop_NickBody` at the same world position (2.3, ·, −6.3) — a live idling body overlapping a corpse prop. Also, the morning player spawn (0.75, 0, 0.25) sits inside `BO_Sofa`'s own collider (x ∈ [0.25, 1.25], z ∈ [−1.5, 2.0]). Both are pre-existing bugs in `CabinNightCharacterBuilder.cs`, unrelated to art sourcing but will visibly break any morning dressing pass until fixed. | `CabinNightCharacterBuilder.cs:58` |
| 8 | **`CabinNightTerrain.asset` is still corrupt** (3 bytes short of its own header's declared size, from a prior git EOL-normalization bug — see the separate fix session). Until the original author's copy lands, there's no snow terrain to apply §4's ground material to. | |

---

## Summary — what to actually do first

1. Fix blockers **#1–#4** (magenta trees, morning's stolen night fog, dead post-processing, invisible
   snow) — none of the art choices above can be judged accurately until these are gone.
2. Land **blocker #8** (the terrain file) — get `CabinNightTerrain.asset` from the original author's
   working copy, then apply the §4 snow material.
3. Cabin story props first (§4, story-critical table) — these carry the mystery's clues.
4. Cabin general dressing + exterior (§4 remainder).
5. Interrogation room (§5) — lower priority per your call, but the highest visual payoff per asset
   once you get to it, since it's currently 8 grey cubes and one shadowless light.
