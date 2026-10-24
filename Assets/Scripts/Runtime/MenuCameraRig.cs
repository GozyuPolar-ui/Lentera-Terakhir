using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JRPG.Menu
{
    public sealed class MenuCameraRig : MonoBehaviour
    {
        public Transform lookTarget;
        public float dollyDistance = 0.28f;
        public float dollyLoopDuration = 24f;
        public float breathingAmount = 0.035f;
        public float breathingSpeed = 0.55f;
        public float handheldAmount = 0.12f;
        public float handheldSpeed = 0.12f;
        public float parallaxDegrees = 1.5f;
        public float inputSmoothing = 4f;
        public float lookSmoothing = 2f;

        private Vector3 basePosition;
        private Vector2 smoothedPointer;
        private float phaseOffset;

        private void Awake()
        {
            basePosition = transform.position;
            phaseOffset = Random.value * 100f;
        }

        private void LateUpdate()
        {
            if (lookTarget == null)
            {
                return;
            }

            smoothedPointer = Vector2.Lerp(
                smoothedPointer, ReadPointer(),
                1f - Mathf.Exp(-inputSmoothing * Time.unscaledDeltaTime));

            float time = Time.unscaledTime;
            float loop = Mathf.Max(1f, dollyLoopDuration);
            float dolly = (1f - Mathf.Cos(2f * Mathf.PI * time / loop)) * 0.5f;
            float noiseX = (Mathf.PerlinNoise(phaseOffset, time * handheldSpeed) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(phaseOffset + 31f, time * handheldSpeed) - 0.5f) * 2f;
            Vector3 towardTarget = (lookTarget.position - basePosition).normalized;
            Vector3 position = basePosition + towardTarget * (dollyDistance * dolly);
            position += transform.up * (Mathf.Sin(time * breathingSpeed) * breathingAmount);
            position += transform.right * (noiseX * handheldAmount);
            position += transform.up * (noiseY * handheldAmount * 0.55f);
            transform.position = position;

            Quaternion targetRotation = Quaternion.LookRotation(
                lookTarget.position - position, Vector3.up);
            targetRotation *= Quaternion.Euler(
                -smoothedPointer.y * parallaxDegrees,
                smoothedPointer.x * parallaxDegrees,
                noiseX * handheldAmount * 0.3f);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRotation, 1f - Mathf.Exp(-lookSmoothing * Time.unscaledDeltaTime));
        }

        private static Vector2 ReadPointer()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null)
            {
                return Vector2.zero;
            }

            Vector2 position = Mouse.current.position.ReadValue();
#else
            Vector2 position = Input.mousePosition;
#endif
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            return new Vector2(
                Mathf.Clamp((position.x / width - 0.5f) * 2f, -1f, 1f),
                Mathf.Clamp((position.y / height - 0.5f) * 2f, -1f, 1f));
        }
    }
}
