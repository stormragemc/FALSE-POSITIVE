import { AssetType, defineAssets } from '@iwsdk/core';
import { interrogationRoom, cabinRoom, desk, chair, photo, radio, key, memoryDoor } from './scene-assets/room.js';
const url=(p)=>`${import.meta.env.BASE_URL}${p}`;
export default defineAssets({
 room:interrogationRoom(), cabin:cabinRoom(), desk:desk(), chair:chair(), photo:photo(), radio:radio(), key:key(), door:memoryDoor(),
 officer:{url:url('models/officer.glb'),type:AssetType.GLTF,priority:'critical'},
 controls:{url:url('ui/controls.uikitml'),type:AssetType.UIKitML},
 keyboard:{url:url('ui/keyboard.uikitml'),type:AssetType.UIKitML},
});
