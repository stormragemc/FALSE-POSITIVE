#!/usr/bin/env python3
"""Create XR cast GLBs without changing retained mesh or skin buffer bytes.

Requires Pillow. Run from any directory; paths resolve relative to this script.
Unused facial targets are removed. No base geometry decimation/quantization.
"""
from __future__ import annotations

import copy
import hashlib
import io
import json
from pathlib import Path
import struct

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/_Project/Art/Characters'
DEST = ROOT / 'Assets/_Project/Art/XR'
KEEP_MORPHS = {'mouthOpen', 'jawOpen', 'eyeBlinkLeft', 'eyeBlinkRight', 'eyesClosed', 'viseme_aa'}


def load_glb(path):
    payload = path.read_bytes()
    magic, version, size = struct.unpack_from('<III', payload)
    assert magic == 0x46546C67 and version == 2 and size == len(payload)
    offset = 12
    chunks = {}
    while offset < len(payload):
        length, kind = struct.unpack_from('<II', payload, offset)
        chunks[kind] = payload[offset + 8:offset + 8 + length]
        offset += 8 + length
    return json.loads(chunks[0x4E4F534A]), chunks[0x004E4942], payload


def used_accessors(doc):
    values = set()
    for mesh in doc['meshes']:
        for primitive in mesh['primitives']:
            values.update(primitive['attributes'].values())
            if 'indices' in primitive:
                values.add(primitive['indices'])
            for target in primitive.get('targets', []):
                values.update(target.values())
    for skin in doc.get('skins', []):
        if 'inverseBindMatrices' in skin:
            values.add(skin['inverseBindMatrices'])
    for animation in doc.get('animations', []):
        assert all(channel['target']['path'] != 'weights' for channel in animation['channels'])
        for sampler in animation['samplers']:
            values.update((sampler['input'], sampler['output']))
    return values


def remap_accessors(doc, mapping):
    for mesh in doc['meshes']:
        for primitive in mesh['primitives']:
            primitive['attributes'] = {key: mapping[value] for key, value in primitive['attributes'].items()}
            if 'indices' in primitive:
                primitive['indices'] = mapping[primitive['indices']]
            for target in primitive.get('targets', []):
                for key, value in target.items():
                    target[key] = mapping[value]
    for skin in doc.get('skins', []):
        if 'inverseBindMatrices' in skin:
            skin['inverseBindMatrices'] = mapping[skin['inverseBindMatrices']]
    for animation in doc.get('animations', []):
        for sampler in animation['samplers']:
            sampler['input'] = mapping[sampler['input']]
            sampler['output'] = mapping[sampler['output']]


def accessor_views(accessor):
    if 'bufferView' in accessor:
        yield accessor, 'bufferView'
    if 'sparse' in accessor:
        yield accessor['sparse']['indices'], 'bufferView'
        yield accessor['sparse']['values'], 'bufferView'


