using UnityEngine;

namespace FalsePositive.CabinNight
{
    /// <summary>Returns the standalone-level player to the authored spawn if they leave the set.</summary>
    public sealed class CabinFallRecovery : MonoBehaviour
    {
        [SerializeField] private Vector3 spawnPosition;
        [SerializeField] private float minimumHeight = -4f;

        public void Configure(Vector3 position)
        {
            spawnPosition = position;
        }

        private void LateUpdate()
        {
            if (transform.position.y >= minimumHeight)
            {
                return;
            }

            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            transform.position = spawnPosition;

            if (controller != null)
            {
                controller.enabled = true;
            }
        }
    }
}
