#!/usr/bin/env python3
"""Verify binary Unity serialized assets (TerrainData, LightingData, etc.) are
not truncated or byte-shifted by git EOL normalization.

Unity's binary SerializedFile format embeds its own total file size in the
header. If a `.gitattributes` rule ever applies `eol=lf` / CRLF normalization
to one of these files, git silently rewrites CR/CRLF byte sequences and the
file's actual size stops matching the size baked into its own header — the
exact corruption that broke Assets/_Project/CabinNight/Data/CabinNightTerrain.asset
(see git commit 2edccea vs a63544b).

This script walks Assets/, skips plain-text YAML assets (files that start
with "%YAML"), parses the SerializedFile header for both the legacy layout
(format version < 22) and the current large-file layout (>= 22), and reports
any file whose declared size does not match its size on disk.

Usage:
    python Tools/verify-unity-binary-assets.py [root]

Exit code is non-zero if any mismatch (or unparsable serialized file) is found.
"""
import os
import struct
import sys

ASSET_EXTS = (
    ".asset", ".unity", ".prefab", ".terrainlayer", ".mat", ".controller", ".anim",
)


def check_file(path):
    """Return None if OK, else a short reason string."""
    size = os.path.getsize(path)
    if size < 64:
        return None  # too small to be a serialized file worth checking

    with open(path, "rb") as fh:
        head = fh.read(64)

    if head[:5] == b"%YAML":
        return None  # plain-text YAML asset, not a binary SerializedFile

    if len(head) < 32:
        return None

    try:
        version = struct.unpack_from(">I", head, 8)[0]
    except struct.error:
        return None

    # Unity SerializedFile format versions are small positive integers
    # (current LTS is in the low 20s). Anything wildly out of range means
    # this isn't actually a SerializedFile header we understand — skip it
    # rather than false-positive.
    if not (5 <= version <= 100):
        return None

    try:
        if version < 22:
            declared_size = struct.unpack_from(">I", head, 4)[0]
        else:
            declared_size = struct.unpack_from(">q", head, 24)[0]
    except struct.error:
        return "unparsable header"

    if declared_size != size:
        return f"header declares {declared_size} bytes, actual {size} (delta {size - declared_size})"

    return None


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "Assets"
    problems = []
    for dirpath, _dirs, files in os.walk(root):
        for name in files:
            if not name.endswith(ASSET_EXTS):
                continue
            path = os.path.join(dirpath, name)
            reason = check_file(path)
            if reason:
                problems.append((path, reason))

    if problems:
        print("Binary asset integrity check FAILED:")
        for path, reason in problems:
            print(f"  {path}: {reason}")
        return 1

    print("Binary asset integrity check passed: no size mismatches found.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
