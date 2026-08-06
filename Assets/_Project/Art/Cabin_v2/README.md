# Cabin_v2 — First-Floor Cabin (Model Files Only)

Source: `ArtSource\Cabin_v2.blend` (kept outside `Assets\` deliberately — Unity
auto-imports `.blend` files when Blender is installed, which would create a
duplicate model alongside `Cabin.fbx`).

Built from scratch to a **10 × 10 m interior footprint, 2.7 m ceiling**,
textured with the Poly Haven `weathered_plank_siding_4k` PBR set (wood),
`red_brick_4k` (fireplace) and `blue_metal_plate_4k` (grille/hanger/shoes).
Second floor is out of scope — only the ceiling slab and stair opening are
built.

**Revision note:** this is a shrink of an earlier 12 × 12 m / 3.0 m-ceiling
build. Every item kept its designated place in the layout; only the room and
the objects whose size is *derived* from the room (walls, slabs, stairs,
railing, chimney) changed size or position. See "Coordinate reference" below
for the exact deltas if you need to reconcile against old notes.

## Object inventory

### `Cabin.fbx` (everything except the door)

| Object | Description |
|---|---|
| `SM_Cabin_Floor` | 10.4 × 10.4 × 0.2 m slab, top at z = 0 |
| `SM_Cabin_Walls` | Single mesh: N/E/S/W walls + 45° chamfered NE corner, door + window openings cut |
| `SM_Cabin_Ceiling` | 10.4 × 10.4 × 0.2 m slab at z = 2.7–2.9, stair opening cut |
| `SM_Cabin_Roof` | Flat 10.8 × 10.8 × 0.15 m deck, 0.2 m overhang, underside at z = 2.9, separate object |
| `SM_Cabin_Stairs` | 16 risers (0.18125 m) × 15 treads (0.35 m, 5.25 m run) along the West wall, solid stepped mass under the full run, flush to the floor (z = 0) — no floating stringers |
| `SM_Cabin_StairRailing` | Newels/balusters/handrail on the open east side; rail height is capped at z = 2.6 (rather than continuing to rise with the rake) so it stays clear of the 2.7 m ceiling and the roof deck |
| `SM_Table` | Dining table, (+3.0, −2.3) |
| `SM_Chair_01`…`06` | Dining chairs around the table, facing inward with ±3° yaw jitter |
| `BO_Sofa` | Untextured grey blockout, 5-seater, 3.5 × 1.0 m, (−0.75, −0.25), facing +X |
| `BO_Fireplace` | **Red brick** (`M_Brick_Red`), centred on the South wall at (0, ~−4.3), chimney breast runs to the new ceiling (z = 2.7) |
| `BO_WindowGrille` | **Blue metal plate** (`M_Metal_BluePlate`), inset in the North window reveal at x = −2.3, 15 vertical + 8 horizontal bars, ≤ 0.12 m clear gaps — too tight to pass a person through |
| `BO_CoatHanger` | **Blue metal plate**, free-standing hall tree (disc base + pole + 6 angled pegs, 1.8 m tall) at (+1.4, +4.5), off the North wall |
| `BO_Shoes_01`…`04` | **Blue metal plate**, pairs along the East wall at x = +4.6, y = 2.4/1.7/1.0/0.3 |

### `Door.fbx`

| Object | Description |
|---|---|
| `SM_Door` | Plank-and-batten door, 1.05 × 2.10 × 0.055 m. **Origin is at the bottom of the hinge edge** (not the FBX bounding-box centre — this is the specific defect in the old `Assets\Cabin\Prefabs\Door.prefab` that this rebuild avoids). Mesh and export are **unchanged** from the previous revision — only its placement coordinate (below) moved with the shrunk chamfer. |

## Coordinate reference — what changed in this revision

The shell shrank from a 12 × 12 m / 3.0 m-ceiling footprint to 10 × 10 m /
2.7 m. The interior span went from `[−6, +6]` to `[−5, +5]` on both axes — a
uniform 1.0 m inward shift on every wall. The NE chamfer line moved from
`x + y = 9.5` to `x + y = 7.5` (a pure translation by `(−1, −1)`; the 45°
angle and 3.54 m diagonal are unchanged).

Everything that isn't part of the shell itself either moved by the wall's
shift on its own axis (window, grille, sofa, table, chairs, door — all
`(−1, −1)` where they sit near the NE chamfer, or a single-axis 1.0 m shift
where they sit against a single wall) or was rebuilt at the new scale (stairs,
railing, fireplace, whose *size* — not just position — depends on the
room dimensions).

The staircase changed from 18 risers × 17 treads (5.95 m run, 3.2 m
floor-to-floor) to **16 risers × 15 treads (5.25 m run, 2.9 m
floor-to-floor)** — the riser height (0.18125 m) is re-derived so
`16 × 0.18125 = 2.9` exactly. The ceiling's stair opening is derived from
headroom the same way as before: it starts where clearance over a tread first
drops under 2.0 m (now tread 4, `y = +0.35`) and runs to `y = −3.95`, just
past the top landing.

## Door hinge — placement instructions for Unity

The door's local origin *is* the hinge pivot — `SM_Door`'s mesh vertices are
built and baked so that local `(0,0,0)` sits exactly at the bottom of the
hinge edge, and `Door.fbx` was exported with the object's transform at
identity (location, rotation, and scale all zero/one) so nothing is
double-offset. The hinge sits on the chamfer's north/west end (nearer the
window), matching the "hinge on the chamfer's west/left end" spec.

**Hinge coordinate, in the source `.blend`'s native Blender Z-up frame:**

| X | Y | Z (up) |
|---|---|---|
| 3.378769 | 4.121231 | 0.0 |

(This revision's shell shift moved it by exactly `(−1, −1)` from the previous
value of `(4.378769, 5.121231, 0.0)`.)

⚠️ **This number has not been converted to Unity's frame, and that conversion
was deliberately not guessed here.** The FBX was exported with
`bake_space_transform=False` (per spec), which means Blender does **not**
bake an axis-conversion matrix into the numbers — it just writes Blender's
raw local coordinates and flags the file's up/forward axes, leaving the
actual remapping to Unity's own FBX importer at import time. That remapping
is Unity-implementation-defined and can't be verified from inside Blender.

**To place the door correctly:**
1. Import `Cabin.fbx` into a test scene and expand it — note where
   `SM_Chair_05` (a recognizable, asymmetric reference point) lands in the
   Inspector. Its known Blender-frame coordinate is now `(3.0, −0.85, 0.0)`
   (was `(4.0, −1.15, 0.0)` before this revision).
2. Compare the two to derive the actual axis mapping Unity applied (commonly
   `Unity(x,y,z) = Blender(x,z,y)` with no sign flips for this exact
   `axis_forward='-Z'`/`axis_up='Y'` combination — but confirm it against
   step 1 rather than assuming it).
3. Apply that same derived mapping to the hinge coordinate above and use the
   result as `SM_Door`'s Transform Position.
4. Rotating the door's local vertical axis swings it on the hinge. **Which
   sign opens it inward vs. outward was not confirmed through an actual FBX
   round-trip** (rotation handedness can flip again during the same
   import-time axis remapping as the position). In the Blender source file,
   rotating `SM_Door` by **negative** Z keeps the free edge swinging toward
   the room interior (positive Z visibly swings it outward, past the
   exterior wall line) — treat that sign as a starting guess only and
   confirm visually once the door is in the Unity scene.

## Materials

| Material | Objects | Source (Poly Haven) |
|---|---|---|
| `M_Wood_WeatheredPlank` | Floor, walls, ceiling, roof, stairs, railing, table, chairs, door | `weathered_plank_siding_4k` |
| `M_Brick_Red` | `BO_Fireplace` | `red_brick_4k` |
| `M_Metal_BluePlate` | `BO_WindowGrille`, `BO_CoatHanger`, `BO_Shoes_01`…`04` | `blue_metal_plate_4k` |
| `M_Blockout_Grey` | `BO_Sofa` only | none — flat colour, untextured by explicit request |

All three textured materials follow the same Principled BSDF wiring: Base
Color (sRGB) ← diffuse JPG, Roughness (Non-Color) ← roughness map, Normal
(Non-Color → Normal Map node) ← normal map, Metallic = 0 for all (the blue
plate is painted sheet metal, not bare metal). No `*_disp_4k` map is wired
for any of them — this is a game asset with no displacement/tessellation
pipeline.

## Texel-density rules

Every wood-textured object uses `weathered_plank_siding_4k` with **one
texture tile = 2.0 m** on every surface (cube projection, `cube_size=2.0`),
so plank scale reads identically across the floor, walls, stairs, and
furniture. The chamfer wall faces and the door (both physically at 45° to the
world axes) were unwrapped on an axis-aligned duplicate and had their UVs
transferred back — a plain cube projection on those faces would otherwise
compress the texture ~1.41× relative to everything else.

`BO_Fireplace` uses `cube_size=1.5` for brick — matched to the object's own
scale (roughly a dozen brick courses across the 1.4 m firebox) rather than
the wood rule, since 2.0 m would make the bricks read oversized on an object
this small.

`BO_CoatHanger` and `BO_Shoes_01`…`04` use `cube_size=1.0` for blue metal.
`BO_WindowGrille` uses its own smaller `cube_size=0.25`, because its bar
faces are only 0.04 m wide — at 1.0 m each face would sample a flat,
featureless 4% crop of the texture and read as untextured plastic instead of
painted metal.

Each wood object also has a second UV channel (`UVMap_Lightmap`, generated via
`smart_project`) for Unity lightmapping, in addition to the primary `UVMap`
used for the diffuse/normal/roughness textures. The brick and metal materials
are small props with no baked-lighting requirement, so they only have the
primary `UVMap`.

## Textures

`Textures\` contains the game-ready set (re-saved from the 4K Poly Haven
sources, which ship oversized/wrong-format files for a Unity asset):

- `weathered_plank_siding_diff_4k.jpg`, `red_brick_diff_4k.jpg`,
  `blue_metal_plate_diff_4k.jpg` — copied as-is (Base Color, sRGB)
- `Cabin_Normal.png` — wood normal map, converted from the source EXR at
  **16-bit** (62 MB — 3× larger than the EXR it replaces; kept from the
  previous revision, not touched here). Re-export at 8-bit if file size
  matters more than precision.
- `Brick_Normal.png`, `Metal_Normal.png` — normal maps for the new materials,
  converted at **8-bit** deliberately (unlike the wood one above) to avoid
  repeating that size tradeoff twice more; still tens of MB each because PNG
  compresses noisy normal-map detail poorly, but roughly half the size a
  16-bit version would have been.
- `Cabin_Rough.png`, `Brick_Rough.png`, `Metal_Rough.png` — 8-bit greyscale
  roughness maps (Non-Color). **Invert into the Smoothness channel** when the
  URP/Lit materials are authored in Unity — these are Roughness, not
  Smoothness.
- `disp_4k.png` files — **not copied** for any material; this is a game
  asset, no displacement/tessellation pipeline.

## Outstanding work (not done here)

- No Unity scene edits, no prefabs, no URP materials, no `DoorInteractable`
  script. Materials must be authored in Unity: `M_Wood_WeatheredPlank`,
  `M_Brick_Red`, and `M_Metal_BluePlate` each map to Base Color + Normal +
  inverted-Roughness-as-Smoothness above; `M_Blockout_Grey` is a flat-color
  placeholder with no textures.
- Wiring these into `Assets\_Project\Scenes\NobodyWentOut_CabinNight.unity`
  and replacing the old `Assets\Cabin\` prefabs is a separate follow-up.
- The existing `Assets\Cabin\` package was left untouched.

## Known minor notes

- The `weathered_plank_siding_4k` source folder that was in
  `C:\Users\Giorg\Downloads\` earlier is no longer present on disk as of this
  revision. This didn't block anything — Blender already had the images
  loaded in memory from the previous session, the Unity-side diffuse JPG was
  already copied into `Textures\` before the folder disappeared, and this
  export uses `path_mode='STRIP'` (see below) which never needs the source
  files to exist. If you re-open `ArtSource\Cabin_v2.blend` on a machine
  without that folder, the wood material's image nodes will show as
  "missing file" in Blender's UI even though the FBX export is unaffected;
  re-point them at wherever that Poly Haven set now lives if you need to
  re-render or re-export.
- This revision's FBX export uses `path_mode='STRIP'` instead of the previous
  `'COPY'`. `'COPY'` was what created the `Cabin.fbm\` folder that had to be
  manually deleted after every previous export (it dumps the raw 4K
  EXR/JPG source beside the model). `'STRIP'` never creates it. This is safe
  because all materials are authored by hand in Unity anyway (see
  "Outstanding work" above), so the FBX's embedded texture file paths are
  never read.
- Stair railing height is capped at z = 2.6 rather than continuing to climb
  with the rake all the way to the top landing — an uncapped rail would have
  reached roughly z = 3.6 at the top newel, clipping through both the 2.7 m
  ceiling and the 2.9 m roof deck. The rail is a sloped segment near the
  bottom of the run and a flat segment (at z = 2.6) for the upper portion,
  which sits comfortably under the ceiling since that whole stretch of the
  stairwell is within the ceiling opening anyway.
