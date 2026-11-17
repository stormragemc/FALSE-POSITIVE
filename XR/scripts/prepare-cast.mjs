import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';
import { NodeIO, PropertyType } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { draco, prune, textureCompress } from '@gltf-transform/functions';
import draco3d from 'draco3dgltf';
import sharp from 'sharp';

const REPO = fileURLToPath(new URL('../../', import.meta.url));
const ROLES = ['Priya', 'Ivy', 'Aaron', 'Nick'];
const KEEP = new Set(['mouthOpen', 'jawOpen', 'eyeBlinkLeft', 'eyeBlinkRight', 'eyesClosed', 'viseme_aa']);

function signature(document) {
  const root = document.getRoot();
  return {
    meshes: root.listMeshes().map((mesh) => ({
      name: mesh.getName(),
      primitives: mesh.listPrimitives().map((primitive) => ({
        mode: primitive.getMode(),
        indices: primitive.getIndices()?.getCount() ?? null,
        triangles: primitive.getMode() === 4
          ? (primitive.getIndices()?.getCount() ?? primitive.getAttribute('POSITION').getCount()) / 3 : 0,
        targets: primitive.listTargets().map((target) => target.getName()),
      })),
    })),
    skins: root.listSkins().map((skin) => {
      const matrices = skin.getInverseBindMatrices()?.getArray();
      return {
        joints: skin.listJoints().map((joint) => ({ name: joint.getName(),
          translation: joint.getTranslation(), rotation: joint.getRotation(), scale: joint.getScale() })),
        inverseBindMatricesSHA256: matrices ? createHash('sha256')
          .update(new Uint8Array(matrices.buffer, matrices.byteOffset, matrices.byteLength)).digest('hex') : null,
      };
    }),
  };
}

function selectMorphs(document) {
  const root = document.getRoot();
  for (const animation of root.listAnimations()) {
    if (animation.listChannels().some((channel) => channel.getTargetPath() === 'weights')) {
      throw new Error('Cast optimizer cannot remove morph targets from a morph-weight animation.');
    }
  }
  for (const mesh of root.listMeshes()) {
    const first = mesh.listPrimitives()[0];
    if (!first?.listTargets().length) continue;
    const targets = first.listTargets();
    const indices = targets.flatMap((target, index) => KEEP.has(target.getName()) ? [index] : []);
    const targetNames = indices.map((index) => targets[index].getName());
    const weights = mesh.getWeights();
    for (const primitive of mesh.listPrimitives()) {
      const previous = primitive.listTargets();
      if (previous.length !== targets.length || previous.some((target, i) => target.getName() !== targets[i].getName())) {
        throw new Error(`Inconsistent morph-target layout in ${mesh.getName()}.`);
      }
      for (const [index, target] of previous.entries()) {
        if (!indices.includes(index)) primitive.removeTarget(target);
      }
    }
    mesh.setExtras({ ...mesh.getExtras(), targetNames });
    mesh.setWeights(indices.map((index) => weights[index] ?? 0));
    for (const node of root.listNodes()) {
      if (node.getMesh() !== mesh || !node.getWeights().length) continue;
      const previous = node.getWeights();
      node.setWeights(indices.map((index) => previous[index] ?? 0));
    }
  }
}

function verifySignature(expected, actual, name) {
  if (JSON.stringify(expected.meshes) !== JSON.stringify(actual.meshes)
    || expected.skins.length !== actual.skins.length) {
    throw new Error(`${name} lost triangle counts, mesh layout or selected morph targets.`);
  }
  let maxRestTRSDelta = 0;
  for (const [index, skin] of expected.skins.entries()) {
    const decoded = actual.skins[index];
    if (skin.inverseBindMatricesSHA256 !== decoded.inverseBindMatricesSHA256
      || skin.joints.length !== decoded.joints.length) {
      throw new Error(`${name} changed its inverse-bind matrices or joint count.`);
    }
    for (const [jointIndex, joint] of skin.joints.entries()) {
      const other = decoded.joints[jointIndex];
      if (joint.name !== other.name) throw new Error(`${name} changed its skin joint order.`);
      for (const field of ['translation', 'rotation', 'scale']) {
        for (let component = 0; component < joint[field].length; component++) {
          const delta = Math.abs(joint[field][component] - other[field][component]);
          // NodeIO omits near-identity TRS values within its 1e-6 writer
          // tolerance. This bound permits only that metadata canonicalization;
          // inverse-bind matrix bytes, names, order and triangle counts stay exact.
          if (!Number.isFinite(delta) || delta > 1e-6) {
            throw new Error(`${name} changed ${joint.name} rest ${field} by ${delta}.`);
          }
          maxRestTRSDelta = Math.max(maxRestTRSDelta, delta);
        }
      }
    }
  }
  return maxRestTRSDelta;
}

