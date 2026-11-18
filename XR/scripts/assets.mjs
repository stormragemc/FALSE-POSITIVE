import { cp, mkdir, readFile, writeFile, readdir } from 'node:fs/promises';
import sharp from 'sharp';
import { basename } from 'node:path';
import { prepareCast } from './prepare-cast.mjs';
import { CINEMATIC_SOURCE_FILES } from '../src/cinematics.js';
const base = new URL('../', import.meta.url);
await mkdir(new URL('public/models/',base),{recursive:true});
await mkdir(new URL('public/media/',base),{recursive:true});
await cp(new URL('../Assets/_Project/Art/NewCop.glb',base),new URL('public/models/officer.glb',base));
await cp(new URL('../Sidecar/web/images',base),new URL('public/media/images',base),{recursive:true});
await cp(new URL('../Sidecar/web/audio',base),new URL('public/media/audio',base),{recursive:true});
for(const file of ['public/media/images/cabin-group.webp','public/media/images/full-cast.webp']) {
 const bytes=await readFile(new URL(file,base));
 if(bytes.toString('utf8',0,7)==='version') throw new Error('Git LFS assets are missing. Run git lfs pull --include="Sidecar/web/**"');
}
console.log('Prepared original officer, images and authored voice assets.');

await prepareCast();
for (const source of CINEMATIC_SOURCE_FILES) {
 const folder=source.includes('/SFX/')?'sfx':'vo';
 await mkdir(new URL(`public/media/${folder}/`,base),{recursive:true});
 const bytes=await readFile(new URL(`../${source}`,base));
 if(bytes.toString('utf8',0,7)==='version')throw new Error(`Required cinematic audio was not hydrated: ${source}`);
 await writeFile(new URL(`public/media/${folder}/${basename(source)}`,base),bytes);
}
console.log('Prepared all canonical cinematic voices, effects and cast models.');
await sharp(new URL('../Assets/_Project/Art/Cabin_v2/Textures/weathered_plank_siding_diff_4k.jpg',base).pathname).resize(1024).jpeg({quality:85}).toFile(new URL('public/media/images/cabin-wood.jpg',base).pathname);
const effects=new URL('../Assets/_Project/Art/Audio/SFX/',base);
for(const name of await readdir(effects))if(/\.(mp3|wav)$/i.test(name))await cp(new URL(name,effects),new URL(`public/media/sfx/${name}`,base));

for(const name of ['desk','chair','sofa'])await cp(new URL(`../Assets/_Project/Art/XR/Cinematic/${name}.glb`,base),new URL(`public/models/${name}-detail.glb`,base));
