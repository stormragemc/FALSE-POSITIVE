# XR cast derivatives

These GLBs are optimized derivatives of the existing FALSE POSITIVE character
models under `Assets/_Project/Art/Characters/`. They retain the original
characters, triangle counts, skins, material assignments and base transforms.
Original asset and licensing terms continue to apply; this conversion does not
change their license or establish additional redistribution rights.

| Character | Original bytes | Texture/morph intermediate bytes | Intermediate gzip bytes |
|---|---:|---:|---:|
| Priya | 14,280,456 | 5,785,824 | 4,548,865 |
| Ivy | 14,562,588 | 5,770,540 | 4,599,549 |
| Aaron | 13,635,440 | 5,481,032 | 4,481,809 |
| Nick | 14,592,108 | 6,209,228 | 4,979,947 |

## Distribution

The final `.draco.glb` variants add WebP quality 90 followed by Draco compression
through official glTF Transform CLI 4.5.1. Their filenames retain `draco` for
clarity, but both `KHR_draco_mesh_compression` and `EXT_texture_webp` are required.

| Character | Final Draco + WebP bytes | Final gzip bytes |
|---|---:|---:|
| Priya | 1,940,516 | 1,471,323 |
| Ivy | 1,894,136 | 1,441,475 |
| Aaron | 1,713,096 | 1,250,662 |
| Nick | 1,976,692 | 1,498,381 |

All generated GLBs/gzip files here are local pipeline intermediates and
verification artifacts. The production build can regenerate its public model
files from the existing tracked original characters rather than committing the
derived binaries. Gzip uses compression level 9 and `mtime=0`. Every compressed
container was decompressed and compared byte for byte with its matching GLB.

The build must decompress each container to a `.glb` before glTF loading. A
`.glb.gz` filename alone does not make a normal glTF loader decompress it.

## Changes

- Embedded textures are capped at 512 pixels on the longest edge.
- Opaque color textures use JPEG quality 82; normal, metallic/roughness,
  occlusion and alpha-bearing textures use PNG.
- Only the facial targets required by the XR actor controller are retained:
  `mouthOpen`, `jawOpen`, `eyeBlinkLeft`, `eyeBlinkRight`, `eyesClosed`, and
  `viseme_aa`, where present on each original mesh.
- Unused morph-target accessors and buffer views are pruned. All references are
  remapped and binary data remain aligned to four-byte boundaries.
- In the texture/morph intermediate, no mesh decimation or geometry quantization
  is performed. Retained base
  vertex attributes, indices, skin weights and inverse-bind-matrix buffer bytes
  are independently compared with the original and remain bit-identical.
- The files do not add animations. The XR actor controller poses the original
  skeleton at runtime.

## Final compression and fidelity

The final Draco/WebP variants use quantization: position 16 bits, normal 12,
texture coordinates 14, and generic attributes 16, with decode speed 8 and
encode speed 5. The source-mesh ranges imply position grid steps below
0.028 millimetres. This is a grid bound, not a measured final surface-error
benchmark. No triangle decimation is requested or performed; triangle counts,
skin joint names/order, and selected morph names/counts are checked against
the intermediate. Vertex counts/layouts may change through Draco welding and
splitting. Quantized geometry and skin weights are not bit-identical to the
original float payloads.

All four inverse-bind-matrix payloads remain bit-identical (3,456 bytes per
54-joint skin). A separate raw-JSON comparison found rest-transform component
deltas below `5.2e-7` after the glTF Transform read/write, so those JSON values
are not described as bit-identical. The manifest records each model's maximum
component delta and inverse-bind SHA256.

WebP conversion must happen **before** Draco. The CLI reads/decompresses Draco
on input and removes that extension for ordinary transforms; applying `webp`
after `draco` would leave uncompressed geometry. A final-output check requires
both compression extensions.

`manifest.json` records source/output/gzip SHA256, byte sizes, selected morph
names, and verified retained geometry/skin accessors for each derivative.

## Reproduce

With Python and Pillow installed, run from the repository root:

```sh
python3 XR/scripts/optimize-cast.py
python3 - <<'PY'
from pathlib import Path
import gzip, hashlib, json
root = Path('Assets/_Project/Art/XR')
records = json.loads((root / 'manifest.json').read_text())
for record in records:
    path = root / (record['name'] + '.glb')
    data = gzip.compress(path.read_bytes(), compresslevel=9, mtime=0)
    target = path.with_suffix('.glb.gz')
    target.write_bytes(data)
    assert gzip.decompress(data) == path.read_bytes()
    record.update(gzip_output=str(target), gzip_bytes=len(data),
                  gzip_sha256=hashlib.sha256(data).hexdigest())
(root / 'manifest.json').write_text(json.dumps(records, indent=2) + '\n')
PY
```

The optimizer resolves paths relative to itself and rebuilds all four files.
Its checks fail if unsupported compressed geometry, external buffers, or a
morph-weight animation would make this rewrite ambiguous.

Apply the final official compression stages after producing the intermediate:

```sh
npx --yes @gltf-transform/cli@4.5.1 webp \
  Assets/_Project/Art/XR/Priya.glb /tmp/Priya.webp.glb \
  --quality 90 --effort 85
npx --yes @gltf-transform/cli@4.5.1 draco \
  /tmp/Priya.webp.glb Assets/_Project/Art/XR/Priya.draco.glb \
  --quantize-position 16 --quantize-normal 12 \
  --quantize-texcoord 14 --quantize-generic 16 \
  --decode-speed 8 --encode-speed 5
```

Repeat with each character's exact name. The production Node pipeline performs
the corresponding operations without requiring Pillow or Blender.

## Production Node build

`XR/scripts/prepare-cast.mjs` exports `prepareCast()` and also runs directly as
`node XR/scripts/prepare-cast.mjs`. It reads the original tracked character
models and writes lowercase files to `XR/public/models/`. The build dependency
versions verified locally are `@gltf-transform/core`, `extensions`, and
`functions` 4.5.1, `draco3dgltf` 1.5.7, and `sharp` 0.35.4.

All four real-source builds passed. This exact Node dependency set produced
1,935,644 bytes for Priya, 1,889,056 for Ivy, 1,713,256 for Aaron and 1,973,660
for Nick. These differ slightly from the CLI-generated local artifacts above.
The build independently decodes each result and checks unchanged triangle
counts, 54-joint order, inverse-bind-matrix SHA256 and 19 selected morph targets
across the meshes. Rest-transform component differences must remain below
`1e-6`; the largest observed difference was `5.194e-7`.
