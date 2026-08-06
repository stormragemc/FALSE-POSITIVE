using UnityEngine;

namespace FalsePositive.Interaction
{
    /// <summary>
    /// The M2 "help Aaron lift him" gameplay interlude — E to lift, once,
    /// between the OutIntoTheSnow and TheCarry cutscenes
    /// (Cutscene.M2MorningController owns the sequencing). Added to a
    /// runtime-only child of Prop_NickBody for the duration of the interlude
    /// and destroyed immediately after.
    ///
    /// Parenting alone does NOT keep this from contending with the
    /// InspectPoint already on Prop_NickBody ("Look at the body",
    /// MemorySceneDressing) — CutsceneStage.LiftInterludeRoutine copies
    /// Prop_NickBody's own BoxCollider center/size onto this object's
    /// collider, so the two colliders are exactly coincident and
    /// Physics.Raycast tie-breaks between them arbitrarily. If the body's
    /// InspectPoint hasn't been completed yet (player never looked at the
    /// body through the window), E can resolve to it instead of this prompt
    /// and the interlude's "while (!pressed)" wait never exits. The routine
    /// disables Prop_NickBody's BoxCollider for the duration of the
    /// interlude to make this deterministic — see its comment.
    /// </summary>
    public sealed class LiftPrompt : Interactable
    {
        private AudioClip _liftClip;

        public void Configure(string prompt, AudioClip liftClip)
        {
            lookPrompt = prompt;
            _liftClip = liftClip;
        }

        public override void OnInteract()
        {
            if (_liftClip != null) AudioSource.PlayClipAtPoint(_liftClip, transform.position);
            MarkComplete();
        }
    }
}
