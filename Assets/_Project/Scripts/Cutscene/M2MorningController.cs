using FalsePositive.Flow;
using FalsePositive.Interaction;
using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// Lives in Memory_CabinMorning.unity. Drives the M2_Morning beat order:
    /// Priya screams -> they come down -> control returns, objective "Get
    /// outside" -> key + locked door -> out into the snow -> the carry -> the
    /// sofa -> fuzzy out. Only touches same-scene objects and
    /// GameFlowDirector.Instance.
    /// </summary>
    public sealed class M2MorningController : MonoBehaviour
    {
        [SerializeField] private DoorInteractable frontDoor;

        private GameFlowDirector _flow;
        private CutsceneStage _stage;
        private bool _doorOpened;

        private void OnEnable()
        {
            _flow = GameFlowDirector.Instance;
            _stage = FindAnyObjectByType<CutsceneStage>();
            if (_flow != null) _flow.PhaseChanged += OnPhaseChanged;
            if (frontDoor != null) frontDoor.Opened += OnDoorOpened;
        }

        private void OnDisable()
        {
            if (_flow != null) _flow.PhaseChanged -= OnPhaseChanged;
            if (frontDoor != null) frontDoor.Opened -= OnDoorOpened;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.M2_Morning) return;
            _doorOpened = false;

            _flow.RequestCutscene(CutsceneId.PriyaScreams, () =>
            {
                _flow.RequestCutscene(CutsceneId.TheyComeDown, () =>
                {
                    _flow.Objectives?.Set("Get outside.");
                });
            });
        }

        private void OnDoorOpened()
        {
            if (_doorOpened) return;
            _doorOpened = true;

            // _flow is only null if this scene is booted standalone (Play
            // pressed directly on Memory_CabinMorning) rather than reached
            // through GameFlowDirector's phase flow, in which case
            // OnEnable never resolved an Instance. Warn instead of throwing
            // an NRE — the door still opens, it just can't drive the rest
            // of the beat sequence without a flow to request cutscenes from.
            if (_flow == null)
            {
                Debug.LogWarning("[M2MorningController] Door opened but GameFlowDirector.Instance is null " +
                    "(scene booted standalone) — the carry sequence needs the full flow to play.");
                return;
            }

            _flow.RequestCutscene(CutsceneId.OutIntoTheSnow, () =>
            {
                _flow.Objectives?.Set("Help Aaron lift him.");

                // Gameplay interlude, not a cutscene beat — CutsceneBeat can
                // only WaitForSeconds, it has no way to wait on the player
                // pressing E, so this runs directly on CutsceneStage between
                // OutIntoTheSnow finishing and TheCarry starting.
                if (_stage != null)
                {
                    _stage.RunLiftInterlude(() => RequestCarrySequence());
                }
                else
                {
                    RequestCarrySequence();
                }
            });
        }

        private void RequestCarrySequence()
        {
            _flow.Objectives?.Set("Bring him to the sofa.");

            // TheCarry's own recipe (VO/beats) finishes on a fixed clock —
            // CutsceneStage.TheCarry now hands the player control instead of
            // scripting the walk back, so how long the actual carry takes
            // depends on the player, not that clock. RunCarryArrival is the
            // real gate: it waits for the player to physically reach the
            // sofa before TheSofa plays, whether that's before or after the
            // dialogue beats finish.
            _flow.RequestCutscene(CutsceneId.TheCarry, () =>
            {
                _flow.Flags?.Set(MemoryFlagIds.CarriedBody);
                if (_stage != null)
                {
                    _stage.RunCarryArrival(RequestSofaSequence);
                }
                else
                {
                    RequestSofaSequence();
                }
            });
        }

        private void RequestSofaSequence()
        {
            _flow.RequestCutscene(CutsceneId.TheSofa, () =>
            {
                _flow.RequestCutscene(CutsceneId.FuzzyToVerdict, () => _flow.AdvancePhase());
            });
        }
    }
}
