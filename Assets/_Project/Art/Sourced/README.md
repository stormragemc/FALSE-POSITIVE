# Sourced asset set — Cabin cozy-decor pass

All source files under `Cabin/` are CC0 — nine from [Poly Haven](https://polyhaven.com/)
downloaded 7 August 2026, four more (three Poly Haven, one [ambientCG](https://ambientcg.com/))
downloaded 9 August 2026 for the interior-decor pass documented in
[`Assets/_Project/Art/Cabin_v2/README.md`](../Cabin_v2/README.md) ("Decor pass" section).
Both sources publish under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/), so
attribution is not required. This file and `manifest.json` keep the build reproducible and make
the third-party content explicit anyway — see `Assets/README.md`'s third-party disclosure table
for the project-wide summary.

The first nine assets below were imported in the same pass as
[`Assets/_Project/Art/PolyHaven/`](../PolyHaven/README.md) but were never recorded in a manifest
of their own until now; this file and `manifest.json` backfill that gap.

## Imported assets

| Asset | Author(s) | Resolution | In-game use |
|---|---|---:|---|
| [`ArmChair_01`](https://polyhaven.com/a/ArmChair_01) | Kirill Sannikov | 2K | `Decor_ArmChair` — armchair by the fireplace |
| [`vintage_cabinet_01`](https://polyhaven.com/a/vintage_cabinet_01) | Rico Cilliers | 2K | `Decor_Cabinet` — cabinet against the fireplace wall |
| [`wicker_basket_01`](https://polyhaven.com/a/wicker_basket_01) | Kuutti Siitonen | 2K | `Decor_LogBasket` — log basket beside the hearth |
| [`wooden_lantern_01`](https://polyhaven.com/a/wooden_lantern_01) | James Ray Cock | 2K | `Decor_TableLantern` — lantern on the dining table |
| [`wooden_candlestick`](https://polyhaven.com/a/wooden_candlestick) | Josh Dean | 2K | `Decor_MantelCandle` — candlestick on the mantel |
| [`wine_bottles_01`](https://polyhaven.com/a/wine_bottles_01) | Rico Cilliers, Jurita Burger | 2K | Not yet placed — reserved for a future `Prop_Bottles` retarget, see below |
| [`mantel_clock_01`](https://polyhaven.com/a/mantel_clock_01) | Rico Cilliers, Yann Kervran | 2K | Not yet placed — reserved for a future `Prop_MantelClock` retarget |
| [`portable_cassette_player`](https://polyhaven.com/a/portable_cassette_player) | Mateusz Sadek | 2K | Not yet placed — reserved for a future `Prop_Radio` retarget |
| [`sofa_03`](https://polyhaven.com/a/sofa_03) | Fran Calvente | 2K | Not yet placed — duplicate of the copy already used by `Art/PolyHaven/` for `BO_Sofa`; deferred, see below |
| [`Shelf_01`](https://polyhaven.com/a/Shelf_01) | Gabriel Radić | 2K | `Decor_Shelf` — freestanding bookcase on the window wall |
| [`book_encyclopedia_set_01`](https://polyhaven.com/a/book_encyclopedia_set_01) | DaDrood | 2K | `Decor_Books` — books on `Decor_Shelf` |
| [`ceramic_vase_01`](https://polyhaven.com/a/ceramic_vase_01) | James Ray Cock | 2K | `Decor_Vase` — vase on `Decor_Shelf` |
| [`Carpet016`](https://ambientcg.com/view?id=Carpet016) | ambientCG (Lennart Demes) | 2K | `Decor_Rug_Hearth`, `Decor_Rug_Dining`, `Decor_WallArt_02` |

`manifest.json` records each file's source URL and checksum where one is meaningful — see its
`note` field for why the first nine entries carry `"checksum_source": "not_tracked"` instead of a
published md5.

## Why three props are unplaced

`wine_bottles_01`, `mantel_clock_01` and `portable_cassette_player` are not general decoration —
they are the intended real models for `Prop_Bottles`, `Prop_MantelClock` and `Prop_Radio`, which
`MemorySceneDressing.cs` already places and, for the radio, wires to `RadioTuner` and two
`AudioSource`s. Placing the Sourced copies as separate objects in `CabinV2Builder.cs` would create
duplicates that z-fight with the story props and steal `InteractionRaycaster` hits. Retargeting the
existing props at these models is a `MemorySceneDressing.cs` change, not a `CabinV2Builder.cs` one,
and is left for a follow-up pass.

`sofa_03` is not swapped onto `BO_Sofa` in this pass because `CutsceneStage.cs` resolves three
actor rest spots via `GameObject.Find("BO_Sofa")`, and the swap helper in `CabinV2Builder.cs`
(`SwapOneFurnitureObject`) disables the object it replaces — `GameObject.Find` does not see
inactive objects, so the swap would silently drop those rest spots to an unyawed fallback inside
the new sofa mesh. That needs a `CutsceneStage.cs` change alongside the swap, not a decor-only one.

## Unity processing

- Models use FBX only. Blender, glTF, USD and displacement downloads are deliberately omitted,
  matching the policy already stated in `Art/PolyHaven/README.md`.
- The nine pre-existing assets' texture files were renamed from Poly Haven's `_2k` suffix scheme
  to a flat `_diff/_nor/_arm` scheme during their original import, and each carries a
  project-generated `_metallicSmoothness.png` (metallic in RGB, `1 - roughness` in alpha) — same
  convention as `Art/PolyHaven/README.md` documents for its own `*_metal_smooth_*.png` files. The
  four newer downloads keep their original Poly Haven/ambientCG filenames unmodified.
- `Shelf_01` and `ceramic_vase_01` use Poly Haven's `.exr` normal maps (no `.jpg` normal was
  published for either); `book_encyclopedia_set_01` uses the `.jpg` cover normal map only — the
  paper texture set was not imported, since the books are seen closed on a shelf.
- `Carpet016` was downloaded as ambientCG's 2K-JPG zip; only `_Color.jpg` and `_NormalGL.jpg` were
  kept and renamed to `_diff_2k.jpg` / `_nor_gl_2k.jpg` for consistency with the Poly Haven naming
  used everywhere else in this project. Displacement, AO, and Roughness were discarded — the
  project sets a flat `_Smoothness` scalar per material rather than sampling a roughness map (see
  `Cabin_v2/README.md`), and the `_arm` shortcut every Poly Haven texture set uses on this
  project's flat-smoothness materials.
- ambientCG does not publish per-file checksums the way Poly Haven's API does; `Carpet016`'s
  manifest entries record a locally-computed SHA-256 instead, marked `"checksum_source": "computed"`.

## Scene integration

None of these assets are wired into any scene yet by way of this README — see
`Assets/_Project/Art/Cabin_v2/README.md`'s "Decor pass" section for the actual placement, which is
built procedurally by `CabinV2Builder.cs` into `Cabin_v2.prefab` (and therefore appears in
`Memory_CabinNight.unity`, `Memory_CabinMorning.unity`, and the `MainMenu.unity` 3D backdrop, since
all three hold an instance of that prefab).
