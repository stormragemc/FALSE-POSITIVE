"""Converts the two PolyHaven furniture `.blend` files (table, stool) into
clean FBX assets Unity can import without also auto-importing the raw
`.blend` — see `Assets/_Project/Art/Cabin_v2/README.md`'s own note (and
this repo's established convention): Unity auto-imports any `.blend` file
found under `Assets/` when Blender is installed, which creates a duplicate
model alongside a hand-exported FBX of the same source. `Cabin_v2.blend` and
`Props_Drinks.blend` both live in `ArtSource/` (outside `Assets/`) for
exactly this reason; these two furniture sources were dropped directly under
`Assets/_Project/Art/Furniture/` and need the same treatment.

Unlike `rig_newcop.py`, there's no rigging/posing here — PolyHaven's static
props import at whatever scale they were authored at (not necessarily this
project's real-world convention; see `MemorySceneDressing.py`... i.e.
`CabinV2Builder.cs`'s furniture-swap step, which corrects the *scale*
in Unity afterward rather than baking a scale correction here — this script
just does the format conversion). Run once per source file.

Each `.blend` is OPENED FRESH (bpy.ops.wm.open_mainfile), not appended into
a shared scene, since each ships its own complete self-contained scene
(single object + materials) — no cross-file state to worry about.

Run headless:
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" ^
    --background --factory-startup --python Tools\\blender\\export_furniture.py
"""
import os

import bpy

PROJECT_ROOT = r"C:\Users\Giorg\Documents\Unity\FALSE-POSITIVE"

# (source .blend under Assets/, destination FBX under Assets/, also under Assets/)
SOURCES = [
    (
        os.path.join(PROJECT_ROOT, r"Assets\_Project\Art\Furniture\WoodenTable_01_4k.blend\WoodenTable_01_4k.blend"),
        os.path.join(PROJECT_ROOT, r"Assets\_Project\Art\Furniture\WoodenTable_01.fbx"),
    ),
    (
        os.path.join(PROJECT_ROOT, r"Assets\_Project\Art\Furniture\wooden_stool_01_4k.blend\wooden_stool_01_4k.blend"),
        os.path.join(PROJECT_ROOT, r"Assets\_Project\Art\Furniture\WoodenStool_01.fbx"),
    ),
]


def log(msg):
    print(f"[export_furniture] {msg}")


def export_one(blend_path, fbx_path):
    log(f"Opening {blend_path}")
    bpy.ops.wm.open_mainfile(filepath=blend_path)

    mesh_objects = [o for o in bpy.data.objects if o.type == "MESH"]
    log(f"  {len(mesh_objects)} mesh object(s): {[o.name for o in mesh_objects]}")
    for o in mesh_objects:
        o.select_set(True)

    # Same conventions as rig_newcop.py's export_fbx: Blender Z-up -> Unity
    # Y-up via axis_forward/axis_up (NOT bake_space_transform, matching
    # Cabin.fbx/Door.fbx's own export convention per the Cabin_v2 README),
    # no leaf bones (no armature here anyway), no baked animation (static
    # props).
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=False,
    )
    log(f"  Exported {fbx_path}")


def main():
    for blend_path, fbx_path in SOURCES:
        export_one(blend_path, fbx_path)
    log("DONE")


main()
