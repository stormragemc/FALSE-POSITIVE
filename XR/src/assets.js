import { AssetType, defineAssets } from '@iwsdk/core';
import { interrogationRoom, cabinRoom, desk, chair, photo, radio, key, memoryDoor, sofa } from './scene-assets/room.js';
const url=(p)=>`${import.meta.env.BASE_URL}${p}`;
export default defineAssets({
 sofa:sofa(), room:interrogationRoom(), cabin:cabinRoom(), desk:desk(), chair:chair(), photo:photo(), radio:radio(), key:key(), door:memoryDoor(),
 ...Object.fromEntries(['aaron','ivy','nick','priya'].map(role=>[role,{url:url(`models/${role}.glb`),type:AssetType.GLTF,priority:'critical'}])),
 officer:{url:url('models/officer.glb'),type:AssetType.GLTF,priority:'critical'},
 ...Object.fromEntries(['desk','chair','sofa'].map(name=>[`${name}-detail`,{url:url(`models/${name}-detail.glb`),type:AssetType.GLTF,priority:'critical'}])),
 'cabin-wood':{url:url('media/images/cabin-wood.jpg'),type:AssetType.Texture,priority:'critical'},
 'cast-photo':{url:url('media/images/full-cast.webp'),type:AssetType.Texture,priority:'critical'},
 controls:{url:url('ui/controls.uikitml'),type:AssetType.UIKitML},
 keyboard:{url:url('ui/keyboard.uikitml'),type:AssetType.UIKitML},
});
