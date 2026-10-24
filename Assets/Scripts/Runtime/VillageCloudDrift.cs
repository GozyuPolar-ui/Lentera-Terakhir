using UnityEngine;

namespace JRPG.Rendering
{
    public class VillageCloudDrift : MonoBehaviour
    {
        [SerializeField] private float speed = 0.7f;
        [SerializeField] private float horizontalLimit = 90f;

        public void Configure(float driftSpeed, float wrapLimit)
        {
            speed = driftSpeed;
            horizontalLimit = wrapLimit;
        }

        private void Update()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform cloud = transform.GetChild(i);
                Vector3 position = cloud.localPosition;
                position.x += speed * Time.deltaTime;
                if (position.x > horizontalLimit)
                {
                    position.x = -horizontalLimit;
                }

                cloud.localPosition = position;
            }
        }
    }
}
