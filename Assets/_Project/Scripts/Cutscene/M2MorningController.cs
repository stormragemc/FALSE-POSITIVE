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
        private bool _doorOpened;

        private void OnEnable()
        {
            _flow = GameFlowDirector.Instance;
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

            _flow.RequestCutscene(CutsceneId.OutIntoTheSnow, () =>
            {
                _flow.RequestCutscene(CutsceneId.TheCarry, () =>
                {
                    _flow.Flags?.Set(MemoryFlagIds.CarriedBody);
                    _flow.RequestCutscene(CutsceneId.TheSofa, () =>
                    {
                        _flow.RequestCutscene(CutsceneId.FuzzyToVerdict, () => _flow.AdvancePhase());
                    });
                });
            });
        }
    }
}
