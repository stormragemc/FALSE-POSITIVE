"""Turn NewCop.glb (an Avaturn T2 export: 73 morph targets on Head_Mesh
including a full Oculus-viseme set + jawOpen, 54-joint Mixamo-style
skeleton, 0 animations) into a seated Humanoid FBX for Unity.

Unlike Tools/blender/rig_cop.py (the old Avaturn T1 pass), this model needs
NO jaw-bone surgery and NO mouth-dimple carve: it already ships real morph
targets, so the mouth is driven by blendshapes (uLipSync) at runtime, not a
bone hinge. This script only does the two things a T2 export still needs
before Unity can use it as a Humanoid: (1) Blender->FBX at all, since Unity
6000.5 has no glTF importer and glTFast's importer has no Humanoid option;
(2) bake a seated rest pose, since the source export is a standing/T-pose
character and the interrogation scene has him sitting in a chair.

Run headless:
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" ^
    --background --factory-startup --python Tools\\blender\\rig_newcop.py

(This exact script was actually run interactively via the Blender MCP addon,
not headless — see the session transcript. It's saved here in headless-
runnable form for reproducibility; a `bpy.ops.wm.read_factory_settings`
call at the top of `main()` crashed the MCP connection once when combined
with the glTF importer's context assumptions, so the live session cleared
`bpy.data.objects` manually instead. Both should be equivalent when run via
the real `--background` CLI, where context is fully re-initialized between
calls; only the interactive MCP path showed the crash.)
"""
import math

import bpy
from mathutils import Matrix, Vector

GLB_PATH = r"C:\Users\Giorg\Documents\Unity\FALSE-POSITIVE\Assets\_Project\Art\NewCop.glb"
FBX_OUT = r"C:\Users\Giorg\Documents\Unity\FALSE-POSITIVE\Assets\_Project\Art\NewCop_rigged.fbx"

# Seat height target — same value as the old rig_cop.py, which was tuned
# against this same room's CopChairPlaceholder (Interrogation.unity, local
# Y=0.225 under the set root) and confirmed here to produce a correct
# seated L-shape profile (side-view render) for this model's proportions
# too (this model's own standing Hips rest Z was 0.984, close enough to
# the old model's that the same target still reads as a natural sit).
SEAT_HEIGHT_Z = 0.45

# This T2 export's own mesh names — different from the old T1 export's
# _EXPECTED_MESH_NAMES (avaturn_body etc.). Used only as a stray-object
# guard before FBX export (see remove_stray_meshes' docstring in
# rig_cop.py for why this check exists at all: an Eevee render pass was
# once observed to silently inject a helper mesh into the scene).
_EXPECTED_MESH_NAMES = {
    "Body_Mesh", "Eye_Mesh", "EyeAO_Mesh", "Eyelash_Mesh", "Head_Mesh",
    "Teeth_Mesh", "Tongue_Mesh", "avaturn_glasses_0", "avaturn_glasses_1",
    "avaturn_hair_0", "avaturn_shoes_0", "avaturn_look_0",
}


def log(msg):
    print(f"[rig_newcop] {msg}")


def import_source():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.import_scene.gltf(filepath=GLB_PATH)

    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    assert arm.matrix_world == arm.matrix_world.Identity(4), \
        "armature has a non-identity transform; coordinate assumptions below would be wrong"

    remove_stray_meshes()
    log(f"Imported. Armature={arm.name!r} bones={len(arm.data.bones)}")
    return arm


def remove_stray_meshes():
    all_meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    stray = [o for o in all_meshes if o.name not in _EXPECTED_MESH_NAMES]
    if stray and len(stray) >= len(all_meshes) - 1:
        names = ", ".join(repr(o.name) for o in stray)
        raise RuntimeError(
            f"remove_stray_meshes() would delete {len(stray)} of {len(all_meshes)} mesh "
            f"objects ({names}) — that looks like a mesh-naming change, not a stray "
            "artifact. Refusing to delete blind; update _EXPECTED_MESH_NAMES and re-run."
        )
    for o in stray:
        log(f"Removing stray mesh object from scene: {o.name!r}")
        bpy.data.objects.remove(o, do_unlink=True)
    if not stray:
        log("No stray mesh objects found.")


