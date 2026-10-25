using System.Collections;
using UnityEngine;

namespace JRPG.Core
{
    public sealed class TerrainSnap : MonoBehaviour
    {
        public bool snapOnGenerate = true;
        public float offsetY;
        public Terrain terrain;

        private static readonly string[] GroundedObjectNames =
        {
            "Broken_Hearth_Sign",
            "Abandoned_Campsite",
            "Rin_Sword",
            "Traveler_Note",
            "Last_Lantern_Pickup",
            "First_Fog_Skeleton",
            "Player",
            "Hearth_Gate",
            "Forest_Path_Lamp_1",
            "Forest_Path_Lamp_2",
            "Forest_Path_Lamp_3",
            "Forest_Path_Lamp_4"
        };

        private void Start()
        {
            if (transform.name == "Forest_Generated")
            {
                StartCoroutine(SnapGeneratedSetpieces());
                return;
            }

            if (snapOnGenerate)
            {
                SnapNow();
            }
        }

        public void SnapNow()
        {
            if (!snapOnGenerate)
            {
                return;
            }

            if (terrain == null)
            {
                terrain = FindForestTerrain();
            }

            if (terrain == null)
            {
                Debug.LogWarning("TerrainSnap: no Forest_Terrain was found for " + name + ".", this);
                return;
            }

            CharacterController characterController = GetComponent<CharacterController>();
            bool controllerWasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            Vector3 position = transform.position;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + offsetY;
            transform.position = position;
            Physics.SyncTransforms();
            if (characterController != null)
            {
                characterController.enabled = controllerWasEnabled;
            }
        }

        public static void SnapKnownObjects(Terrain targetTerrain)
        {
            if (targetTerrain == null)
            {
                Debug.LogWarning("TerrainSnap: cannot snap objects because the Terrain is missing.");
                return;
            }

            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int snapped = 0;
            foreach (Transform target in transforms)
            {
                if (!IsGroundedObjectName(target.name))
                {
                    continue;
                }

                TerrainSnap snap = target.GetComponent<TerrainSnap>();
                bool addedSnap = snap == null;
                if (snap == null)
                {
                    snap = target.gameObject.AddComponent<TerrainSnap>();
                }

                snap.terrain = targetTerrain;
                snap.snapOnGenerate = true;
                if (addedSnap && target.name == "Player")
                {
                    snap.offsetY = 0.15f;
                }
                snap.SnapNow();
                snapped++;
            }

            Debug.Log("TerrainSnap: re-grounded " + snapped + " tutorial objects.");
        }

        private IEnumerator SnapGeneratedSetpieces()
        {
            float timeout = 10f;
            Transform setpieces = null;
            while (timeout > 0f && setpieces == null)
            {
                setpieces = transform.Find("Forest_Prologue_Setpieces");
                if (setpieces != null)
                {
                    break;
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (setpieces == null)
            {
                yield break;
            }

            Transform[] targets = setpieces.GetComponentsInChildren<Transform>(true);
            foreach (Transform target in targets)
            {
                if (!IsGroundedObjectName(target.name))
                {
                    continue;
                }

                TerrainSnap snap = target.GetComponent<TerrainSnap>();
                if (snap == null)
                {
                    snap = target.gameObject.AddComponent<TerrainSnap>();
                }

                snap.terrain = terrain;
                snap.snapOnGenerate = true;
                snap.SnapNow();
            }
        }

        private static bool IsGroundedObjectName(string objectName)
        {
            foreach (string groundedName in GroundedObjectNames)
            {
                if (objectName == groundedName)
                {
                    return true;
                }
            }

            return false;
        }

        private static Terrain FindForestTerrain()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Terrain candidate in terrains)
            {
                if (candidate.name == "Forest_Terrain")
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
