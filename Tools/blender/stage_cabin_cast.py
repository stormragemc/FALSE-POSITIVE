"""Stage the four Avaturn cabin-cast GLBs as Unity Humanoid-ready FBXs.

Unity 6000.5 has no built-in glTF importer in this project.  As documented in
``docs/UNITY_CLIENT.md``, Blender is the project's bridge from Avaturn GLB to
Unity's normal FBX/ModelImporter Humanoid workflow.

The source GLBs also contain Blender-scene leftovers (most notably an
Icosphere).  This script exports only meshes skinned to the avatar's single
armature, preserves facial shape keys, and unpacks the GLB's anonymous images
to stable per-character texture folders so Unity's FBX importer can resolve
them.

Run all four from the project root:

  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" ^
    --background --factory-startup --python Tools\\blender\\stage_cabin_cast.py

Pass character names after ``--`` to stage a subset, for example ``-- Ivy``.
"""

from __future__ import annotations

import sys
from pathlib import Path

import bpy


PROJECT_ROOT = Path(__file__).resolve().parents[2]
CHARACTER_ROOT = PROJECT_ROOT / "Assets" / "_Project" / "Art" / "Characters"
CHARACTERS = ("Aaron", "Ivy", "Nick", "Priya")
REQUIRED_HUMANOID_BONES = {
    "Hips",
    "Spine",
    "Spine1",
    "Spine2",
    "Neck",
    "Head",
    "LeftArm",
    "LeftForeArm",
    "LeftHand",
    "RightArm",
    "RightForeArm",
    "RightHand",
    "LeftUpLeg",
    "LeftLeg",
    "LeftFoot",
    "RightUpLeg",
    "RightLeg",
    "RightFoot",
}


def log(message: str) -> None:
    print(f"[stage_cabin_cast] {message}")


def requested_characters() -> tuple[str, ...]:
    if "--" not in sys.argv:
        return CHARACTERS

    requested = tuple(sys.argv[sys.argv.index("--") + 1 :])
    unknown = sorted(set(requested) - set(CHARACTERS))
    if unknown:
        raise ValueError(
            f"Unknown character(s): {', '.join(unknown)}. "
            f"Expected one or more of: {', '.join(CHARACTERS)}"
        )
    return requested or CHARACTERS


def has_armature_modifier(mesh: bpy.types.Object, armature: bpy.types.Object) -> bool:
    return any(
        modifier.type == "ARMATURE" and modifier.object == armature
        for modifier in mesh.modifiers
    )


def import_and_validate(character: str) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    source_path = CHARACTER_ROOT / f"{character}.glb"
    if not source_path.is_file():
        raise FileNotFoundError(source_path)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source_path))

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(
            f"{character}: expected one armature, found {len(armatures)}"
        )
    armature = armatures[0]

    bone_names = {bone.name for bone in armature.data.bones}
    missing_bones = sorted(REQUIRED_HUMANOID_BONES - bone_names)
    if missing_bones:
        raise RuntimeError(
            f"{character}: armature is missing Humanoid bones: "
            f"{', '.join(missing_bones)}"
        )

    skinned_meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and has_armature_modifier(obj, armature)
    ]
    if len(skinned_meshes) < 5:
        raise RuntimeError(
            f"{character}: expected at least five skinned avatar meshes, "
            f"found {len(skinned_meshes)}"
        )

    return armature, skinned_meshes


def remove_non_character_objects(
    character: str,
    armature: bpy.types.Object,
    skinned_meshes: list[bpy.types.Object],
) -> None:
    keep = {armature, *skinned_meshes}
    removed = []
    for obj in list(bpy.context.scene.objects):
        if obj not in keep:
            removed.append(f"{obj.name} ({obj.type})")
            bpy.data.objects.remove(obj, do_unlink=True)

    log(f"{character}: removed non-character objects: {', '.join(removed)}")


def extract_textures(character: str) -> int:
    texture_root = CHARACTER_ROOT / "Textures" / character
    texture_root.mkdir(parents=True, exist_ok=True)

    extracted = 0
    for image in bpy.data.images:
        if image.source != "FILE" or image.size[0] == 0 or image.size[1] == 0:
            continue

        extension = ".png" if image.file_format == "PNG" else ".jpg"
        safe_name = "".join(
            char if char.isalnum() or char in ("-", "_") else "_"
            for char in image.name
        )
        output_path = texture_root / f"{character}_{safe_name}{extension}"
        image.filepath_raw = str(output_path)
        image.save()
        image.filepath = str(output_path)
        extracted += 1

    if extracted == 0:
        raise RuntimeError(f"{character}: source GLB contained no extractable images")
    return extracted


def export_fbx(character: str) -> None:
    armature, skinned_meshes = import_and_validate(character)
    remove_non_character_objects(character, armature, skinned_meshes)
    texture_count = extract_textures(character)

    output_path = CHARACTER_ROOT / f"{character}.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=False,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        path_mode="RELATIVE",
        embed_textures=False,
    )

    shape_key_count = sum(
        max(0, len(mesh.data.shape_keys.key_blocks) - 1)
        if mesh.data.shape_keys is not None
        else 0
        for mesh in skinned_meshes
    )
    log(
        f"{character}: exported {output_path} with "
        f"{len(armature.data.bones)} bones, {len(skinned_meshes)} skinned meshes, "
        f"{shape_key_count} shape keys, and {texture_count} external textures"
    )


def main() -> None:
    for character in requested_characters():
        export_fbx(character)


if __name__ == "__main__":
    main()
