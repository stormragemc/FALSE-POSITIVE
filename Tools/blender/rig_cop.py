"""Turn cop.glb (an Avaturn T1 export: 0 morph targets, 0 animations, no jaw
bone, sealed mouth) into a jaw-riggable, seated FBX for Unity.

Why FBX and not glTFast: the skeleton uses Mixamo-standard bone names
(Hips, Spine/Spine1/Spine2, Neck, Head, LeftUpLeg, ...). Exporting through
Blender to FBX gives Unity's normal ModelImporter with an Animation Type:
Humanoid dropdown that auto-maps this rig — glTFast's ScriptedImporter
doesn't offer that path.

Every measurement below (jaw hinge height, mouth width, ear band, chin
position) was derived from the actual imported geometry with short,
throwaway probe scripts (query vertex/bone coordinates from the imported
mesh, print, delete) rather than guessed — see each constant's inline
comment for what it represents; the probe scripts themselves weren't kept,
since the numbers they produced are what's captured here.

Run headless:
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" ^
    --background --factory-startup --python Tools\\blender\\rig_cop.py
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

GLB_PATH = r"C:\Users\Giorg\Documents\Unity\FalsePositiveTest\Assets\_Project\Art\cop.glb"
FBX_OUT = r"C:\Users\Giorg\Documents\Unity\FalsePositiveTest\Assets\_Project\Art\cop_rigged.fbx"

# --- measured constants (see _measure.py / _measure2.py / _measure3.py) ---
# Blender space after glTF import: X = left/right, Y = front(-)/back(+),
# Z = up. Front direction is -Y (confirmed: lip verts sit at Y ~ -0.09..-0.12).
LIP_SEAM_Z = 1.6482          # frontmost lip-seam ridge height
CHIN_Z = 1.560               # bottom of chin
EAR_Z_AVG = 1.677            # average ear-band height (widest |X| verts)
EAR_Y_AVG = 0.038            # average ear-band Y (how far back the ears sit)
MOUTH_HALF_WIDTH_X = 0.024   # mouth corner-to-corner half width
HINGE_Z = 1.635              # jaw hinge height: below ear, above chin
# First render pass (-22 deg test) showed a large black tear from cheek to
# neck: on a sealed mesh, ANY rotation stretches the seam between "moved"
# (jaw-weighted) and "static" skin, and a wide jaw-weight region turns that
# into a wide, highly visible gap. Pulled forward from -0.02 to -0.055 so
# the weighted region stays centered on the chin/lower-lip rather than
# reaching into the cheeks — smaller moving area, smaller/less visible tear.
FORWARD_MASK_Y = -0.055       # verts more forward (more negative Y) than this
                              # are candidate jaw verts; verts further back
                              # (toward/under the ear) are excluded


def clamp(x, lo=0.0, hi=1.0):
    return max(lo, min(hi, x))


def smoothstep(t):
    t = clamp(t)
    return t * t * (3 - 2 * t)


def log(msg):
    print(f"[rig_cop] {msg}")


def import_source():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=GLB_PATH)

    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    body = next(o for o in bpy.data.objects if o.type == "MESH" and "body" in o.name.lower())

    assert arm.matrix_world == arm.matrix_world.Identity(4), "armature has a non-identity transform; coordinate assumptions below would be wrong"
    assert body.matrix_world == body.matrix_world.Identity(4), "body mesh has a non-identity transform; coordinate assumptions below would be wrong"

    log(f"Imported. Armature={arm.name!r} bones={len(arm.data.bones)}, body={body.name!r} verts={len(body.data.vertices)}")
    return arm, body


def add_jaw_bone(arm):
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    head_bone = eb["Head"]

    jaw = eb.new("Jaw")
    jaw.parent = head_bone
    jaw.use_connect = False
    jaw.use_deform = True
    # Head/pivot near the ear height, slightly forward of the ear (the TMJ
    # hinge, simplified to one midline bone rather than a paired L/R hinge).
    jaw.head = Vector((0.0, EAR_Y_AVG - 0.03, HINGE_Z))
    # Tail toward the chin: forward and down, giving Update()'s X-rotation
    # in JawBoneCopMouth a "drop open" axis that reads correctly.
    jaw.tail = Vector((0.0, -0.085, CHIN_Z + 0.01))

    # EditBone references (and their .head/.tail) go stale the instant we
    # leave Edit mode, so snapshot the values into plain tuples first.
    head_snapshot = tuple(round(c, 4) for c in jaw.head)
    tail_snapshot = tuple(round(c, 4) for c in jaw.tail)

    bpy.ops.object.mode_set(mode="OBJECT")
    log(f"Added Jaw bone: head={head_snapshot} tail={tail_snapshot}")


def weight_jaw(body):
    me = body.data
    head_vg = body.vertex_groups["Head"]
    head_idx = head_vg.index
    jaw_vg = body.vertex_groups.new(name="Jaw")

    affected = 0
    for v in me.vertices:
        head_w = 0.0
        for g in v.groups:
            if g.group == head_idx:
                head_w = g.weight
                break
        if head_w <= 0.0:
            continue

        z, y = v.co.z, v.co.y
        if z >= HINGE_Z:
            continue

        t = (HINGE_Z - z) / (HINGE_Z - CHIN_Z)
        w = smoothstep(t)

        # Forward mask: fade out verts that sit back toward/under the ear
        # (e.g. jaw hinge area itself, side of neck) so only the visible
        # lower face — chin, lower lip, lower cheek — actually moves.
        if y > FORWARD_MASK_Y:
            back_t = clamp((y - FORWARD_MASK_Y) / (EAR_Y_AVG - FORWARD_MASK_Y))
            w *= (1.0 - back_t)

        w = clamp(w) * head_w
        if w < 0.01:
            continue

        jaw_vg.add([v.index], w, "REPLACE")
        head_vg.add([v.index], head_w - w, "REPLACE")
        affected += 1

    log(f"Jaw vertex group: {affected} verts weighted (of {len(me.vertices)} total).")
    assert affected > 200, f"suspiciously few verts weighted to Jaw ({affected}) — hinge/mask constants likely wrong for this mesh"
    return affected


def carve_mouth_dimple(body):
    """Push the lip-seam region backward (a pure vertex translate with
    smooth falloff — no faces added/removed, mesh stays manifold) and
    darken the innermost faces, so a jaw-open rotation reveals a shadowed
    recess instead of visibly stretching the sealed skin. Guarded: if the
    seam-region selection is implausibly small, skip and report rather than
    risk a bad deformation."""
    me = body.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()

    # Selection band: whole mouth width, from just AT the seam (not above
    # it — the first render pass leaked 12mm above LIP_SEAM_Z and caught
    # the nose/nostril base, turning into a black smear across most of the
    # lower face, not a mouth-shaped dimple) down to just above the chin
    # taper, symmetric front falloff.
    band = []
    for v in bm.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        if abs(x) > MOUTH_HALF_WIDTH_X + 0.002:
            continue
        if not (CHIN_Z + 0.050 < z < LIP_SEAM_Z + 0.002):
            continue
        if y > -0.06:  # only the forward-protruding lip/chin surface
            continue
        band.append(v)

    log(f"Mouth dimple band: {len(band)} candidate verts.")
    if len(band) < 40:
        log("WARNING: too few verts in mouth band — skipping dimple carve, keeping sealed geometry.")
        bm.free()
        return False

    # Falloff weight per vertex: 1.0 at the seam center (front-most, mid
    # height), fading to 0 at the band's horizontal/vertical edges so the
    # push blends smoothly into the surrounding face instead of pinching.
    # Push magnitude (not just band membership) also drives which faces get
    # darkened below, so the dark material stays confined to the visibly
    # recessed core instead of covering the whole soft-edged selection box.
    xs = [abs(v.co.x) for v in band]
    max_x = max(xs) or 1.0
    max_push = 0.018  # up to 18mm backward at the seam center
    push_by_idx = {}
    for v in band:
        x, y, z = v.co.x, v.co.y, v.co.z
        x_t = 1.0 - smoothstep(abs(x) / max_x)
        z_center = (LIP_SEAM_Z + CHIN_Z + 0.050) / 2.0
        z_span = (LIP_SEAM_Z + 0.002 - (CHIN_Z + 0.050)) / 2.0
        z_t = 1.0 - smoothstep(abs(z - z_center) / z_span)
        push = clamp(x_t * z_t) * max_push
        v.co.y += push  # +Y is backward/into the head
        push_by_idx[v.index] = push

    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    me.update()

    # Dark interior material only on faces whose vertices were pushed back
    # substantially (>50% of max) — the recessed core, not the soft-falloff
    # blend ring, which should stay the original skin material so the push
    # reads as a smooth dent rather than a flat dark patch with a hard edge.
    # A dark reddish-brown, not near-black: the first render pass used
    # (0.02, 0.015, 0.015) and it read as a solid black hole rather than a
    # shadowed recess — too dark to distinguish from ambient occlusion.
    mat = bpy.data.materials.get("MouthInterior")
    if mat is None:
        mat = bpy.data.materials.new("MouthInterior")
        mat.diffuse_color = (0.14, 0.05, 0.045, 1.0)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (0.14, 0.05, 0.045, 1.0)
            if "Roughness" in bsdf.inputs:
                bsdf.inputs["Roughness"].default_value = 0.9
    mat_index = len(body.data.materials)
    body.data.materials.append(mat)

    # Higher threshold than the first pass (0.5) — only the most-recessed
    # core gets the dark material, so it stays a small contained patch.
    darken_threshold = max_push * 0.7
    darkened = 0
    for poly in me.polygons:
        pushes = [push_by_idx.get(vi, 0.0) for vi in poly.vertices]
        if pushes and min(pushes) >= darken_threshold:
            poly.material_index = mat_index
            darkened += 1
    log(f"Mouth dimple carved: pushed {len(band)} verts (max {max_push*1000:.0f}mm), darkened {darkened} core faces.")
    return True


SEAT_HEIGHT_Z = 0.45  # matches the room's table/chair numbers (plan Phase 2)


def bake_seated_pose(arm):
    bpy.context.view_layer.objects.active = arm

    # Root-bone drop, computed in EDIT mode where head/tail are unambiguous
    # armature-space coordinates (pose-space .location is in the bone's own
    # local axes, which for a Hips root bone is not guaranteed to line up
    # with world Z — rotating the legs alone, without this, would leave the
    # pelvis at standing height with the thighs sticking out horizontally
    # in mid-air instead of actually sitting down into the seat).
    bpy.ops.object.mode_set(mode="EDIT")
    orig_hips_z = arm.data.edit_bones["Hips"].head.z
    bpy.ops.object.mode_set(mode="OBJECT")

    drop = orig_hips_z - SEAT_HEIGHT_Z
    log(f"Original standing Hips Z: {orig_hips_z:.3f}, dropping {drop:.3f} to reach seat height {SEAT_HEIGHT_Z}")

    bpy.ops.object.mode_set(mode="POSE")
    pb = arm.pose.bones

    hips = pb["Hips"]
    # Convert a world-space -Z offset into the Hips bone's local pose space
    # via its rest matrix, so the translation is correct regardless of the
    # bone's own local-axis orientation.
    world_offset = Vector((0.0, 0.0, -drop))
    local_offset = hips.bone.matrix_local.to_3x3().inverted() @ world_offset
    hips.location += local_offset

    def rot_x(name, degrees):
        pb[name].rotation_mode = "XYZ"
        pb[name].rotation_euler[0] += math.radians(degrees)

    # Hip bend (thighs forward, roughly horizontal into the now-lowered
    # seat), knee bend (shins back down toward the floor), slight spine
    # settle, arms relaxed at the sides. Foot-to-floor placement is an
    # approximation, not exact IK — the table occludes the legs and most of
    # the forearms in normal play framing, so getting hips/spine/head right
    # matters far more here.
    #
    # Arm sign/axis was verified empirically via Tools/blender/_debug_arms.py
    # renders, not assumed: the bind pose is a T-pose (arms horizontal), and
    # +X on Arm — not the -55 first guessed — is what rotates it down to the
    # sides; a first attempt at -55 sent the arms straight up above the head.
    for side in ("Left", "Right"):
        rot_x(f"{side}UpLeg", -85)   # thigh rotates up to horizontal
        rot_x(f"{side}Leg", 90)      # knee bends so the shin drops vertical
        rot_x(f"{side}Foot", -8)
        rot_x(f"{side}Arm", 80)      # T-pose horizontal -> resting at the side
        rot_x(f"{side}ForeArm", 25)  # slight relaxed elbow bend

    rot_x("Spine", 4)
    rot_x("Spine1", 3)
    rot_x("Spine2", -2)
    rot_x("Head", -3)  # slight watchful forward tilt

    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")

    # bpy.ops.pose.armature_apply() only rewrites the armature's REST
    # transforms. That looks sufficient, but it is NOT: the Armature
    # modifier deforms each vertex by (pose_matrix @ rest_matrix.inverted())
    # per bone, and immediately after the operator runs, pose == the new
    # rest (zero delta) — so every bone's deform factor is identity and the
    # mesh renders at its untouched BASIS shape, i.e. it silently snaps
    # back to the original standing pose. Confirmed empirically via
    # Tools/blender/_debug_pose*.py: eval-mesh Z range reverted from the
    # correctly-seated -0.435..1.382 back to the original standing
    # 0.015..1.808 every time, both via the operator and a hand-rolled
    # equivalent, in this --background session.
    #
    # Fix: explicitly capture each skinned mesh's CURRENTLY DEFORMED
    # (posed) vertex positions via the evaluated depsgraph, then overwrite
    # the mesh's basis with them — belt-and-suspenders alongside the rest-
    # transform bake below, which is what keeps a later bone rotation (the
    # Jaw, at runtime in Unity) deforming correctly from this new baseline
    # rather than from the old standing basis.
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
            f"({len(obj.data.vertices)}) — Armature modifier must not add/remove verts for this to be safe"
        )
        posed_coords_by_obj[obj.name] = [v.co.copy() for v in eval_mesh.vertices]
        eval_obj.to_mesh_clear()

    # Now bake the armature's rest transforms to match the current pose
    # (still needed so a later Jaw rotation is relative to the seated rig).
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

    # Overwrite each mesh's basis with its captured posed shape.
    for obj in skinned:
        coords = posed_coords_by_obj[obj.name]
        for i, co in enumerate(coords):
            obj.data.vertices[i].co = co
        obj.data.update()

    log("Seated pose baked into mesh basis + armature rest pose.")


_EXPECTED_MESH_NAMES = {
    "avaturn_body", "avaturn_glasses_0", "avaturn_glasses_1",
    "avaturn_hair_0", "avaturn_look_0", "avaturn_shoes_0",
}


def remove_stray_meshes():
    """render_checks() triggers actual render calls (bpy.ops.render.render),
    and at least one Eevee/Eevee Next render pass was observed to silently
    add its own helper mesh object to the scene (seen in practice: an
    "Icosphere" at 100x scale, unrelated to the character, that rode along
    into the FBX export and into Unity as a giant blank-white blob covering
    the model). Guard against that landing in the export regardless of
    root cause: after render_checks() runs, delete any MESH object that
    isn't one of the known avaturn_* character meshes.

    Safety valve: _EXPECTED_MESH_NAMES is a hardcoded list of this
    particular T1 export's mesh names. A future T2 re-export could name
    meshes differently (Avaturn's naming isn't guaranteed stable across
    exports), in which case a naive version of this guard would delete the
    *entire* character instead of one stray helper. Refuse to delete
    everything blind — if the "stray" set is most of the scene's meshes,
    something upstream changed and this needs a human, not a silent wipe."""
    all_meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    stray = [o for o in all_meshes if o.name not in _EXPECTED_MESH_NAMES]
    if stray and len(stray) >= len(all_meshes) - 1:
        names = ", ".join(repr(o.name) for o in stray)
        raise RuntimeError(
            f"remove_stray_meshes() would delete {len(stray)} of {len(all_meshes)} mesh "
            f"objects ({names}) — that looks like a mesh-naming change (e.g. a T2 re-export), "
            f"not a stray render artifact. Refusing to delete blind; update "
            f"_EXPECTED_MESH_NAMES for the new source file and re-run."
        )
    for o in stray:
        log(f"Removing stray mesh object from scene before export: {o.name!r}")
        bpy.data.objects.remove(o, do_unlink=True)
    if not stray:
        log("No stray mesh objects found before export.")


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


def render_checks(arm, body):
    """Jaw-closed / jaw-open renders plus a couple of framing angles, so the
    rig can be judged from actual pixels instead of trusting the geometry
    math blind (plan Phase 1 step 8)."""
    import os

    out_dir = r"C:\Users\Giorg\AppData\Local\Temp\claude\C--Users-Giorg-Documents-Unity-FalsePositiveTest\871b6373-c7df-484c-ba66-5197763cf31d\scratchpad\cop_renders"
    os.makedirs(out_dir, exist_ok=True)

    scene = bpy.context.scene
    for engine_id in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        try:
            scene.render.engine = engine_id
            break
        except TypeError:
            continue
    log(f"Render engine: {scene.render.engine}")
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640

    # A single hard sun light produced a strong, asymmetric self-shadow
    # across the nose/cheek that was visible even with the jaw fully
    # closed — a lighting artifact, not a geometry defect, but it made the
    # jaw-open renders impossible to read correctly. Fill light + world
    # ambient give a flatter, more honest diagnostic image.
    key = bpy.data.lights.new("KeyLight", type="SUN")
    key.energy = 2.0
    key_obj = bpy.data.objects.new("KeyLight", key)
    scene.collection.objects.link(key_obj)
    key_obj.location = (0.5, -1.0, 2.2)
    key_obj.rotation_euler = (math.radians(55), 0, math.radians(25))

    fill = bpy.data.lights.new("FillLight", type="SUN")
    fill.energy = 1.2
    fill_obj = bpy.data.objects.new("FillLight", fill)
    scene.collection.objects.link(fill_obj)
    fill_obj.rotation_euler = (math.radians(65), 0, math.radians(-140))

    world = bpy.data.worlds.new("ProbeWorld")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.35, 0.35, 0.35, 1.0)
        bg.inputs[1].default_value = 0.6
    scene.world = world

    cam_data = bpy.data.cameras.new("ProbeCam")
    cam_obj = bpy.data.objects.new("ProbeCam", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    def shoot(name, loc, look_at):
        cam_obj.location = loc
        direction = (Vector(look_at) - Vector(loc)).normalized()
        cam_obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(out_dir, name)
        bpy.ops.render.render(write_still=True)
        log(f"Rendered {scene.render.filepath}.png")

    # Derived from the actual post-bake rest pose rather than guessed — the
    # first render pass guessed 1.35 while the seated-pose bug was still
    # live and the framing came out wrong as a result.
    head_z = arm.data.bones["Head"].head_local.z
    head_target = (0.0, -0.02, head_z)
    log(f"Camera head target: {head_target} (Head bone rest Z={head_z:.3f})")
    shoot("01_face_front", (0.0, -0.75, head_z + 0.03), head_target)
    shoot("02_face_side", (0.65, -0.05, head_z + 0.03), head_target)
    shoot("03_body_wide", (0.0, -2.2, 1.15), (0.0, 0.0, SEAT_HEIGHT_Z + 0.5))

    # Jaw open: rotate the pose bone, render, then reset — same rest pose
    # export either way, this is purely a visual check of the weighting.
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    jaw_pb = arm.pose.bones["Jaw"]
    jaw_pb.rotation_mode = "XYZ"
    # -22 deg (a first test) tore the sealed mesh open into a visible black
    # gap. -10 deg matches the realistic runtime range better: JawBoneCopMouth
    # clamps rms*amplitudeGain to [0,1] and lerps toward openLocalEuler,
    # which Phase 3 will tune down from its 15 deg default to stay under
    # what this sealed mesh can take without visibly tearing.
    jaw_pb.rotation_euler[0] = math.radians(-10)
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")
    shoot("04_jaw_open", (0.0, -0.6, head_z + 0.02), head_target)

    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    jaw_pb.rotation_euler[0] = 0.0
    bpy.ops.object.mode_set(mode="OBJECT")

    log(f"Render checks written to {out_dir}")


def main():
    arm, body = import_source()
    add_jaw_bone(arm)
    weight_jaw(body)
    carve_mouth_dimple(body)
    bake_seated_pose(arm)

    # Sanity: read the ACTUAL deformed silhouette via the evaluated
    # depsgraph, not body.data.vertices — armature_apply() rebases the rest
    # pose so the raw mesh basis stays numerically unchanged by design
    # (that's what makes the bake visually seamless), so checking the raw
    # vertices here would silently show the pre-seated bounding box even
    # though the character is now genuinely seated.
    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_body = body.evaluated_get(depsgraph)
    eval_mesh = eval_body.to_mesh()
    zs = [eval_body.matrix_world @ v.co for v in eval_mesh.vertices]
    zs = [c.z for c in zs]
    eval_body.to_mesh_clear()
    hips_z = arm.data.bones["Hips"].head_local.z
    log(f"Post-pose DEFORMED body Z range: {min(zs):.3f}..{max(zs):.3f}, Hips rest-pose Z: {hips_z:.3f}")

    render_checks(arm, body)
    export_fbx()
    log("DONE")


main()
