import { Euler, Matrix4, Quaternion, Vector3 } from '@iwsdk/core';

// NewCop.glb's native rig is measured in metres and faces +Z.  Posing the
// actual skin retains its face, clothing and hand detail; no proxy is used.
const officers = new WeakMap();

export function setupOfficer(root, { seatHeight = 0.535 } = {}) {
  if (officers.has(root)) return officers.get(root);
  const bones = new Map();
  const faces = [];
  root.traverse((object) => {
    if (object.isBone) bones.set(object.name.replace(/^mixamorig[:_]?/, ''), object);
    if (object.isMesh && object.morphTargetDictionary && object.morphTargetInfluences) {
      faces.push(object);
    }
    if (object.isMesh) {
      object.castShadow = true;
      object.receiveShadow = true;
      // A posed skinned mesh extends beyond its imported bind-pose bounds.
      // There is one officer, so correctness costs less than per-frame bounds.
      if (object.isSkinnedMesh) object.frustumCulled = false;
    }
  });
  const required = ['Hips', 'Head', 'Spine2', 'LeftArm', 'LeftForeArm', 'LeftHand',
    'RightArm', 'RightForeArm', 'RightHand', 'LeftUpLeg', 'LeftLeg', 'LeftFoot',
    'RightUpLeg', 'RightLeg', 'RightFoot'];
  const missing = required.filter((name) => !bones.has(name));
  if (missing.length) throw new Error(`Officer rig is missing: ${missing.join(', ')}`);

  const original = new Map([...bones].map(([name, bone]) => [name, {
    position: bone.position.clone(), quaternion: bone.quaternion.clone(),
  }]));
  const hips = bones.get('Hips');
  const rig = hips.parent;
  hips.position.y = seatHeight;
  root.updateWorldMatrix(true, true);

  function rigPoint(x, y, z) {
    return rig.localToWorld(new Vector3(x, y, z));
  }

  function worldRotation(bone, quaternion) {
    const parentRotation = bone.parent.getWorldQuaternion(new Quaternion());
    bone.quaternion.copy(parentRotation.invert().multiply(quaternion));
    bone.updateWorldMatrix(false, true);
  }

  function aimBone(bone, child, target) {
    const origin = bone.getWorldPosition(new Vector3());
    const from = child.getWorldPosition(new Vector3()).sub(origin).normalize();
    const to = target.clone().sub(origin).normalize();
    const turn = new Quaternion().setFromUnitVectors(from, to);
    const rotation = bone.getWorldQuaternion(new Quaternion());
    worldRotation(bone, turn.multiply(rotation));
  }

  // Analytic two-bone IK. The bend pole fixes elbows outside the torso and
  // knees in front of the chair, instead of accepting arbitrary joint rolls.
  function limb(upperName, lowerName, tipName, target, pole) {
    const upper = bones.get(upperName), lower = bones.get(lowerName), tip = bones.get(tipName);
    const start = upper.getWorldPosition(new Vector3());
    const middle = lower.getWorldPosition(new Vector3());
    const end = tip.getWorldPosition(new Vector3());
    const a = start.distanceTo(middle), b = middle.distanceTo(end);
    const axis = target.clone().sub(start);
    const distance = Math.min(a + b - 0.0001, Math.max(Math.abs(a - b) + 0.0001, axis.length()));
    axis.normalize();
    const along = (a * a - b * b + distance * distance) / (2 * distance);
    const bend = pole.clone().sub(start);
    bend.addScaledVector(axis, -bend.dot(axis)).normalize();
    const elbow = start.clone().addScaledVector(axis, along)
      .addScaledVector(bend, Math.sqrt(Math.max(0, a * a - along * along)));
    aimBone(upper, lower, elbow);
    aimBone(lower, tip, target);
  }

  for (const [side, sign] of [['Left', 1], ['Right', -1]]) {
    limb(`${side}UpLeg`, `${side}Leg`, `${side}Foot`,
      rigPoint(sign * 0.115, 0.085, 0.43), rigPoint(sign * 0.11, 0.47, 0.8));
    const foot = bones.get(`${side}Foot`), toe = bones.get(`${side}ToeBase`);
    if (toe) {
      const ankle = foot.getWorldPosition(new Vector3());
      const direction = rigPoint(0, -0.06, 0.14).sub(rigPoint(0, 0, 0));
      aimBone(foot, toe, ankle.add(direction));
    }
    limb(`${side}Arm`, `${side}ForeArm`, `${side}Hand`,
      rigPoint(sign * 0.205, 0.795, side === 'Left' ? 0.34 : 0.31),
      rigPoint(sign * 0.65, 0.77, -0.1));
    const rigRotation = rig.getWorldQuaternion(new Quaternion());
    const handRotation = new Quaternion().setFromRotationMatrix(new Matrix4().makeBasis(
      new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, -1, 0),
    ));
    worldRotation(bones.get(`${side}Hand`), rigRotation.multiply(handRotation));
    for (const finger of ['Index', 'Middle', 'Ring', 'Pinky']) {
      for (const [joint, curl] of [[1, 0.1], [2, 0.15], [3, 0.08]]) {
        const bone = bones.get(`${side}Hand${finger}${joint}`);
        if (bone) bone.rotateX(curl);
      }
    }
  }
  root.updateWorldMatrix(true, true);
  const posed = new Map([...bones].map(([name, bone]) => [name, bone.quaternion.clone()]));
  const head = bones.get('Head'), chest = bones.get('Spine2');
  const deltaRotation = new Quaternion(), euler = new Euler();
  let speech = 0;
  let clock = 0;

  function morph(name, value) {
    for (const face of faces) {
      const index = face.morphTargetDictionary[name];
      if (index !== undefined) face.morphTargetInfluences[index] = value;
    }
  }

  const controller = {
    root,
    bones,
    // talking is an audio-playback state, not testimony sentiment or guilt.
    // An optional measured audio level makes jaw motion follow actual speech.
    update(talking = false, delta = 1 / 60, time, audioLevel) {
      clock = Number.isFinite(time) ? time : clock + Math.min(delta, 0.1);
      const target = talking ? 1 : 0;
      speech += (target - speech) * Math.min(1, Math.max(0, delta) * 10);
      euler.set(0.008 * Math.sin(clock * 0.64) + speech * 0.008 * Math.sin(clock * 2.1),
        0.018 * Math.sin(clock * 0.38), 0.004 * Math.sin(clock * 0.51));
      deltaRotation.setFromEuler(euler);
      head.quaternion.copy(posed.get('Head')).multiply(deltaRotation);
      euler.set(0.006 * Math.sin(clock * 1.25), 0, 0);
      chest.quaternion.copy(posed.get('Spine2')).multiply(deltaRotation.setFromEuler(euler));
      const rightArm = bones.get('RightArm');
      euler.set(speech * 0.015 * Math.sin(clock * 1.7), 0, 0);
      rightArm.quaternion.copy(posed.get('RightArm')).multiply(deltaRotation.setFromEuler(euler));
      const envelope = Number.isFinite(audioLevel)
        ? Math.max(0, Math.min(1, audioLevel * 4))
        : 0.3 + 0.7 * Math.abs(Math.sin(clock * 9.1) * Math.cos(clock * 3.7));
      const mouth = speech * envelope;
      morph('jawOpen', mouth * 0.2);
      morph('mouthOpen', mouth * 0.16);
      morph('viseme_aa', mouth * 0.1);
      morph('viseme_E', mouth * 0.06 * Math.max(0, Math.sin(clock * 7)));
      const blinkPhase = (clock + 1.2) % 4.9;
      const blink = blinkPhase < 0.19 ? Math.sin(blinkPhase / 0.19 * Math.PI) : 0;
      morph('eyeBlinkLeft', blink);
      morph('eyeBlinkRight', blink);
    },
    restore() {
      for (const [name, bone] of bones) {
        bone.position.copy(original.get(name).position);
        bone.quaternion.copy(original.get(name).quaternion);
      }
      for (const name of ['jawOpen', 'mouthOpen', 'viseme_aa', 'viseme_E', 'eyeBlinkLeft', 'eyeBlinkRight']) morph(name, 0);
      officers.delete(root);
    },
  };
  controller.update(false, 1 / 60, 0);
  officers.set(root, controller);
  return controller;
}

export function poseOfficer(root, { talking = false, delta = 1 / 60, time = 0, audioLevel } = {}) {
  const controller = officers.get(root) ?? setupOfficer(root);
  controller.update(talking, delta, time, audioLevel);
  return controller;
}
