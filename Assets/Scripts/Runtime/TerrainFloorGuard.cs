using System.Collections;
using UnityEngine;

namespace JRPG.Core
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TerrainFloorGuard : MonoBehaviour
    {
        public Terrain terrain;
        public float recoveryThreshold = 0.5f;
        public float recoveryOffset = 0.2f;

        private CharacterController characterController;
        private PlayerController playerController;
        private bool isRecovering;
        private bool hasWarned;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            playerController = GetComponent<PlayerController>();
            if (terrain == null)
            {
                Terrain[] terrains = FindObjectsByType<Terrain>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Terrain candidate in terrains)
                {
                    if (candidate.name == "Forest_Terrain")
                    {
                        terrain = candidate;
                        break;
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (terrain == null || characterController == null || isRecovering
                || !characterController.enabled
                || (playerController != null && !playerController.enabled))
            {
                return;
            }

            Vector3 position = transform.position;
            float groundHeight = terrain.SampleHeight(position) + terrain.transform.position.y;
            if (position.y >= groundHeight - recoveryThreshold)
            {
                return;
            }

            if (!hasWarned)
            {
                Debug.LogWarning(
                    "TerrainFloorGuard: player fell below Forest_Terrain; restoring the player to the surface.",
                    this);
                hasWarned = true;
            }

            StartCoroutine(RecoverToTerrain(groundHeight + recoveryOffset));
        }

        private IEnumerator RecoverToTerrain(float targetY)
        {
            isRecovering = true;
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            Vector3 position = transform.position;
            position.y = targetY;
            transform.position = position;
            Physics.SyncTransforms();
            yield return null;
            if (wasEnabled && characterController != null)
            {
                characterController.enabled = true;
            }

            isRecovering = false;
        }
    }
}
