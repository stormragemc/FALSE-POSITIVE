# Poly Haven asset set

All source files in this directory were downloaded from [Poly Haven](https://polyhaven.com/)
on 7 August 2026. Poly Haven publishes these assets under
[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/), so attribution is not
required. This file and `manifest.json` keep the build reproducible and make the third-party
content explicit anyway.

## Imported assets

| Asset | Author(s) | Resolution | In-game use |
|---|---|---:|---|
| [`metal_office_desk`](https://polyhaven.com/a/metal_office_desk) | Ulan Cabanilla | 2K | Interrogation desk |
| [`SchoolChair_01`](https://polyhaven.com/a/SchoolChair_01) | Ethan Place | 2K | Officer and player chairs |
| [`caged_hanging_light`](https://polyhaven.com/a/caged_hanging_light) | Ulan Cabanilla | 1K | Interrogation ceiling fixture |
| [`binder_notebook`](https://polyhaven.com/a/binder_notebook) | DaDrood | 1K | Evidence binder on the desk |
| [`sofa_03`](https://polyhaven.com/a/sofa_03) | Fran Calvente | 2K | Cabin sofa and wake/sleep staging |
| [`rubber_boots`](https://polyhaven.com/a/rubber_boots) | L | 1K | Four pairs of cabin footwear |
| [`concrete_wall_001`](https://polyhaven.com/a/concrete_wall_001) | Dimitrios Savva (photography), Rico Cilliers (processing) | 2K | Interrogation walls and ceiling |
| [`smooth_concrete_floor`](https://polyhaven.com/a/smooth_concrete_floor) | Dimitrios Savva | 2K | Interrogation floor |

`manifest.json` records each downloaded file's Poly Haven CDN URL and published MD5 checksum.
Every downloaded file was verified before being moved into its final path.

## Unity processing

- Models use FBX only. Blender, glTF, USD and displacement downloads are deliberately omitted.
- Color maps are JPG; masks that need lossless values are PNG. Normal maps use Poly Haven's
  OpenGL (`nor_gl`) convention and import as Unity normal maps without flipping green.
- `*_metal_smooth_*.png` files are project-generated URP masks: metallic in RGB and
  `1 - roughness` in alpha.
- `sofa_03_fringe_base_2k.png` is project-generated from diffuse RGB plus the supplied opacity
  map in alpha, allowing the fringe material to use URP alpha clipping.
- Each model has a project-local URP/Lit material and a grounded prefab with a box collider.
- The source FBX and texture files remain beside the generated Unity assets so the import can be
  audited or rebuilt without locating another copy.

## Scene integration

- `Interrogation.unity` retains the existing `Room/Table`, chair, `Cop`, `Player`, and
  `PlayerSeatAnchor` containers/references. Imported visuals sit below those containers.
- `Memory_CabinNight.unity` and `Memory_CabinMorning.unity` replace the sofa and footwear
  blockouts. The authored brick fireplace and coat rack remain because the radio, clock and
  coat staging depend on them.
- The legacy Tree Creator materials in `Assets/Cabin/Terrain/Tree/Tree.prefab` were replaced
  with project-local URP bark and alpha-clipped leaf materials under
  `Assets/_Project/Art/Environment/Materials/`.