/** Build the actual, rigged cast from the original repository GLBs. */
export async function prepareCast({
  sourceDir = resolve(REPO, 'Assets/_Project/Art/Characters'),
  outputDir = resolve(REPO, 'XR/public/models'),
  names = ROLES,
} = {}) {
  const [decoder, encoder] = await Promise.all([
    draco3d.createDecoderModule(), draco3d.createEncoderModule(),
  ]);
  const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({
    'draco3d.decoder': decoder, 'draco3d.encoder': encoder,
  });
  await mkdir(outputDir, { recursive: true });
  const records = [];
  for (const name of names) {
    if (!ROLES.includes(name)) throw new Error(`Unknown cast member: ${name}`);
    const input = resolve(sourceDir, `${name}.glb`);
    const document = await io.read(input);
    const original = signature(document);
    selectMorphs(document);
    await document.transform(prune({
      propertyTypes: [PropertyType.PRIMITIVE_TARGET, PropertyType.ACCESSOR],
      keepAttributes: true, keepExtras: true,
    }));
    const expected = signature(document);
    if (original.skins.some((skin) => skin.joints.length !== 54)) {
      throw new Error(`${name} does not have the expected original 54-joint skin.`);
    }
    // Texture work must precede Draco: a later read/texture rewrite can decode
    // and remove Draco. Only this final write performs geometry compression.
    await document.transform(
      textureCompress({ encoder: sharp, resize: [512, 512], targetFormat: 'webp',
        quality: 90, effort: 85 }),
      draco({ quantizePosition: 16, quantizeNormal: 12, quantizeTexcoord: 14,
        quantizeGeneric: 16, decodeSpeed: 8, encodeSpeed: 5 }),
    );
    const binary = await io.writeBinary(document);
    const jsonLength = Buffer.from(binary.buffer, binary.byteOffset, binary.byteLength).readUInt32LE(12);
    const json = JSON.parse(Buffer.from(binary.buffer, binary.byteOffset + 20, jsonLength).toString('utf8'));
    for (const extension of ['KHR_draco_mesh_compression', 'EXT_texture_webp']) {
      if (!json.extensionsRequired?.includes(extension)) throw new Error(`${name} lost required ${extension}.`);
    }
    const decoded = await io.readBinary(binary);
    const actual = signature(decoded);
    const maxRestTRSDelta = verifySignature(expected, actual, name);
    for (const texture of decoded.getRoot().listTextures()) {
      if (texture.getMimeType() !== 'image/webp' || texture.getSize()?.some((size) => size > 512)) {
        throw new Error(`${name} texture format or resolution is outside the XR budget.`);
      }
    }
    const output = resolve(outputDir, `${name.toLowerCase()}.glb`);
    await writeFile(output, binary);
    const record = { name, output, bytes: binary.byteLength,
      sourceBytes: (await readFile(input)).byteLength,
      sha256: createHash('sha256').update(binary).digest('hex'),
      triangles: actual.meshes.flatMap((mesh) => mesh.primitives).reduce((sum, primitive) => sum + primitive.triangles, 0),
      joints: actual.skins[0].joints.length,
      maxRestTRSDelta,
      morphTargets: actual.meshes.flatMap((mesh) => mesh.primitives).reduce((sum, primitive) => sum + primitive.targets.length, 0),
    };
    records.push(record);
    console.log(`Prepared ${name}: ${(record.bytes / 1e6).toFixed(2)} MB, ${record.triangles} triangles, ${record.joints} joints, ${record.morphTargets} facial targets.`);
  }
  return records;
}

export default prepareCast;

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  const options = {};
  const args = process.argv.slice(2);
  for (let index = 0; index < args.length; index += 2) {
    const value = args[index + 1];
    if (!value) throw new Error(`Missing value for ${args[index]}`);
    if (args[index] === '--source-dir') options.sourceDir = resolve(value);
    else if (args[index] === '--output-dir') options.outputDir = resolve(value);
    else if (args[index] === '--names') options.names = value.split(',');
    else throw new Error(`Unknown option: ${args[index]}`);
  }
  await prepareCast(options);
}
