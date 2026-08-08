using System.Collections;
using System.Collections.Generic;
using FalsePositive.CabinNight;
using FalsePositive.Flow;
using FalsePositive.Interaction;
using FalsePositive.Player;
using FalsePositive.UI;
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

        // Wired by Editor.MemorySceneWiring alongside frontDoor-style fields —
        // "body_lift_effort" SFX for the lift interlude below. Null is fine
        // (LiftPrompt.OnInteract no-ops on a null clip) if that SFX hasn't
        // been generated yet.
        [SerializeField] private AudioClip liftEffortClip;

        // Ivy's one line during the lift ("Careful... careful. Easy.") — the
        // lift interlude is gameplay, not a CutsceneBeat, so it can't carry
        // dialogue through CutsceneRecipeBuilder the way TheCarry/TheSofa do;
        // this is played directly instead.
        [SerializeField] private AudioClip ivyLiftLineClip;

        public void Configure(bool isMorningScene) => isMorning = isMorningScene;

        // Door hinge/rotation convention duplicated from CabinV2Builder
        // (Scripts/Editor/, not reachable from this runtime assembly) —
        // that class remains the source of truth if these ever need to
        // change; see its DoorClosedRotation/DoorOpenYawDegrees doc comment
        // for the full derivation.
        private static readonly Quaternion DoorClosedRotation = Quaternion.Euler(270f, 0f, 0f);
        private const float DoorOpenYawDegrees = 100f;

        // BO_Sofa's measured collider (Unity_RunCommand, Cabin_v2/Memory_CabinMorning):
        // center (0.75, 0.43, 0.25), size (1.0, 0.85, 3.5) -> x in [0.25, 1.25],
        // z in [-1.5, 2.0] at the sofa's ORIGINAL yaw 0. The sofa's open face
        // is its local -X (Cabin_v2 README: Blender +X -> Unity -X), so these
        // rest spots sit just off that face — NOT the old (0.75, ·, ·) values,
        // which were inside the sofa's own box and would have left the player
        // standing (and previously the CharacterController re-enabling) INSIDE it.
        //
        // Stored as offsets in BO_SOFA'S LOCAL SPACE, resolved through
        // SofaPoint() at runtime. They used to be world-space, which was only
        // correct while the sofa sat axis-aligned at yaw 0; it is now turned to
        // face the fireplace (CabinV2Builder.SofaYaw), which sweeps that box to
        // x [-1.06, 2.56], z [-0.56, 1.06] and would have put every one of
        // these spots inside the furniture — the exact bug the paragraph above
        // records fixing once already. Local offsets follow the sofa at any yaw.
        private static readonly Vector3 SofaPlayerRestLocal = new Vector3(-1.15f, 0f, 0.05f);
        private static readonly Vector3 SofaAaronRestLocal = new Vector3(-1.15f, 0f, -0.75f);
        private static readonly Vector3 SofaBodyRestLocal = new Vector3(0f, 0.85f, 0.15f);

        /// <summary>Resolves a BO_Sofa-local offset to world space. Falls back
        /// to the sofa's authored origin so a missing/renamed sofa degrades to
        /// the old fixed layout instead of dumping actors at world zero.</summary>
        private static Vector3 SofaPoint(Vector3 localOffset)
        {
            GameObject sofa = GameObject.Find("BO_Sofa");
            return sofa != null
                ? sofa.transform.TransformPoint(localOffset)
                : new Vector3(0.75f, 0f, 0.25f) + localOffset;
        }

        // The front door sits in the 45-degree chamfered corner (x + z =
        // -7.5), not in an axis-aligned wall — confirmed against the door's
        // own measured collider, center (-3.75, 1.05, -3.75). A straight
        // line from anywhere inside the cabin to a point outside near
        // Prop_NickBody almost never passes through this gap, which is what
        // let the old MovePlayerTo/MoveTo calls walk everyone straight
        // through solid wall. These three waypoints route through it:
        // the doorway centre, a point just outside along the door's
        // exterior normal, and a point that rounds the chamfer corner
        // before turning toward the body (a direct line from just-outside-
        // the-door to the body re-enters the cabin through the z=-5 wall).
        private static readonly Vector3 DoorwayCentre = new Vector3(-3.75f, 0f, -3.75f);
        private static readonly Vector3 DoorwayOutside = new Vector3(-4.6f, 0f, -4.6f);
        private static readonly Vector3 ChamferCorner = new Vector3(-2.0f, 0f, -5.8f);

        private Coroutine _bodyFollowRoutine;
        private Coroutine _aaronFollowRoutine;

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
                case CutsceneId.GoodYears:
                    return GoodYears();
                case CutsceneId.WhenItWentWrong:
                    return WhenItWentWrong();
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
            GameObject player = GameObject.Find("Player (Male - First Person)");
            Vector3 bodyPos = body != null ? body.transform.position : new Vector3(2.3f, 0.1f, -6.3f);
            Vector3 nearBody = bodyPos + new Vector3(1.2f, 0f, 0.5f);

            // Pulled back from the body (was (-1.0, 0, 0.3), ~1.0 m out) to
            // (-1.6, 0, 0.6), ~1.7 m out — close enough to keep
            // InteractionRaycaster's 3 m range but far enough that looking
            // down at a body lying flat doesn't need a near-vertical pitch.
            // See SeedPitch below for the derivation.
            Vector3 playerSpot = bodyPos + new Vector3(-1.6f, 0f, 0.6f);

            // Player kept steerable through the door approach used to leave
            // the player sitting on the sofa watching everyone else teleport
            // outside while the VO narrated it — see this beat's CutsceneRecipe
            // (VisibleRecipe.keepScreenLit) and CutsceneDirector.PlayRoutine for
            // why the screen stays lit for this whole beat instead of fading to
            // black. Both Move and Look are gated for this scripted walk-out
            // (the player is watching the staged approach, not free-looking
            // through it) — Interact is left live throughout regardless
            // (SetMovementGated only touches Move/Look/Sprint) for the lift
            // interlude between this beat and TheCarry. Look is released again
            // once the camera is seeded onto the body, below.
            PlayerInputRouter input = FindPlayerInput();
            input?.SetMovementGated(true);

            // The door itself never physically opened before — DoorInteractable
            // played a creak and fired Opened, but nothing rotated the mesh,
            // so "out into the snow" played out with a still-shut door. Swing
            // it open across this same beat, alongside the cast walking
            // through it — including the player, who used to phase straight
            // through the wall several metres from the actual doorway.
            Vector3[] playerPath = { DoorwayCentre, DoorwayOutside, ChamferCorner, playerSpot };
            Vector3[] aaronPath = { DoorwayCentre, DoorwayOutside, ChamferCorner, nearBody };
            Vector3[] ivyPath = { DoorwayCentre, DoorwayOutside, ChamferCorner, nearBody + new Vector3(-0.6f, 0f, 0.2f) };
            Vector3[] priyaPath = { DoorwayCentre, DoorwayOutside, ChamferCorner, nearBody + new Vector3(0.4f, 0f, -0.6f) };

            // CutsceneRecipeBuilder gives OutIntoTheSnow three beats totalling
            // 7.5 s. The routed path (through the doorway, around the chamfer
            // corner) is roughly double the length of the old straight-line
            // shortcut, so everyone walks at a matched pace timed to land
            // inside that window rather than the old flat 3.2 s/2.2 m-per-s.
            const float playerTotalDuration = 7f;
            float playerPathLength = player != null ? PathLength(player.transform.position, playerPath) : 8f;
            float playerSpeed = Mathf.Max(0.1f, playerPathLength / playerTotalDuration);

            yield return RunTogether(
                SwingDoorOpen(),
                MovePlayerAlong(playerPath, playerSpeed),
                MoveActorAlong(aaron, aaronPath, SpeedFor(aaron, aaronPath)),
                MoveActorAlong(ivy, ivyPath, SpeedFor(ivy, ivyPath)),
                MoveActorAlong(priya, priyaPath, SpeedFor(priya, priyaPath)));

            // Aim only after arrival — seeding it up front (toward the exit
            // spot, before the walk) is what left the camera ~68 degrees off
            // the body once everyone was actually in place, which is what
            // silently killed the lift prompt below. SeedYaw resets pitch to
            // 0 (FreeLookCameraRig.SeedYaw), so pitch has to be seeded after.
            FreeLookCameraRig rig = FindPlayerRig();
            if (rig != null && player != null)
            {
                Vector3 toBody = bodyPos - player.transform.position;
                Vector3 flatToBody = new Vector3(toBody.x, 0f, toBody.z);
                float yaw = flatToBody.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(flatToBody.normalized, Vector3.up).eulerAngles.y
                    : rig.transform.eulerAngles.y;
                rig.SeedYaw(yaw);

                // standingPitchClampDegrees is 85 (InterrogationConfig.asset),
                // so the true look-down angle at playerSpot (~44 degrees for
                // the offset above) is comfortably inside the clamp.
                float horizontalDist = flatToBody.magnitude;
                const float eyeHeight = 1.64f;
                float pitch = horizontalDist > 0.01f
                    ? Mathf.Atan2(eyeHeight - bodyPos.y, horizontalDist) * Mathf.Rad2Deg
                    : 45f;
                rig.SeedPitch(pitch);
            }

            // Movement stays gated (the lift interlude needs the player to
            // stand still) but look is released so a bad aim is recoverable
            // by mouse alone instead of relying on the seed above being exact.
            input?.SetLookGated(false);
        }

        private static float SpeedFor(GameObject actor, Vector3[] path)
        {
            const float totalDuration = 7f;
            if (actor == null) return 2.2f;
            float length = PathLength(actor.transform.position, path);
            return Mathf.Max(0.1f, length / totalDuration);
        }

        private static float PathLength(Vector3 start, Vector3[] waypoints)
        {
            float length = 0f;
            Vector3 previous = start;
            foreach (Vector3 waypoint in waypoints)
            {
                length += Vector3.Distance(previous, waypoint);
                previous = waypoint;
            }
            return length;
        }

        private IEnumerator SwingDoorOpen()
        {
            GameObject door = GameObject.Find("Prop_FrontDoor_Locked");
            if (door == null) yield break;

            Quaternion open = Quaternion.Euler(0f, DoorOpenYawDegrees, 0f) * DoorClosedRotation;
            Quaternion start = door.transform.rotation;

            float t = 0f;
            const float duration = 1.2f;
            while (t < duration)
            {
                t += Time.deltaTime;
                door.transform.rotation = Quaternion.Slerp(start, open, t / duration);
                yield return null;
            }
            door.transform.rotation = open;
        }

        /// <summary>The M2 "help Aaron lift him" gameplay interlude — E to lift,
        /// once, between the OutIntoTheSnow and TheCarry cutscenes.
        /// Cutscene.M2MorningController calls this directly (not through
        /// CutsceneDirector — CutsceneBeat has no way to wait on input, only
        /// WaitForSeconds, so this cannot be a recipe beat) and continues its
        /// own chain from onComplete.</summary>
        public void RunLiftInterlude(System.Action onComplete)
        {
            StartCoroutine(LiftInterludeRoutine(onComplete));
        }

        private IEnumerator LiftInterludeRoutine(System.Action onComplete)
        {
            GameObject body = GameObject.Find("Prop_NickBody");
            GameObject player = GameObject.Find("Player (Male - First Person)");
            GameObject aaron = GameObject.Find("Aaron Teague (Male)");
            if (body == null || player == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            GameObject liftGo = new GameObject("LiftPrompt");
            liftGo.transform.SetParent(body.transform, false);
            BoxCollider bodyCollider = body.GetComponent<BoxCollider>();
            BoxCollider liftCollider = liftGo.AddComponent<BoxCollider>();
            if (bodyCollider != null)
            {
                liftCollider.center = bodyCollider.center;
                liftCollider.size = bodyCollider.size;
            }

            // The copy above makes this collider exactly coincident with
            // Prop_NickBody's own — Physics.Raycast would otherwise tie-break
            // arbitrarily between LiftPrompt and the body's InspectPoint
            // ("Look at the body"), which can softlock this wait forever if
            // the InspectPoint hasn't already been completed. Disabling the
            // body's collider for the interlude makes LiftPrompt the only
            // hittable target here, deterministically.
            if (bodyCollider != null) bodyCollider.enabled = false;

            LiftPrompt lift = liftGo.AddComponent<LiftPrompt>();
            lift.Configure("Help lift him", liftEffortClip);

            bool pressed = false;
            lift.Completed += _ => pressed = true;

            // A bare "while (!pressed)" has no escape hatch — if the player
            // is ever aimed away from the body (a bad SeedYaw/SeedPitch, an
            // edge case in the geometry above) this would hang forever with
            // movement gated and no way out for the player. 25 s is well
            // past any real attempt at pressing E; auto-complete instead of
            // trusting the aim to always be perfect.
            const float liftTimeoutSeconds = 25f;
            float elapsed = 0f;
            while (!pressed)
            {
                elapsed += Time.deltaTime;
                if (elapsed > liftTimeoutSeconds)
                {
                    Debug.LogWarning("[CutsceneStage] Lift interlude timed out after " +
                        liftTimeoutSeconds + "s without E — auto-completing.");
                    break;
                }
                yield return null;
            }

            // Left disabled here on purpose — the player carries Nick
            // themselves through TheCarry now (see that method and
            // RunCarryArrival), and a live collider on the body would fight
            // the player's CharacterController the whole way. Re-enabled by
            // RunCarryArrival once the carry actually ends.
            Object.Destroy(liftGo);

            if (ivyLiftLineClip != null)
            {
                GameObject ivy = GameObject.Find("Ivy Teague (Female)");
                Vector3 voPosition = ivy != null ? ivy.transform.position : body.transform.position;
                AudioSource.PlayClipAtPoint(ivyLiftLineClip, voPosition);
                GameFlowDirector.Instance?.Subtitles?.Show("IVY", "Careful… careful. Easy.", ivyLiftLineClip.length);
            }

            CabinAnimatorDriver playerDriver = player.GetComponent<CabinAnimatorDriver>();
            CabinAnimatorDriver aaronDriver = aaron != null ? aaron.GetComponent<CabinAnimatorDriver>() : null;
            playerDriver?.PlayState("Lift_Crouch", 0.15f);
            aaronDriver?.PlayState("Lift_Crouch", 0.15f);

            Transform view = FindPlayerView();
            Vector3 viewStart = view != null ? view.localPosition : Vector3.zero;
            Vector3 viewLow = viewStart + new Vector3(0f, -0.4f, 0f);

            const float dipDuration = 0.6f;
            float t = 0f;
            while (t < dipDuration)
            {
                t += Time.deltaTime;
                if (view != null) view.localPosition = Vector3.Lerp(viewStart, viewLow, t / dipDuration);
                yield return null;
            }

            t = 0f;
            while (t < dipDuration)
            {
                t += Time.deltaTime;
                if (view != null) view.localPosition = Vector3.Lerp(viewLow, viewStart, t / dipDuration);
                yield return null;
            }
            if (view != null) view.localPosition = viewStart;

            playerDriver?.PlayProfile(CabinIdleProfile.Carrying);

            onComplete?.Invoke();
        }

        /// <summary>
        /// Used to be a scripted 4 s auto-walk back to the sofa (straight
        /// through the wall, same as the old OutIntoTheSnow). Per the user's
        /// call, the player now carries Nick back inside themselves: this
        /// just hands control back and starts the body/Aaron follow
        /// coroutines, then returns immediately — the actual "carry" plays
        /// out over however long the player takes to walk back, tracked by
        /// RunCarryArrival (called from M2MorningController once this
        /// cutscene's dialogue beats finish), not by this method's own
        /// duration.
        /// </summary>
        private IEnumerator TheCarry()
        {
            GameObject aaron = GameObject.Find("Aaron Teague (Male)");
            GameObject body = GameObject.Find("Prop_NickBody");
            GameObject player = GameObject.Find("Player (Male - First Person)");
            CabinAnimatorDriver playerDriver = player != null ? player.GetComponent<CabinAnimatorDriver>() : null;

            FindPlayerInput()?.SetMovementGated(false);

            FreeLookCameraRig rig = FindPlayerRig();
            if (rig != null) rig.SeedPitch(0f);

            playerDriver?.PlayProfile(CabinIdleProfile.Carrying);

            if (body != null && player != null)
            {
                _bodyFollowRoutine = StartCoroutine(FollowBody(body.transform, player.transform));
            }
            if (aaron != null && player != null)
            {
                _aaronFollowRoutine = StartCoroutine(FollowAaron(aaron, player.transform));
            }

            yield break;
        }

        /// <summary>Pins Prop_NickBody in front of the player every frame
        /// while the carry is in progress, so it reads as carried rather
        /// than dragged. The 0.7/0.95 offsets are a first pass, not tuned —
        /// a 1.75 m-wide body 0.7 m in front of a 1.64 m-high camera can
        /// fill most of the screen; adjust by eye on first playtest.</summary>
        private IEnumerator FollowBody(Transform body, Transform player)
        {
            const float forwardOffset = 0.7f;
            const float heightOffset = 0.95f;

            while (true)
            {
                Vector3 flatForward = new Vector3(player.forward.x, 0f, player.forward.z);
                if (flatForward.sqrMagnitude > 0.0001f) flatForward.Normalize();

                body.position = player.position + flatForward * forwardOffset + Vector3.up * heightOffset;
                // Carried across the arms rather than facing the direction
                // of travel — laid out perpendicular to the player's facing.
                body.rotation = Quaternion.LookRotation(player.right, Vector3.up);

                yield return null;
            }
        }

        /// <summary>Keeps Aaron at the player's side during the carry, with a
        /// dead zone so he doesn't jitter in place when the player stops.</summary>
        private IEnumerator FollowAaron(GameObject aaronGo, Transform player)
        {
            CabinAnimatorDriver aaronDriver = aaronGo.GetComponent<CabinAnimatorDriver>();
            aaronDriver?.PlayState("Walk_Carry", 0.2f);

            const float sideOffset = 1.1f;
            const float followSpeed = 3.5f;
            const float deadZone = 0.15f;
            Transform aaron = aaronGo.transform;

            while (true)
            {
                Vector3 target = player.position + player.right * sideOffset;
                if (Vector3.Distance(aaron.position, target) > deadZone)
                {
                    aaron.position = Vector3.MoveTowards(aaron.position, target, followSpeed * Time.deltaTime);
                    Vector3 flatFacing = new Vector3(player.forward.x, 0f, player.forward.z);
                    if (flatFacing.sqrMagnitude > 0.0001f)
                    {
                        aaron.rotation = Quaternion.LookRotation(flatFacing.normalized, Vector3.up);
                    }
                }
                yield return null;
            }
        }

        /// <summary>Waits for the player to actually walk the body back to
        /// the sofa before letting M2MorningController continue into
        /// TheSofa — decoupled from TheCarry's own VO/beat timer (which
        /// fires on a fixed clock regardless of how long the player takes
        /// to walk). Times out and auto-completes rather than risking a
        /// second permanent freeze if the player somehow gets stuck.</summary>
        public void RunCarryArrival(System.Action onArrive)
        {
            StartCoroutine(CarryArrivalRoutine(onArrive));
        }

        private IEnumerator CarryArrivalRoutine(System.Action onArrive)
        {
            GameObject player = GameObject.Find("Player (Male - First Person)");
            const float arriveRadius = 1.2f;
            const float timeoutSeconds = 40f;
            float elapsed = 0f;

            if (player != null)
            {
                Vector3 playerRest = SofaPoint(SofaPlayerRestLocal);
                Vector3 flatTarget = new Vector3(playerRest.x, 0f, playerRest.z);
                while (true)
                {
                    Vector3 flatPlayer = new Vector3(player.transform.position.x, 0f, player.transform.position.z);
                    if (Vector3.Distance(flatPlayer, flatTarget) <= arriveRadius) break;

                    elapsed += Time.deltaTime;
                    if (elapsed > timeoutSeconds)
                    {
                        Debug.LogWarning("[CutsceneStage] Carry arrival timed out after " +
                            timeoutSeconds + "s — auto-completing.");
                        break;
                    }
                    yield return null;
                }
            }

            if (_bodyFollowRoutine != null) { StopCoroutine(_bodyFollowRoutine); _bodyFollowRoutine = null; }
            if (_aaronFollowRoutine != null) { StopCoroutine(_aaronFollowRoutine); _aaronFollowRoutine = null; }

            // Deliberately NOT re-gating movement here. TheSofa is reached
            // through GameFlowDirector.RequestCutscene, which degrades to a
            // screen-fader blink (CutsceneDirector unregistered) rather than
            // ever calling back into this class if something upstream is
            // wrong — gating here and relying on TheSofa's own release at
            // the end (":632") to undo it would reintroduce exactly the kind
            // of permanent freeze this whole pass exists to remove, just one
            // step later. A few seconds of free movement during the settle
            // beat is a cosmetic risk; a silent second softlock is not.
            GameObject aaronGo = GameObject.Find("Aaron Teague (Male)");
            if (aaronGo != null) yield return SettleAaronOntoSofa(aaronGo.transform, aaronGo.GetComponent<CabinAnimatorDriver>());

            GameObject body = GameObject.Find("Prop_NickBody");
            BoxCollider bodyCollider = body != null ? body.GetComponent<BoxCollider>() : null;
            if (bodyCollider != null) bodyCollider.enabled = true;

            onArrive?.Invoke();
        }

        /// <summary>FollowAaron only ever chased the player's side — nothing
        /// previously walked him the rest of the way onto SofaAaronRest once
        /// the carry stopped, which left him standing wherever the player
        /// happened to end the walk instead of settled by the sofa. Short
        /// lerp so it doesn't read as a snap. Keeps Walk_Carry playing for
        /// its own duration and is awaited (not fire-and-forget) BEFORE
        /// onArrive fires TheSofa — TheSofa immediately cross-fades Aaron to
        /// a standing Controlled pose, and if that landed mid-lerp he'd
        /// visibly ice-skate across the floor while idling.</summary>
        private IEnumerator SettleAaronOntoSofa(Transform aaron, CabinAnimatorDriver driver)
        {
            driver?.PlayState("Walk_Carry", 0.1f);

            Vector3 start = aaron.position;
            Vector3 aaronRest = SofaPoint(SofaAaronRestLocal);
            const float duration = 0.8f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                aaron.position = Vector3.Lerp(start, aaronRest, t / duration);
                yield return null;
            }
            aaron.position = aaronRest;
        }

        private IEnumerator TheSofa()
        {
            GameObject priya = GameObject.Find("Priya Raman (Female)");
            GameObject aaron = GameObject.Find("Aaron Teague (Male)");
            GameObject body = GameObject.Find("Prop_NickBody");
            GameObject player = GameObject.Find("Player (Male - First Person)");

            Vector3 bodyRest = SofaPoint(SofaBodyRestLocal);
            Vector3 bodyStart = body != null ? body.transform.position : bodyRest;
            const float duration = 1.2f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (body != null) body.transform.position = Vector3.Lerp(bodyStart, bodyRest, t / duration);
                yield return null;
            }
            if (body != null) body.transform.position = bodyRest;

            ScriptedActor priyaActor = priya != null ? priya.GetComponent<ScriptedActor>() : null;
            priyaActor?.PlayPose(CabinIdleProfile.Kneeling);

            CabinAnimatorDriver aaronDriver = aaron != null ? aaron.GetComponent<CabinAnimatorDriver>() : null;
            aaronDriver?.PlayProfile(CabinIdleProfile.Controlled);

            CabinAnimatorDriver playerDriver = player != null ? player.GetComponent<CabinAnimatorDriver>() : null;
            playerDriver?.PlayProfile(CabinIdleProfile.Controlled);

            // The whole M2 carry sequence — door, walk-out, lift, carry-in —
            // kept player movement gated (OutIntoTheSnow) so they couldn't
            // wander off mid-cutscene. This is the last beat, so restore it.
            FindPlayerInput()?.SetMovementGated(false);
        }

        // ---- helpers ----

        // --- P3 memory pair (CS-16A / CS-16B) --------------------------------

        // M1_Night deliberately leaves Nick and the Teagues disabled
        // (CabinNightCharacterBuilder: Nick is already outside, Aaron/Ivy are
        // upstairs behind the blocked stairs) — and that has to stay true, or
        // STORY_SCRIPT.md §7's "who went through the door?" trap collapses:
        // the player could just look around the room and see who is still in
        // it. So these two flashbacks borrow the cast rather than owning it.
        // Every actor they switch on is switched back off, and every transform
        // they move is put back, before the routine returns.
        private GameObject FindCastMember(string name)
        {
            // GameObject.Find skips inactive objects; Transform.Find does not,
            // so the lookup goes through the always-active Characters root.
            GameObject root = GameObject.Find("Characters");
            if (root == null) return null;
            Transform found = root.transform.Find(name);
            return found != null ? found.gameObject : null;
        }

        private readonly List<BorrowedActor> _borrowed = new List<BorrowedActor>();

        private readonly struct BorrowedActor
        {
            public readonly GameObject Go;
            public readonly bool WasActive;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly CabinIdleProfile Pose;
            public readonly bool WantsPose;

            public BorrowedActor(GameObject go, bool wasActive, Vector3 position, Quaternion rotation,
                CabinIdleProfile pose = default, bool wantsPose = false)
            {
                Go = go;
                WasActive = wasActive;
                Position = position;
                Rotation = rotation;
                Pose = pose;
                WantsPose = wantsPose;
            }
        }

        private GameObject Borrow(string name, Vector3 position, float yaw, CabinIdleProfile pose)
        {
            GameObject go = FindCastMember(name);
            if (go == null) return null;
            _borrowed.Add(new BorrowedActor(go, go.activeSelf, go.transform.position, go.transform.rotation, pose, true));
            go.SetActive(true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            // Pose is applied by PoseBorrowed() a frame later, NOT here. These
            // actors are disabled at scene load, so SetActive(true) is their
            // first activation: Awake has only just run, Start has not, and the
            // Animator is not initialised yet. Cross-fading a clip into that
            // window drops the hips to the root — the clips are muscle-only
            // with zero root curves by design (CabinAnimationBuilder), so
            // nothing puts the body back up — and the mesh sinks about hip
            // height into the floor while the collider stays correct.
            return go;
        }

        /// <summary>Applies the poses recorded by Borrow(), one frame after
        /// activation so each actor's Animator has initialised. Yield on this,
        /// do not call it directly.</summary>
        private IEnumerator PoseBorrowed()
        {
            yield return null;
            foreach (BorrowedActor b in _borrowed)
            {
                if (!b.WantsPose || b.Go == null || !b.Go.activeSelf) continue;
                ScriptedActor actor = b.Go.GetComponent<ScriptedActor>();
                if (actor != null) actor.PlayPose(b.Pose);
            }

            // Let the Animator evaluate the pose it was just handed before the
            // feet are measured against it. CabinAnimatorDriver.PlayProfile
            // applies the hip-height offset instantly but crossfades the
            // muscle pose over 0.25s (CrossFadeInFixedTime) — measuring feet
            // one frame later catches a transitional pose (hips already
            // dropped, legs still in the old shape) and bakes in a wrong
            // one-time correction that nothing re-runs once the blend
            // finishes. Wait past the fade, not one frame.
            yield return new WaitForSeconds(0.3f);

            foreach (BorrowedActor b in _borrowed)
            {
                if (!b.WantsPose || b.Go == null || !b.Go.activeSelf) continue;
                PlantFeet(b.Go);
            }
        }

        /// <summary>Drops the character so its feet rest on the floor.
        ///
        /// The baked clips are muscle-only with zero root curves
        /// (CabinAnimationBuilder authors none, because root translation is not
        /// scale-safe at the cast's 0.96-1.02 scales). Muscle values carry pose
        /// but not hip height, and CabinAnimatorDriver only ever applies a
        /// NEGATIVE body offset (Kneeling -0.28, Sleeping -0.12) — there is no
        /// positive standing baseline anywhere. Measured live during CS-16A:
        /// every borrowed actor sat with its Hips bone at y = -0.08 against a
        /// player rig at +0.92, i.e. a full hip-height below the floor, while
        /// the CapsuleCollider stayed correctly at 0.
        ///
        /// Uses the humanoid foot bones, NOT renderer bounds. SkinnedMeshRenderer
        /// bounds are a conservative box that does not hug the animated pose:
        /// measuring those over-lifted the whole cast by ~0.35m and left them
        /// visibly hovering with their feet at y = 0.40.</summary>
        private static void PlantFeet(GameObject go)
        {
            Animator animator = go.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;

            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null && rightFoot == null) return;

            float lowest = leftFoot == null ? rightFoot.position.y
                : rightFoot == null ? leftFoot.position.y
                : Mathf.Min(leftFoot.position.y, rightFoot.position.y);

            float scale = Mathf.Approximately(go.transform.lossyScale.y, 0f) ? 1f : go.transform.lossyScale.y;
            // The foot bone is the ankle, not the sole — leave a boot's worth
            // of clearance under it or the character sinks to the shins.
            const float SoleToAnkle = 0.09f;
            float target = go.transform.position.y + SoleToAnkle * scale;

            float lift = target - lowest;
            if (Mathf.Abs(lift) < 0.005f) return;

            Transform body = animator.transform;
            body.localPosition += new Vector3(0f, lift / scale, 0f);
        }

        private void ReturnBorrowed()
        {
            for (int i = _borrowed.Count - 1; i >= 0; i--)
            {
                BorrowedActor b = _borrowed[i];
                if (b.Go == null) continue;
                b.Go.transform.SetPositionAndRotation(b.Position, b.Rotation);
                b.Go.SetActive(b.WasActive);
            }
            _borrowed.Clear();
        }

        /// <summary>Yaw that makes an actor standing at <paramref name="from"/>
        /// face <paramref name="target"/>, flattened to the floor plane.</summary>
        private static float YawToward(Vector3 from, Vector3 target)
        {
            Vector3 flat = new Vector3(target.x - from.x, 0f, target.z - from.z);
            return flat.sqrMagnitude < 0.0001f
                ? 0f
                : Quaternion.LookRotation(flat.normalized, Vector3.up).eulerAngles.y;
        }

        // Chair assignments for the P3 memory pair. SM_Chair_05 is David's —
        // CabinV2Builder derived the Blender->Unity axis mapping from it, and
        // it sits exactly where M1_Night spawns the player, so the memories
        // reuse the seat the player already occupies.
        private const string ChairDavid = "SM_Chair_05"; // (-3.00, 0.85)
        private const string ChairNick = "SM_Chair_01";  // (-1.85, 1.60) beside David
        private const string ChairIvy2 = "SM_Chair_02";  // (-1.85, 3.00) same side as Nick
        private const string ChairAaron = "SM_Chair_03"; // (-4.15, 1.60) opposite Nick
        private const string ChairIvy = "SM_Chair_04";   // (-4.15, 3.00) beside Aaron
        private const string ChairPriya = "SM_Chair_06"; // (-3.00, 3.75) facing David

        /// <summary>Standing spot for an actor at a chair: the chair's floor
        /// position pushed away from the table so the body clears both the chair
        /// and the table mesh.
        ///
        /// The first pass placed everyone on table-relative offsets and several
        /// landed inside SM_Chair_01/03/04 — the chairs ring the table at ±1.15
        /// in x and 1.60/3.00 in z, which those offsets walked straight into. On
        /// screen the actors' legs simply vanished into the furniture, which read
        /// as them being sunk into the floor.</summary>
        private static Vector3 StandingAtChair(string chairName, Vector3 table, float clearance = 0.5f)
        {
            GameObject chair = GameObject.Find(chairName);
            if (chair == null) return table;
            Vector3 seat = new Vector3(chair.transform.position.x, 0f, chair.transform.position.z);
            Vector3 away = seat - table;
            away.y = 0f;
            return away.sqrMagnitude < 0.0001f ? seat : seat + away.normalized * clearance;
        }

        /// <summary>Seat position for an actor at a chair, tucked toward the
        /// table. Contrast StandingAtChair, which pushes 0.5 m AWAY so a
        /// standing body clears the furniture — a seated body belongs in the
        /// chair, so that clearance must not be applied. The drop onto the seat
        /// pad is the Seated profile's job (CabinPoseLibrary /
        /// CabinAnimatorDriver.Editor_BodyYOffsetFor); the root Transform stays
        /// on the floor exactly as it does for every other pose.
        ///
        /// The tuck closes the last few centimetres between the chair centre
        /// and the table so hands land ON the surface: a seated character
        /// reaches at most 0.249 m forward of the hip at table height (see
        /// CabinPoseLibrary's SeatedForward note — that is the maximum over the
        /// whole arm-muscle space, not a tuning choice), and the chair centres
        /// sit 0.25-0.38 m out from the table's edge depending on which chair.
        ///
        /// MUST stay under the seat's support distance along the facing
        /// direction, measured per chair as 0.229 (SM_Chair_06, the tightest)
        /// to 0.330. A previous value of 0.42 was set from a bad reading of the
        /// chair-to-table gap — taken along the x axis alone, which inflates it
        /// to 0.70 for the diagonal chairs — and it pushed every actor clean off
        /// the front of the seat. 0.18 clears the tightest chair with room and
        /// still puts the hands 0.05-0.18 m onto the table.</summary>
        private const float SeatTuck = 0.18f;

        /// <summary>The seat transforms are still SM_Chair_01..06 even though
        /// CabinV2Builder's furniture swap now hides them behind real stool
        /// models: the swap SetActive(false)s each original and drops
        /// Prop_Chair_0X at the SAME captured world position, so the authored
        /// seating layout lives on the originals and is what this reads.
        ///
        /// The lookup therefore goes through Cabin_v2's transform, NOT
        /// GameObject.Find — Find skips inactive objects, so once the swap ran
        /// it returned null for all six chairs and every actor fell back to
        /// `table`, stacking the whole cast on the table centre. Same reason
        /// FindCastMember goes through the Characters root.</summary>
        private static Vector3 SeatedAtChair(string chairName, Vector3 table)
        {
            GameObject cabin = GameObject.Find("Cabin_v2");
            Transform chair = cabin != null ? cabin.transform.Find(chairName) : null;
            if (chair == null) return table;

            Vector3 seat = new Vector3(chair.position.x, 0f, chair.position.z);
            Vector3 toTable = table - seat;
            toTable.y = 0f;
            if (toTable.sqrMagnitude < 0.0001f) return seat;
            return seat + toTable.normalized * SeatTuck;
        }

        /// <summary>Borrows an actor only to take it off screen, recording its
        /// state so ReturnBorrowed puts it back. Priya is active in M1_Night,
        /// asleep on the sofa and in shot — she has to go for CS-16B's private
        /// argument between David and Nick.</summary>
        private GameObject BorrowHidden(string name)
        {
            GameObject go = FindCastMember(name);
            if (go == null) return null;
            _borrowed.Add(new BorrowedActor(go, go.activeSelf, go.transform.position, go.transform.rotation));
            go.SetActive(false);
            return go;
        }

        /// <summary>Swings the front door open or shut. Records the door's
        /// rotation on first use so ReturnBorrowed restores it — M1_Night needs
        /// the door shut, and a flashback must not leave it hanging open.</summary>
        private IEnumerator SwingFrontDoor(bool open, float duration)
        {
            GameObject door = GameObject.Find("Prop_FrontDoor_Locked");
            if (door == null) yield break;

            bool alreadyBorrowed = false;
            foreach (BorrowedActor b in _borrowed)
            {
                if (b.Go == door) { alreadyBorrowed = true; break; }
            }
            if (!alreadyBorrowed)
            {
                _borrowed.Add(new BorrowedActor(door, door.activeSelf, door.transform.position, door.transform.rotation));
            }

            Quaternion openRotation = Quaternion.Euler(0f, DoorOpenYawDegrees, 0f) * DoorClosedRotation;
            Quaternion from = open ? DoorClosedRotation : openRotation;
            Quaternion to = open ? openRotation : DoorClosedRotation;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                door.transform.rotation = Quaternion.Slerp(from, to, t / duration);
                yield return null;
            }
            door.transform.rotation = to;
        }

        /// <summary>CS-16A — the good years, ~13 s. Everyone is placed at their
        /// own chair around the table, facing in, with the player already in
        /// SM_Chair_05. Warm and undistorted on purpose: this is the version of
        /// the friend group CS-16B destroys, so it has to read as ordinary
        /// first.</summary>
        private IEnumerator GoodYears()
        {
            Vector3 table = TableCentre();
            Vector3 david = SeatedAtChair(ChairDavid, table);

            Vector3 nickAt = SeatedAtChair(ChairNick, table);
            Vector3 priyaAt = SeatedAtChair(ChairPriya, table);
            Vector3 aaronAt = SeatedAtChair(ChairAaron, table);
            Vector3 ivyAt = SeatedAtChair(ChairIvy, table);

            // Everyone leans in over the table. The idle seeds are already
            // per-character (CabinNightCharacterBuilder hashes the name), so a
            // shared profile still breathes and drifts out of phase rather than
            // reading as four copies of one pose.
            GameObject nick = Borrow("Nick Vlahos (Male)", nickAt, YawToward(nickAt, david), CabinIdleProfile.SeatedForward);
            GameObject aaron = Borrow("Aaron Teague (Male)", aaronAt, YawToward(aaronAt, table), CabinIdleProfile.SeatedForward);
            GameObject ivy = Borrow("Ivy Teague (Female)", ivyAt, YawToward(ivyAt, table), CabinIdleProfile.SeatedForward);
            // Priya is already active, asleep on the sofa — Borrow records that
            // so she is returned to it, rather than left standing at the table.
            GameObject priya = Borrow("Priya Raman (Female)", priyaAt, YawToward(priyaAt, david), CabinIdleProfile.SeatedForward);

            yield return PoseBorrowed();

            // 0-4s — old friends. Priya holds up the school photograph of David
            // and Nick. Parented to her hand rather than placed in the air, so
            // it tracks her breathing idle instead of hanging beside her.
            GameObject schoolPhoto = PhotoProps.PutInHand(priya, david, "photo_school_david_nick");
            yield return new WaitForSeconds(3.95f);

            // 4-8s — Aaron and Ivy. Ivy turns to Aaron, then the half-second
            // where Nick and Ivy look at each other instead of at him. Held
            // under half a second: catchable, not certain.
            // 4-8s — Priya swipes to the wedding photo on her phone. Smaller
            // than a print because it is a phone screen, per §4.
            PhotoProps.Discard(schoolPhoto);
            GameObject weddingPhoto = PhotoProps.PutInHand(priya, david, "photo_aaron_ivy_wedding", 0.075f, 0.055f);

            if (ivy != null) ivy.transform.rotation = Quaternion.Euler(0f, YawToward(ivyAt, aaronAt), 0f);
            yield return new WaitForSeconds(3.2f);
            if (nick != null) nick.transform.rotation = Quaternion.Euler(0f, YawToward(nickAt, ivyAt), 0f);
            if (ivy != null) ivy.transform.rotation = Quaternion.Euler(0f, YawToward(ivyAt, nickAt), 0f);
            yield return new WaitForSeconds(0.45f);
            if (nick != null) nick.transform.rotation = Quaternion.Euler(0f, YawToward(nickAt, table), 0f);
            if (ivy != null) ivy.transform.rotation = Quaternion.Euler(0f, YawToward(ivyAt, aaronAt), 0f);
            yield return new WaitForSeconds(0.35f);

            // 8-11s — the toast. Everyone turns in to the middle of the table.
            TurnAllToward(table, nick, aaron, ivy, priya);
            yield return new WaitForSeconds(3f);

            // 11-13s — the coat swap. Nick turns to David and throws the parka.
            if (nick != null) nick.transform.rotation = Quaternion.Euler(0f, YawToward(nickAt, david), 0f);
            yield return new WaitForSeconds(2f);

            // The photographs belong to this memory only — they must not be left
            // in the cabin for the player to find during M1.
            PhotoProps.Discard(weddingPhoto);
            ReturnBorrowed();
        }

        /// <summary>CS-16B — when it went wrong, ~13 s. Same room and cast as
        /// CS-16A so the difference is carried by blocking and performance, per
        /// §5's memory-pair rule. Two staged moments separated by a hard blink:
        /// the table with everyone still present, then David and Nick alone at
        /// the fire. Ends with Nick out through the front door and the scene
        /// handed back exactly as M1_Night left it.</summary>
        private IEnumerator WhenItWentWrong()
        {
            Vector3 table = TableCentre();
            Vector3 fire = FireplaceCentre(table);
            Vector3 david = SeatedAtChair(ChairDavid, table);
            ScreenFader fader = FindAnyObjectByType<ScreenFader>();

            // 0-5s — Aaron learns. Nick and Ivy are on the same side of the
            // table, one chair apart, which is as close as the seating allows.
            // Aaron is opposite and near enough to hear. He never moves: the
            // stillness is the whole performance.
            Vector3 nickAt = SeatedAtChair(ChairNick, table);
            Vector3 ivyAt = SeatedAtChair(ChairIvy2, table);
            Vector3 aaronAt = SeatedAtChair(ChairAaron, table);

            // All three leaning in — the tension here is carried by who is
            // facing whom (YawToward above) rather than by posture.
            GameObject nick = Borrow("Nick Vlahos (Male)", nickAt, YawToward(nickAt, ivyAt), CabinIdleProfile.SeatedForward);
            GameObject ivy = Borrow("Ivy Teague (Female)", ivyAt, YawToward(ivyAt, nickAt), CabinIdleProfile.SeatedForward);
            GameObject aaron = Borrow("Aaron Teague (Male)", aaronAt, YawToward(aaronAt, nickAt), CabinIdleProfile.SeatedForward);
            // The argument is private. Priya asleep on the sofa is still in
            // shot otherwise.
            BorrowHidden("Priya Raman (Female)");

            yield return PoseBorrowed();

            yield return new WaitForSeconds(2.55f);
            // Ivy freezes: she looks at Nick, then at Aaron.
            if (ivy != null) ivy.transform.rotation = Quaternion.Euler(0f, YawToward(ivyAt, aaronAt), 0f);
            yield return new WaitForSeconds(2.4f);

            // Hard blink into the second moment. §4 jumps forward without
            // anyone leaving the room, so the room has to change under a cut
            // rather than have Ivy and Aaron walk out in front of the player.
            if (fader != null) yield return fader.FadeToBlack(0.12f);

            if (ivy != null) ivy.SetActive(false);
            if (aaron != null) aaron.SetActive(false);

            Vector3 nickFire = Vector3.Lerp(fire, david, 0.35f);
            nickFire.y = 0f;
            if (nick != null)
            {
                nick.transform.SetPositionAndRotation(nickFire, Quaternion.Euler(0f, YawToward(nickFire, david), 0f));
                // Nick was Seated at the table; the fire argument is standing.
                // Without this he'd keep the Seated clip (and its hip-drop
                // offset) at a standing floor position — reads as floating/
                // crouched rather than on his feet by the fire.
                nick.GetComponent<ScriptedActor>()?.PlayPose(CabinIdleProfile.Confrontational);
            }

            // Wait for the Confrontational crossfade to finish before feet
            // are measured against it — same fix and same reason as
            // PoseBorrowed's wait (see its doc): CabinAnimatorDriver.
            // ApplyBodyYOffset resets Body.localPosition to the Seated-less
            // rest position INSTANTLY, but the leg muscles take 0.25s
            // (CrossFadeInFixedTime) to blend out of the Seated bend. One
            // frame catches Nick mid-blend — hips already up, legs still
            // bent — and PlantFeet would bake in a correction for that
            // transitional pose instead of the settled standing one.
            yield return new WaitForSeconds(0.3f);
            if (nick != null) PlantFeet(nick);
            if (fader != null) yield return fader.FadeFromBlack(0.12f);

            // 5-11s — the argument, David and Nick alone at the fire.
            yield return new WaitForSeconds(3.4f);

            // 11-13s — Nick goes outside, still in David's thin jacket. The
            // door is opened rather than walked through, then shut behind him.
            // Routed via the doorway waypoints: the front door sits in the
            // chamfered corner and a direct line crosses solid wall.
            yield return SwingFrontDoor(open: true, duration: 0.5f);
            yield return MoveActorAlong(nick, new[] { DoorwayCentre, DoorwayOutside }, 2.6f);
            yield return SwingFrontDoor(open: false, duration: 0.4f);

            ReturnBorrowed();
        }

        private static void TurnAllToward(Vector3 target, params GameObject[] actors)
        {
            foreach (GameObject go in actors)
            {
                if (go == null) continue;
                go.transform.rotation = Quaternion.Euler(0f, YawToward(go.transform.position, target), 0f);
            }
        }

        /// <summary>Floor-plane centre of the table, taken from Prop_FiveCups so
        /// re-dressing the scene moves the staging with it.</summary>
        private static Vector3 TableCentre()
        {
            GameObject cups = GameObject.Find("Prop_FiveCups");
            return cups != null
                ? new Vector3(cups.transform.position.x, 0f, cups.transform.position.z)
                : new Vector3(-3f, 0f, 2.15f);
        }

        /// <summary>Floor-plane centre of the fireplace, taken from the mantel
        /// clock for the same reason as TableCentre.</summary>
        private static Vector3 FireplaceCentre(Vector3 tableFallback)
        {
            GameObject clock = GameObject.Find("Prop_MantelClock");
            return clock != null
                ? new Vector3(clock.transform.position.x, 0f, clock.transform.position.z)
                : tableFallback + new Vector3(2.5f, 0f, 0f);
        }

        private IEnumerator MoveActor(GameObject go, Vector3 destination, float speed, CabinIdleProfile walkProfile = CabinIdleProfile.Walking)
        {
            if (go == null) yield break;
            ScriptedActor actor = go.GetComponent<ScriptedActor>();
            if (actor == null) yield break;
            yield return actor.MoveTo(destination, speed, walkProfile);
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

        private PlayerInputRouter FindPlayerInput()
        {
            GameObject player = GameObject.Find("Player (Male - First Person)");
            return player != null ? player.GetComponent<PlayerInputRouter>() : null;
        }

        /// <summary>Walks the player through a bent path — e.g. through a
        /// doorway rather than straight through a wall (see OutIntoTheSnow) —
        /// at a constant speed. CharacterController must be disabled for the
        /// whole multi-leg move or its own collision resolution fights a
        /// direct transform.position write every frame.</summary>
        private IEnumerator MovePlayerAlong(Vector3[] waypoints, float speed)
        {
            GameObject player = GameObject.Find("Player (Male - First Person)");
            if (player == null || waypoints == null || waypoints.Length == 0) yield break;

            CharacterController controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;

            foreach (Vector3 waypoint in waypoints)
            {
                while (Vector3.Distance(player.transform.position, waypoint) > 0.05f)
                {
                    player.transform.position = Vector3.MoveTowards(player.transform.position, waypoint, speed * Time.deltaTime);
                    yield return null;
                }
                player.transform.position = waypoint;
            }

            if (controller != null) controller.enabled = wasEnabled;
        }

        /// <summary>Walks an NPC through a bent path as one continuous walk
        /// cycle — see ScriptedActor.MoveAlong for why this isn't just
        /// MoveTo called once per waypoint.</summary>
        private IEnumerator MoveActorAlong(GameObject go, Vector3[] waypoints, float speed, CabinIdleProfile walkProfile = CabinIdleProfile.Walking)
        {
            if (go == null) yield break;
            ScriptedActor actor = go.GetComponent<ScriptedActor>();
            if (actor == null) yield break;
            yield return actor.MoveAlong(waypoints, speed, walkProfile);
        }
    }
}