def bake_seated_pose(arm):
    """Bends the skeleton into a seated pose (same bend angles as
    rig_cop.py's proven pass — verified here via a side-view render showing
    the correct L-shape profile before committing to the bake) and bakes
    it into both the mesh basis and the armature rest pose. See
    rig_cop.py's own bake_seated_pose docstring for why armature_apply()
    alone is NOT sufficient and the depsgraph-capture dance below is
    needed.

    CAVEAT not present in the old T1 pass, and PREVIOUSLY MISHANDLED here
    (found via a real bug: the cop's head detached and teleported ~0.5m
    whenever a viseme blendshape activated): several meshes here (Head_Mesh,
    Eye_Mesh, EyeAO_Mesh, Eyelash_Mesh, Teeth_Mesh, Tongue_Mesh) carry real
    shape keys (73 on Head_Mesh alone). Shape key targets are stored as
    absolute positions in the mesh's own local/bind space, independent of
    the current bone pose. The first version of this function wrote only
    obj.data.vertices (the Basis) to the posed shape and left every other
    shape key block untouched, in the original STANDING frame. Since a
    glTF-exported blendshape's delta is (key block - Basis), and Basis had
    just jumped by the full seat-height drop (~0.53m) while the key blocks
    hadn't, every single exported blendshape came out as a ~0.53m rigid
    translation of the whole head — not a small skew, a broken rig. This
    was confirmed empirically: every one of Head_Mesh's 72 blendshapes had
    an identical ~0.0054 mesh-space delta across all 4303 vertices (see
    ASSETS_TODO.md for the diagnostic).

    Because the corruption is a pure translation (the same displacement
    vector for the whole mesh, not a per-vertex-varying rotation skew), the
    fix below is exact: offset every shape key block, including Basis, by
    the identical per-vertex displacement the write-back is about to apply.
    `target - basis` is then unchanged from what the standing-frame rig had,
    for every shape key, regardless of the small head/neck/spine rotation
    (Spine 4°/3°/-2° + Head -3°) baked in alongside the translation.
    """
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    orig_hips_z = arm.data.edit_bones["Hips"].head.z
    bpy.ops.object.mode_set(mode="OBJECT")

    drop = orig_hips_z - SEAT_HEIGHT_Z
    log(f"Original standing Hips Z: {orig_hips_z:.3f}, dropping {drop:.3f} to reach seat height {SEAT_HEIGHT_Z}")

    bpy.ops.object.mode_set(mode="POSE")
    pb = arm.pose.bones

    hips = pb["Hips"]
    world_offset = Vector((0.0, 0.0, -drop))
    local_offset = hips.bone.matrix_local.to_3x3().inverted() @ world_offset
    hips.location += local_offset

    def rot_x(name, degrees):
        pb[name].rotation_mode = "XYZ"
        pb[name].rotation_euler[0] += math.radians(degrees)

    # Same bend angles as rig_cop.py's bake_seated_pose — reused verbatim
    # and confirmed correct for this model via a side-view render (the
    # classic seated L-shape silhouette) before this function ran.
    for side in ("Left", "Right"):
        rot_x(f"{side}UpLeg", -85)
        rot_x(f"{side}Leg", 90)
        rot_x(f"{side}Foot", -8)
        rot_x(f"{side}Arm", 80)
        rot_x(f"{side}ForeArm", 25)

    rot_x("Spine", 4)
    rot_x("Spine1", 3)
    rot_x("Spine2", -2)
    rot_x("Head", -3)

    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")

    skinned = [o for o in bpy.data.objects
               if o.type == "MESH" and any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers)]
    log(f"Baking posed shape into {len(skinned)} skinned mesh(es): {[o.name for o in skinned]}")

    depsgraph = bpy.context.evaluated_depsgraph_get()
    posed_coords_by_obj = {}
    for obj in skinned:
        eval_obj = obj.evaluated_get(depsgraph)
        eval_mesh = eval_obj.to_mesh()
        assert len(eval_mesh.vertices) == len(obj.data.vertices), (
            f"{obj.name}: evaluated vertex count ({len(eval_mesh.vertices)}) != basis count "
            f"({len(obj.data.vertices)})"
        )
        posed_coords_by_obj[obj.name] = [v.co.copy() for v in eval_mesh.vertices]
        eval_obj.to_mesh_clear()

    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.context.view_layer.update()
    pose_matrices = {b.name: b.matrix.copy() for b in arm.pose.bones}
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        eb.matrix = pose_matrices[eb.name]
    bpy.ops.object.mode_set(mode="POSE")
    for b in arm.pose.bones:
        b.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")

    for obj in skinned:
        coords = posed_coords_by_obj[obj.name]

        # Offset every shape key block (including Basis) by the same
        # per-vertex displacement about to be applied to obj.data.vertices,
        # captured from the PRE-write Basis — see this function's docstring.
        # Capturing `basis` after writing obj.data.vertices below would make
        # every offset zero, a silent no-op indistinguishable from success.
        shape_keys = obj.data.shape_keys
        if shape_keys is not None:
            basis = [p.co.copy() for p in shape_keys.key_blocks[0].data]
            for kb in shape_keys.key_blocks:
                for i in range(len(kb.data)):
                    kb.data[i].co = kb.data[i].co + (coords[i] - basis[i])
            log(f"  {obj.name}: offset {len(shape_keys.key_blocks)} shape key block(s) "
                f"({len(kb.data)} verts each) to match the seated bake.")

        for i, co in enumerate(coords):
            obj.data.vertices[i].co = co
        obj.data.update()

    log("Seated pose baked into mesh basis + armature rest pose.")
    log(f"New rest-pose Hips Z: {arm.data.bones['Hips'].head_local.z:.3f}")


def export_fbx():
    remove_stray_meshes()
    bpy.ops.export_scene.fbx(
        filepath=FBX_OUT,
        use_selection=False,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
    )
    log(f"Exported {FBX_OUT}")


def main():
    arm = import_source()
    bake_seated_pose(arm)
    export_fbx()
    log("DONE")


main()
