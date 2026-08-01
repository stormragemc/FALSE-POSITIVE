using UnityEngine;

namespace FalsePositive.Player
{
    /// <summary>
    /// Marks one chair's fixed seated-camera pose, the pose the player is
    /// teleported to when standing back up, and the trigger point/radius for
    /// the "walk up and press E" interaction. Authored in-scene, not code.
    /// </summary>
    public sealed class SeatAnchor : MonoBehaviour
    {
        [Tooltip("The shared Camera is re-parented here while seated. Its local rotation is the seat-forward baseline (identity = facing the cop).")]
        [SerializeField] private Transform cameraMount;

        [Tooltip("Where the player's CharacterController is teleported to when standing back up.")]
        [SerializeField] private Transform exitPose;

        [Tooltip("Where the player must be within InteractRadius of, standing, to sit down with E. Defaults to this object's own transform if unset.")]
        [SerializeField] private Transform interactPoint;

        [SerializeField] private float interactRadius = 1.5f;

        public Transform CameraMount => cameraMount;
        public Transform ExitPose => exitPose;
        public Transform InteractPoint => interactPoint != null ? interactPoint : transform;
        public float InteractRadius => interactRadius;
    }
}
