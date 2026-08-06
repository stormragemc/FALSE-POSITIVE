using FalsePositive.Flow;
using FalsePositive.Interaction;
using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// Lives in Memory_CabinNight.unity. Drives the M1_Night beat order from
    /// docs/STORY_SCRIPT.md §4: stand from chair -> free roam (objective "Fix
    /// the radio") -> radio clears -> someone left (door swings shut) ->
    /// objective "Go to the door" -> reaching the door triggers the loud
    /// call-for-Nick prompt -> fuzzy out. "Reaching the door" is a trigger
    /// volume, not an E-press Interactable — the door is never opened by the
    /// player here, per STORY_SCRIPT.md §4 ("reaching the door triggers it").
    /// Only touches same-scene objects and GameFlowDirector.Instance.
    /// </summary>
    public sealed class M1NightController : MonoBehaviour
    {
        [SerializeField] private RadioTuner radio;

        private GameFlowDirector _flow;
        private bool _radioCleared;
        private bool _doorReached;

        private void OnEnable()
        {
            _flow = GameFlowDirector.Instance;
            if (_flow != null) _flow.PhaseChanged += OnPhaseChanged;
            if (radio != null) radio.Cleared += OnRadioCleared;
        }

        private void OnDisable()
        {
            if (_flow != null) _flow.PhaseChanged -= OnPhaseChanged;
            if (radio != null) radio.Cleared -= OnRadioCleared;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.M1_Night) return;
            _radioCleared = false;
            _doorReached = false;
            _flow.RequestCutscene(CutsceneId.StandFromChair, () =>
            {
                _flow.Objectives?.Set("Fix the radio.");
            });
        }

        private void OnRadioCleared()
        {
            if (_radioCleared) return;
            _radioCleared = true;
            _flow.RequestCutscene(CutsceneId.RadioClears, () =>
            {
                _flow.RequestCutscene(CutsceneId.SomeoneLeft, () =>
                {
                    _flow.Objectives?.Set("Go to the door.");
                });
            });
        }

        /// <summary>This component's own GameObject carries the trigger
        /// collider (BoxCollider, isTrigger, positioned at the front door) —
        /// see MemorySceneDressing/Step 7 wiring.</summary>
        private void OnTriggerEnter(Collider other)
        {
            if (_doorReached || !_radioCleared) return;
            if (other.GetComponentInParent<CharacterController>() == null) return;
            _doorReached = true;

            _flow.RequestSpokenPrompt("Call out for Nick.", requireLoud: true, onSatisfied: () =>
            {
                _flow.Flags?.Set(MemoryFlagIds.CalledForNick);
                _flow.Flags?.Set(MemoryFlagIds.LeftDoorUnlocked);
                _flow.RequestCutscene(CutsceneId.FuzzyToInterrogation, () => _flow.AdvancePhase());
            });
        }
    }
}
