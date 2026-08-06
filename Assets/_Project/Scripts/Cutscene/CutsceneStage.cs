using System.Collections;
using FalsePositive.CabinNight;
using FalsePositive.Flow;
using FalsePositive.Interaction;
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
        // z in [-1.5, 2.0]. The sofa faces -X (Cabin_v2 README: Blender +X ->
        // Unity -X), so its open/approach side is x < 0.25. These rest spots
        // sit just off that face — NOT the old (0.75, ·, ·) values, which were
        // inside the sofa's own box and would have left the player standing
        // (and previously the CharacterController re-enabling) INSIDE it.
        private static readonly Vector3 SofaPlayerRest = new Vector3(-0.4f, 0f, 0.3f);
        private static readonly Vector3 SofaAaronRest = new Vector3(-0.4f, 0f, -0.5f);
        private static readonly Vector3 SofaBodyCarriedHeight = new Vector3(0.75f, 0.9f, 0.45f);
        private static readonly Vector3 SofaBodyRest = new Vector3(0.75f, 0.85f, 0.4f);

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
                Vector3 flatTarget = new Vector3(SofaPlayerRest.x, 0f, SofaPlayerRest.z);
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
            const float duration = 0.8f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                aaron.position = Vector3.Lerp(start, SofaAaronRest, t / duration);
                yield return null;
            }
            aaron.position = SofaAaronRest;
        }

        private IEnumerator TheSofa()
        {
            GameObject priya = GameObject.Find("Priya Raman (Female)");
            GameObject aaron = GameObject.Find("Aaron Teague (Male)");
            GameObject body = GameObject.Find("Prop_NickBody");
            GameObject player = GameObject.Find("Player (Male - First Person)");

            Vector3 bodyStart = body != null ? body.transform.position : SofaBodyRest;
            const float duration = 1.2f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (body != null) body.transform.position = Vector3.Lerp(bodyStart, SofaBodyRest, t / duration);
                yield return null;
            }
            if (body != null) body.transform.position = SofaBodyRest;

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
