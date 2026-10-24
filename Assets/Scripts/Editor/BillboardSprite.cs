using UnityEngine;

namespace JRPG.Rendering
{
    /// <summary>
    /// HD-2D style billboard: the sprite stays perfectly upright and only
    /// matches the camera's yaw (Y rotation). It never tilts with the camera pitch,
    /// so characters look like standing cardboard cut-outs in a 3D world.
    /// </summary>
    [ExecuteAlways]
    public class BillboardSprite : MonoBehaviour
    {
        [Tooltip("Leave empty to use Camera.main")]
        [SerializeField] private Camera targetCamera;

        private void LateUpdate()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            float yaw = cam.transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
