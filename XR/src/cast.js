import { Euler, Matrix4, Quaternion, Vector3 } from '@iwsdk/core';
import { setupOfficer } from './officer.js';

/** Pose the original cast skins. Placement/yaw remain the stage's property. */
export function setupCastActor(root, { pose = 'standing', role = '', seatHeight = 0.535 } = {}) {
  const bones = new Map(), faces = [];
  root.traverse((object) => {
    if (object.isBone) bones.set(object.name.replace(/^mixamorig[:_]?/, ''), object);
    if (object.isMesh) {
      object.castShadow = true; object.receiveShadow = true;
      if (object.isSkinnedMesh) object.frustumCulled = false;
      if (object.morphTargetDictionary && object.morphTargetInfluences) faces.push(object);
    }
  });
  const hips = bones.get('Hips');
  if (!hips || !bones.get('Head')) throw new Error(`Cast rig is incomplete: ${role || root.name}`);
  const rig = hips.parent;
  const originalRig = { position: rig.position.clone(), quaternion: rig.quaternion.clone() };
  const original = new Map([...bones].map(([name, bone]) => [name, {
    position: bone.position.clone(), quaternion: bone.quaternion.clone(),
  }]));
  const posed = new Map();
  const euler = new Euler(), rotation = new Quaternion();
  let currentPose = '', seated = null, speech = 0, clock = 0;

  function localPoint(x, y, z) { return rig.localToWorld(new Vector3(x, y, z)); }
  function orient(bone, q) {
    const parentQ = bone.parent.getWorldQuaternion(new Quaternion());
    bone.quaternion.copy(parentQ.invert().multiply(q));
    bone.updateWorldMatrix(false, true);
  }
  function aim(bone, child, target) {
    const start = bone.getWorldPosition(new Vector3());
    const from = child.getWorldPosition(new Vector3()).sub(start).normalize();
    const to = target.clone().sub(start).normalize();
    const q = new Quaternion().setFromUnitVectors(from, to);
    orient(bone, q.multiply(bone.getWorldQuaternion(new Quaternion())));
  }
  function arm(side, x, y, z, poleX, poleY, poleZ) {
    const upper = bones.get(`${side}Arm`), lower = bones.get(`${side}ForeArm`), hand = bones.get(`${side}Hand`);
    if (!upper || !lower || !hand) return;
    const start = upper.getWorldPosition(new Vector3()), middle = lower.getWorldPosition(new Vector3());
    const tip = hand.getWorldPosition(new Vector3()), target = localPoint(x, y, z);
    const a = start.distanceTo(middle), b = middle.distanceTo(tip), axis = target.clone().sub(start);
    const d = Math.min(a + b - 0.0001, Math.max(Math.abs(a - b) + 0.0001, axis.length()));
    axis.normalize();
    const along = (a * a - b * b + d * d) / (2 * d);
    const bend = localPoint(poleX, poleY, poleZ).sub(start);
    bend.addScaledVector(axis, -bend.dot(axis)).normalize();
    const elbow = start.clone().addScaledVector(axis, along).addScaledVector(bend, Math.sqrt(Math.max(0, a * a - along * along)));
    aim(upper, lower, elbow); aim(lower, hand, target);
  }
  function morph(name, value) {
    for (const face of faces) {
      const index = face.morphTargetDictionary[name];
      if (index !== undefined) face.morphTargetInfluences[index] = value;
    }
  }
  function resetRig() {
    seated?.restore(); seated = null;
    rig.position.copy(originalRig.position); rig.quaternion.copy(originalRig.quaternion);
    for (const [name, bone] of bones) {
      bone.position.copy(original.get(name).position); bone.quaternion.copy(original.get(name).quaternion);
    }
    root.updateWorldMatrix(true, true);
  }

  const actor = {
    root, bones, role,
    get pose() { return currentPose; },
    /** standing, sitting, lying, carry, toast, or guarded. */
    setPose(nextPose = 'standing') {
      if (!['standing', 'sitting', 'lying', 'carry', 'toast', 'guarded'].includes(nextPose)) {
        throw new Error(`Unknown cast pose: ${nextPose}`);
      }
      if (nextPose === currentPose) return actor;
      resetRig(); currentPose = nextPose;
      if (nextPose === 'sitting') {
        seated = setupOfficer(root, { seatHeight, handHeight: seatHeight + 0.12, handDepth: 0.19 });
      } else {
        const hipHeight = original.get('Hips').position.y;
        if (nextPose === 'carry') {
          bones.get('Spine')?.rotateX(0.18);
          bones.get('Spine1')?.rotateX(0.08);
          root.updateWorldMatrix(true, true);
        }
        for (const [side, sign] of [['Left', 1], ['Right', -1]]) {
          const upper = bones.get(`${side}Arm`), lower = bones.get(`${side}ForeArm`), hand = bones.get(`${side}Hand`);
          if (!upper || !lower || !hand) continue;
          const shoulder = rig.worldToLocal(upper.getWorldPosition(new Vector3()));
          const reach = upper.getWorldPosition(new Vector3()).distanceTo(lower.getWorldPosition(new Vector3()))
            + lower.getWorldPosition(new Vector3()).distanceTo(hand.getWorldPosition(new Vector3()));
          let handY = shoulder.y - reach * 0.95, handZ = 0.04, handX = sign * Math.max(0.20, Math.abs(shoulder.x) + 0.035);
          // The carry holds Nick beneath both ankles. Close palms match the
          // actual lying rig's ~0.20m ankle separation, at waist height.
          if (nextPose === 'carry') { handY = hipHeight + 0.10; handZ = 0.40; handX = sign * 0.105; }
          if (nextPose === 'guarded') { handY = hipHeight + 0.18; handZ = 0.18; handX = sign * 0.12; }
          if (nextPose === 'toast' && side === 'Right') { handY = hipHeight + 0.48; handZ = 0.28; handX = -0.30; }
          arm(side, handX, handY, handZ, sign * 0.65, hipHeight + 0.25, -0.12);
          const rigQ = rig.getWorldQuaternion(new Quaternion());
          const xAxis = new Vector3(0, 0, -sign), yAxis = new Vector3(0, -1, 0), zAxis = new Vector3(-sign, 0, 0);
          if (nextPose === 'carry' || nextPose === 'guarded') {
            xAxis.set(1, 0, 0); yAxis.set(0, 0, 1); zAxis.set(0, -1, 0);
          }
          if (nextPose === 'carry') { xAxis.set(-1, 0, 0); zAxis.set(0, 1, 0); }
          const handQ = new Quaternion().setFromRotationMatrix(new Matrix4().makeBasis(xAxis, yAxis, zAxis));
          orient(hand, rigQ.multiply(handQ));
          for (const finger of ['Index', 'Middle', 'Ring', 'Pinky']) for (const joint of [1, 2, 3]) {
            bones.get(`${side}Hand${finger}${joint}`)?.rotateX(joint === 2 ? 0.13 : 0.08);
          }
        }
        if (nextPose === 'lying') {
          rig.quaternion.copy(originalRig.quaternion).multiply(new Quaternion().setFromAxisAngle(new Vector3(1, 0, 0), -Math.PI / 2));
          rig.position.y += 0.125;
        }
      }
      root.updateWorldMatrix(true, true);
      posed.clear(); for (const [name, bone] of bones) posed.set(name, bone.quaternion.clone());
      return actor;
    },
    update({ time, delta = 1 / 60, talking = false, walking = false, dead = false, audioLevel } = {}) {
      clock = Number.isFinite(time) ? time : clock + Math.min(delta, 0.1);
      if (seated) { seated.update(talking, delta, clock, audioLevel); return; }
      speech += ((talking && !dead ? 1 : 0) - speech) * Math.min(1, delta * 10);
      apply('Spine2', dead ? 0 : 0.007 * Math.sin(clock * 1.3));
      apply('Head', dead ? 0 : 0.009 * Math.sin(clock * 0.8), dead ? 0 : 0.025 * Math.sin(clock * 0.34));
      apply('LeftArm', 0); apply('RightArm', 0);
      if (walking && currentPose !== 'lying') {
        const stride = Math.sin(clock * 6.1);
        apply('LeftUpLeg', stride * 0.24); apply('RightUpLeg', -stride * 0.24);
        apply('LeftLeg', -Math.max(0, -stride) * 0.32); apply('RightLeg', -Math.max(0, stride) * 0.32);
        if (currentPose === 'standing') { apply('LeftArm', -stride * 0.13); apply('RightArm', stride * 0.13); }
      } else for (const name of ['LeftUpLeg', 'RightUpLeg', 'LeftLeg', 'RightLeg']) apply(name, 0);
      const envelope = Number.isFinite(audioLevel) ? Math.max(0, Math.min(1, audioLevel * 4)) : 0.3 + 0.7 * Math.abs(Math.sin(clock * 9.1));
      morph('jawOpen', speech * envelope * 0.2); morph('mouthOpen', speech * envelope * 0.16); morph('viseme_aa', speech * envelope * 0.1);
      const blinkPhase = (clock + 1.1) % 4.7;
      const blink = dead ? 1 : blinkPhase < 0.18 ? Math.sin(blinkPhase / 0.18 * Math.PI) : 0;
      morph('eyeBlinkLeft', blink); morph('eyeBlinkRight', blink); morph('eyesClosed', dead ? 1 : 0);
    },
    restore() {
      resetRig(); currentPose = '';
      for (const name of ['mouthOpen', 'jawOpen', 'viseme_aa', 'eyeBlinkLeft', 'eyeBlinkRight', 'eyesClosed']) morph(name, 0);
    },
  };
  function apply(name, x, y = 0, z = 0) {
    const bone = bones.get(name); if (!bone || !posed.has(name)) return;
    bone.quaternion.copy(posed.get(name)).multiply(rotation.setFromEuler(euler.set(x, y, z)));
  }
  actor.setPose(pose);
  return actor;
}
