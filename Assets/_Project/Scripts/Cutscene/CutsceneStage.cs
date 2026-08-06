using System.Collections;
using FalsePositive.CabinNight;
using FalsePositive.Flow;
using FalsePositive.Player;
using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// Per-memory-scene procedural staging: who walks where, where the
    /// camera looks, what gets posed, for the cutscene beats that need more
    /// than CutsceneDirector's default fade+VO (see the plan's Phase 4
    /// table). Everything else — fuzzy transitions, wake, Spassky's answer,
    /// radio clears, flashbacks, endings — is fade/VO-only and needs nothing
    /// here.
    ///
    /// One instance per memory scene (added by MemorySceneBuilderV2, `isMorning`
    /// set per scene). Subscribes to the single persistent CutsceneDirector's
    /// Started event in OnEnable/unsubscribes in OnDisable — SceneRouter
    /// deactivates non-active scenes' roots, so only the currently active
    /// scene's instance is ever subscribed, without any phase-checking here.
    /// </summary>
    public sealed class CutsceneStage : MonoBehaviour
    {
        [SerializeField] private bool isMorning;

        public void Configure(bool isMorningScene) => isMorning = isMorningScene;

        // Door hinge/rotation convention duplicated from CabinV2Builder
        // (Scripts/Editor/, not reachable from this runtime assembly) —
        // that class remains the source of truth if these ever need to
        // change; see its DoorClosedRotation/DoorOpenYawDegrees doc comment
        // for the full derivation.
        private static readonly Quaternion DoorClosedRotation = Quaternion.Euler(270f, 0f, 0f);
        private const float DoorOpenYawDegrees = 100f;

        private CutsceneDirector _director;

        private void OnEnable()
        {
            _director = FindAnyObjectByType<CutsceneDirector>();
            if (_director != null) _director.Started += HandleStarted;
        }

        private void OnDisable()
        {
            if (_director != null) _director.Started -= HandleStarted;
        }

        private void HandleStarted(CutsceneId id)
        {
            IEnumerator routine = isMorning ? MorningRoutine(id) : NightRoutine(id);
            if (routine != null) StartCoroutine(routine);
        }

        private IEnumerator NightRoutine(CutsceneId id)
        {
            switch (id)
            {
                case CutsceneId.StandFromChair:
                    return StandFromChair();
                case CutsceneId.SomeoneLeft:
                    return SomeoneLeft();
                default:
                    return null;
            }
        }

        private IEnumerator MorningRoutine(CutsceneId id)
        {
            switch (id)
            {
                case CutsceneId.PriyaScreams:
                    return PriyaScreams();
                case CutsceneId.TheyComeDown:
                    return TheyComeDown();
                case CutsceneId.OutIntoTheSnow:
                    return OutIntoTheSnow();
                case CutsceneId.TheCarry:
                    return TheCarry();
                case CutsceneId.TheSofa:
                    return TheSofa();
                default:
                    return null;
            }
        }

        // ---- M1_Night ----

        private IEnumerator StandFromChair()
        {
            Transform view = FindPlayerView();
            if (view == null) yield break;

            Vector3 seated = new Vector3(view.localPosition.x, 1.0f, view.localPosition.z);
            Vector3 standing = new Vector3(view.localPosition.x, 1.64f, view.localPosition.z);
            view.localPosition = seated;

            // CutsceneRecipeBuilder gives StandFromChair a single 0.6s SFX
            // beat (chair_creak) — this must finish within that window or
            // the fade-in reveals the camera still mid-rise.
            float t = 0f;
            const float duration = 0.5f;
            while (t < duration)
            {
                t += Time.deltaTime;
                view.localPosition = Vector3.Lerp(seated, standing, t / duration);
                yield return null;
            }
            view.localPosition = standing;
        }

        private IEnumerator SomeoneLeft()
        {
            GameObject door = GameObject.Find("Prop_FrontDoor_Locked");
            FreeLookCameraRig rig = FindPlayerRig();
            if (rig == null) yield break;

            Vector3 doorPos = door != null ? door.transform.position : rig.transform.position + rig.transform.forward;
            Vector3 toDoor = doorPos - rig.transform.position;
            float yaw = Quaternion.LookRotation(new Vector3(toDoor.x, 0f, toDoor.z).normalized, Vector3.up).eulerAngles.y;
            rig.SeedYaw(yaw);
            rig.SeedPitch(5f);

            if (door != null)
            {
                // CutsceneRecipeBuilder gives SomeoneLeft one 1.5s SFX beat
                // (door_latch_close) — the door is already open (blown by
                // the storm) the instant the screen goes black, and swings
                // fully shut across that whole 1.5s so the latch sound lands
                // right as it closes, all before the fade back in.
                Quaternion open = Quaternion.Euler(0f, DoorOpenYawDegrees, 0f) * DoorClosedRotation;
                door.transform.rotation = open;

                float t = 0f;
                const float duration = 1.4f;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    door.transform.rotation = Quaternion.Slerp(open, DoorClosedRotation, t / duration);
                    yield return null;
                }
                door.transform.rotation = DoorClosedRotation;
            }
        }

        // ---- M2_Morning ----

        private IEnumerator PriyaScreams()
        {
            GameObject priya = GameObject.Find("Priya Raman (Female)");
            ScriptedActor actor = priya != null ? priya.GetComponent<ScriptedActor>() : null;
            actor?.PlayPose(CabinIdleProfile.Panicked);
            yield break;
        }

        private IEnumerator TheyComeDown()
        {
            GameObject aaron = GameObject.Find("Aaron Teague (Male)");
            GameObject ivy = GameObject.Find("Ivy Teague (Female)");

            Vector3 aaronFloor = new Vector3(3.2f, 0f, 1.5f);
            Vector3 ivyFloor = new Vector3(2.6f, 0f, 1.0f);

            // CutsceneRecipeBuilder gives TheyComeDown one 1.6s SFX beat
            // (footsteps_stairs) — speed picked so a ~3.5m landing-to-floor
            // move (including the vertical drop, since MoveTo treats XYZ
            // uniformly rather than actually walking the stairs) comfortably
            // finishes inside that window.
            yield return RunTogether(
                MoveActor(aaron, aaronFloor, 3.5f),
                MoveActor(ivy, ivyFloor, 3.5f));
        }

        private IEnumerator OutIntoTheSnow()
        {
            GameObject aaron = GameObject.Find("Aaron Teague (Male)");
            GameObject ivy = GameObject.Find("Ivy Teague (Female)");
            GameObject priya = GameObject.Find("Priya Raman (Female)");
            GameObject body = GameObject.Find("Prop_NickBody");
            Vector3 nearBody = body != null ? body.transform.position + new Vector3(1.2f, 0f, 0.5f) : new Vector3(2.3f, 0f, -5.6f);

            yield return RunTogether(
                MoveActor(aaron, nearBody, 2.2f),
                MoveActor(ivy, nearBody + new Vector3(-0.6f, 0f, 0.2f), 2.2f),
                MoveActor(priya, nearBody + new Vector3(0.4f, 0f, -0.6f), 2.2f));
        }

        private IEnumerator TheCarry()
        {
            GameObject aaron = GameObject.Find("Aaron Teague (Male)");
            GameObject body = GameObject.Find("Prop_NickBody");
            ScriptedActor aaronActor = aaron != null ? aaron.GetComponent<ScriptedActor>() : null;

            Vector3 sofaEnd = new Vector3(0.75f, 0f, 0.6f);
            Vector3 bodyStart = body != null ? body.transform.position : sofaEnd;
            Vector3 bodySofaRest = new Vector3(0.75f, 0.85f, 0.4f);

            const float duration = 8f;
            float t = 0f;

            if (aaronActor != null) aaronActor.PlayPose(CabinIdleProfile.Carrying);

            while (t < duration)
            {
                t += Time.deltaTime;
                float f = Mathf.Clamp01(t / duration);
                if (body != null) body.transform.position = Vector3.Lerp(bodyStart, bodySofaRest, f);
                if (aaron != null) aaron.transform.position = Vector3.Lerp(bodyStart, sofaEnd, f) + new Vector3(0f, 0f, -0.4f);
                yield return null;
            }

            if (body != null) body.transform.position = bodySofaRest;
            if (aaron != null) aaron.transform.position = sofaEnd;
            if (aaronActor != null) aaronActor.PlayPose(CabinIdleProfile.Controlled);
        }

        private IEnumerator TheSofa()
        {
            GameObject priya = GameObject.Find("Priya Raman (Female)");
            ScriptedActor actor = priya != null ? priya.GetComponent<ScriptedActor>() : null;
            actor?.PlayPose(CabinIdleProfile.Kneeling);
            yield break;
        }

        // ---- helpers ----

        private IEnumerator MoveActor(GameObject go, Vector3 destination, float speed)
        {
            if (go == null) yield break;
            ScriptedActor actor = go.GetComponent<ScriptedActor>();
            if (actor == null) yield break;
            yield return actor.MoveTo(destination, speed);
        }

        private IEnumerator RunTogether(params IEnumerator[] routines)
        {
            var running = new Coroutine[routines.Length];
            for (int i = 0; i < routines.Length; i++) running[i] = StartCoroutine(routines[i]);
            foreach (Coroutine c in running) yield return c;
        }

        private Transform FindPlayerView()
        {
            GameObject player = GameObject.Find("Player (Male - First Person)");
            return player != null ? player.transform.Find("FirstPersonView") : null;
        }

        private FreeLookCameraRig FindPlayerRig()
        {
            GameObject player = GameObject.Find("Player (Male - First Person)");
            return player != null ? player.GetComponent<FreeLookCameraRig>() : null;
        }
    }
}