def optimize(path):
    source, binary, source_bytes = load_glb(path)
    doc = copy.deepcopy(source)
    assert len(doc['buffers']) == 1
    assert not doc['buffers'][0].get('uri')
    assert not any('KHR_draco_mesh_compression' in p.get('extensions', {})
                   for m in doc['meshes'] for p in m['primitives'])
    before_targets = 0
    after_targets = 0
    for mesh in doc['meshes']:
        names = mesh.get('extras', {}).get('targetNames', [])
        if not names:
            assert not any(p.get('targets') for p in mesh['primitives'])
            continue
        keep = [index for index, name in enumerate(names) if name in KEEP_MORPHS]
        before_targets += len(names)
        after_targets += len(keep)
        mesh['extras']['targetNames'] = [names[index] for index in keep]
        if 'weights' in mesh:
            mesh['weights'] = [mesh['weights'][index] for index in keep]
        for primitive in mesh['primitives']:
            primitive['targets'] = [primitive['targets'][index] for index in keep]
            if not keep:
                primitive.pop('targets')
    live_accessors = sorted(used_accessors(doc))
    accessor_map = {old: new for new, old in enumerate(live_accessors)}
    doc['accessors'] = [doc['accessors'][index] for index in live_accessors]
    remap_accessors(doc, accessor_map)

    live_views = set()
    for accessor in doc['accessors']:
        live_views.update(container[key] for container, key in accessor_views(accessor))
    image_views = {image['bufferView'] for image in doc.get('images', [])}
    live_views.update(image_views)
    assert not image_views.intersection({container[key] for a in doc['accessors'] for container, key in accessor_views(a)})

    data_images = set()
    for material in doc.get('materials', []):
        textures = [material.get('normalTexture'), material.get('occlusionTexture'),
                    material.get('pbrMetallicRoughness', {}).get('metallicRoughnessTexture')]
        for texture in textures:
            if texture:
                data_images.add(doc['textures'][texture['index']]['source'])

    replacements = {}
    for index, image in enumerate(doc.get('images', [])):
        view_id = image['bufferView']
        view = source['bufferViews'][view_id]
        start = view.get('byteOffset', 0)
        payload = binary[start:start + view['byteLength']]
        picture = Image.open(io.BytesIO(payload))
        picture.load()
        picture.thumbnail((512, 512), Image.Resampling.LANCZOS)
        alpha = picture.getchannel('A').getextrema() if 'A' in picture.getbands() else None
        transparent = alpha is not None and alpha[0] < 255
        if index in data_images or transparent:
            picture = picture.convert('RGBA' if transparent else 'RGB')
            mime = 'image/png'
            options = {'format': 'PNG', 'optimize': True, 'compress_level': 9}
        else:
            picture = picture.convert('RGB')
            mime = 'image/jpeg'
            options = {'format': 'JPEG', 'quality': 82, 'optimize': True, 'subsampling': 0}
        encoded = io.BytesIO()
        picture.save(encoded, **options)
        if view_id in replacements:
            assert replacements[view_id] == encoded.getvalue()
        replacements[view_id] = encoded.getvalue()
        image['mimeType'] = mime

    view_map = {}
    rebuilt = bytearray()
    views = []
    preserved_hash = hashlib.sha256()
    retained_core_views = 0
    for old_id in sorted(live_views):
        view = copy.deepcopy(source['bufferViews'][old_id])
        old_offset = view.get('byteOffset', 0)
        old_data = binary[old_offset:old_offset + view['byteLength']]
        data = replacements.get(old_id, old_data)
        while len(rebuilt) % 4:
            rebuilt.append(0)
        view['byteOffset'] = len(rebuilt)
        view['byteLength'] = len(data)
        view['buffer'] = 0
        if old_id not in image_views:
            assert data == old_data
            preserved_hash.update(old_id.to_bytes(4, 'little'))
            preserved_hash.update(data)
            retained_core_views += 1
        rebuilt.extend(data)
        view_map[old_id] = len(views)
        views.append(view)
    for accessor in doc['accessors']:
        for container, key in accessor_views(accessor):
            container[key] = view_map[container[key]]
    for image in doc.get('images', []):
        image['bufferView'] = view_map[image['bufferView']]
    doc['bufferViews'] = views
    doc['buffers'][0]['byteLength'] = len(rebuilt)

    # Independently verify all base vertex/index/skin accessor payloads, not
    # only the set of bytes copied during the rewrite.
    def payload_for(document, data, accessor_id):
        accessor = document['accessors'][accessor_id]
        out = []
        for container, key in accessor_views(accessor):
            view = document['bufferViews'][container[key]]
            start = view.get('byteOffset', 0)
            out.append(bytes(data[start:start + view['byteLength']]))
        return out
    core_accessors = set()
    for mesh in source['meshes']:
        for primitive in mesh['primitives']:
            core_accessors.update(primitive['attributes'].values())
            if 'indices' in primitive:
                core_accessors.add(primitive['indices'])
    core_accessors.update(skin['inverseBindMatrices'] for skin in source.get('skins', []) if 'inverseBindMatrices' in skin)
    for old_id in core_accessors:
        assert payload_for(source, binary, old_id) == payload_for(doc, rebuilt, accessor_map[old_id])

    encoded_json = json.dumps(doc, separators=(',', ':')).encode()
    encoded_json += b' ' * (-len(encoded_json) % 4)
    rebuilt.extend(b'\0' * (-len(rebuilt) % 4))
    total = 12 + 8 + len(encoded_json) + 8 + len(rebuilt)
    result = (struct.pack('<III', 0x46546C67, 2, total)
              + struct.pack('<II', len(encoded_json), 0x4E4F534A) + encoded_json
              + struct.pack('<II', len(rebuilt), 0x004E4942) + rebuilt)
    destination = DEST / path.name
    destination.write_bytes(result)
    return {
        'name': path.stem, 'source': str(path.relative_to(ROOT)),
        'output': str(destination.relative_to(ROOT)),
        'source_bytes': len(source_bytes), 'output_bytes': len(result),
        'source_sha256': hashlib.sha256(source_bytes).hexdigest(),
        'output_sha256': hashlib.sha256(result).hexdigest(),
        'kept_morph_names': sorted(KEEP_MORPHS),
        'morph_targets_before': before_targets, 'morph_targets_after': after_targets,
        'retained_core_accessors_verified': len(core_accessors),
        'retained_nonimage_buffer_views': retained_core_views,
        'retained_buffer_bytes_sha256': preserved_hash.hexdigest(),
        'mesh_skin_index_bytes_unchanged': True,
    }


if __name__ == '__main__':
    DEST.mkdir(parents=True, exist_ok=True)
    records = []
    for name in ['Priya', 'Ivy', 'Aaron', 'Nick']:
        record = optimize(SOURCE / f'{name}.glb')
        records.append(record)
        print(json.dumps(record), flush=True)
    (DEST / 'manifest.json').write_text(json.dumps(records, indent=2) + '\n')
