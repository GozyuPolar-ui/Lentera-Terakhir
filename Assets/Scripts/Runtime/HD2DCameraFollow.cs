using UnityEngine;

namespace JRPG.Rendering
{
    /// <summary>
    /// HD-2D style camera: fixed angle, narrow field of view, smoothly follows a target.
    /// Put this on the Main Camera and assign the Player as target.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class HD2DCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 14f;
        [SerializeField] private float pitch = 35f;
        [SerializeField] private float yaw = 0f;
        [SerializeField] private float fieldOfView = 28f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private float smoothTime = 0.12f;

        private Camera cam;
        private Vector3 velocity;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            ApplyFov();
        }

        private void OnValidate()
        {
            cam = GetComponent<Camera>();
            ApplyFov();
        }

        private void ApplyFov()
        {
            if (cam != null)
            {
                cam.orthographic = false;
                cam.fieldOfView = fieldOfView;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + targetOffset;
            Vector3 desired = focus - rot * Vector3.forward * distance;

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.rotation = rot;
        }
    }
}
