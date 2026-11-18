import { Euler, Matrix4, Quaternion, Vector3 } from '@iwsdk/core';

// NewCop.glb's native rig is measured in metres and faces +Z.  Posing the
// actual skin retains its face, clothing and hand detail; no proxy is used.
const officers = new WeakMap();

export function setupOfficer(root, { seatHeight = 0.535, handHeight = 0.827, handDepth = 0.34 } = {}) {
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
      rigPoint(sign * 0.205, handHeight, side === 'Left' ? handDepth : handDepth - 0.03),
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
    update(talking = false, delta = 1 / 60, time, audioLevel, performance = {}) {
      clock = Number.isFinite(time) ? time : clock + Math.min(delta, 0.1);
      const target = talking ? 1 : 0;
      speech += (target - speech) * Math.min(1, Math.max(0, delta) * 10);
      euler.set(0.008 * Math.sin(clock * 0.64) + speech * 0.008 * Math.sin(clock * 2.1),
        0.018 * Math.sin(clock * 0.38), 0.004 * Math.sin(clock * 0.51));
      deltaRotation.setFromEuler(euler);
      head.quaternion.copy(posed.get('Head')).multiply(deltaRotation);
      euler.set(0.006 * Math.sin(clock * 1.25), 0, 0);
      chest.quaternion.copy(posed.get('Spine2')).multiply(deltaRotation.setFromEuler(euler));
      for (const name of ['Spine','Spine1']) {
        const bone = bones.get(name); if (!bone) continue;
        euler.set((performance.lean || 0) * (name === 'Spine' ? 1 : .35),0,0);
        bone.quaternion.copy(posed.get(name)).multiply(deltaRotation.setFromEuler(euler));
      }
      if (performance.gaze || performance.nod) {
        head.quaternion.multiply(deltaRotation.setFromEuler(euler.set(performance.nod || 0,performance.gaze || 0,0)));
      }
      root.updateWorldMatrix(true,true);
      // Re-solve against the table/lap after breathing and leaning. A fixed arm
      // rotation alone lets the hands drift with every parent spine movement.
      for (const [side,sign] of [['Left',1],['Right',-1]]) {
        for (const part of ['Arm','ForeArm','Hand']) bones.get(side+part).quaternion.copy(posed.get(side+part));
        root.updateWorldMatrix(true,true);
        const target=performance[side.toLowerCase()] || [sign*.205,handHeight,handDepth-(side==='Right'?.03:0)];
        limb(side+'Arm',side+'ForeArm',side+'Hand',rigPoint(...target),rigPoint(sign*.42,.88,-.02));
        const palm=new Quaternion().setFromRotationMatrix(new Matrix4().makeBasis(new Vector3(1,0,0),new Vector3(0,0,1),new Vector3(0,-1,0)));
        worldRotation(bones.get(side+'Hand'),rig.getWorldQuaternion(new Quaternion()).multiply(palm));
        // Flatten the thumb against the table; imported thumb bind poses point down.
        for(const i of [1,2]){
          const bone=bones.get(`${side}HandThumb${i}`),child=bones.get(`${side}HandThumb${i+1}`);
          if(!bone||!child)continue;
          bone.quaternion.copy(posed.get(`${side}HandThumb${i}`));root.updateWorldMatrix(true,true);
          const start=bone.getWorldPosition(new Vector3()),end=child.getWorldPosition(new Vector3());
          end.y=start.y;aimBone(bone,child,end);
        }
      }
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
