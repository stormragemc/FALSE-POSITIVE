import { cp, mkdir, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
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
