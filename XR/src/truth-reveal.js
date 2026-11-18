/** Post-verdict reconstruction. Never feeds knowledge into David's testimony. */
const act = (id, stage, text, duration, audio = null) => ({
 id, canonId: `RECON-${id}`, stage, fadeOutSeconds: .35, fadeInSeconds: .5,
 keepScreenLit: true, endsBlack: false,
 beats: [{speaker:'', text, audio, duration, holdSeconds:duration, minimumSeconds:duration,
  stage, timeCard:'', timeCardCaption:'', dipToBlackBefore:false, memoryFlag:''}],
});
export const TRUTH_CINEMATICS = Object.freeze({
 TruthCoats: act('TruthCoats','truth-coats',
  "What David could not see. Earlier, David and Nick swapped coats. Nick went outside wearing the thin jacket.",5),
 TruthWindow: act('TruthWindow','truth-window',
  '01:20. Aaron confronts Nick about Ivy. Nick strikes the window and falls unconscious. The grille holds.',8),
 TruthLock: act('TruthLock','truth-lock',
  '01:30. Aaron comes inside, locks the door and hangs the key on its hook. Ivy sees him return.',7),
 TruthCold: act('TruthCold','truth-cold',
  'Left unconscious in the storm, Nick cannot get back inside. In the thin jacket, he dies of hypothermia before morning.',10),
 TruthMorning: act('TruthMorning','truth-morning',
  'Later that morning. After Priya finds Nick outside, Aaron and David carry him to the sofa, disturbing the scene and putting David’s hands on the body.',7),
 TruthConclusion: act('TruthConclusion','truth-conclusion',
  'Every fact against David was real. The conclusion could still be wrong. Your testimony decided who was taken; it did not change what happened.',5),
});
export const TRUTH_SEQUENCE = Object.freeze(Object.keys(TRUTH_CINEMATICS));
