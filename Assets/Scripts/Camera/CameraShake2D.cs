using UnityEngine;

namespace Castlevania2D.CameraFx
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class CameraShake2D : MonoBehaviour
    {
        private float remaining;
        private float duration;
        private float amplitude;

        public static void Play(float shakeAmplitude, float shakeDuration)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            CameraShake2D shake = camera.GetComponent<CameraShake2D>();
            if (shake == null)
            {
                shake = camera.gameObject.AddComponent<CameraShake2D>();
            }

            shake.Begin(shakeAmplitude, shakeDuration);
        }

        private void Begin(float shakeAmplitude, float shakeDuration)
        {
            amplitude = Mathf.Max(0f, shakeAmplitude);
            duration = Mathf.Max(0.01f, shakeDuration);
            remaining = duration;
        }

        private void LateUpdate()
        {
            if (remaining <= 0f)
            {
                return;
            }

            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                return;
            }

            float strength = amplitude * (remaining / duration);
            Vector2 offset = Random.insideUnitCircle * strength;
            transform.position += new Vector3(offset.x, offset.y, 0f);
        }
    }
}
