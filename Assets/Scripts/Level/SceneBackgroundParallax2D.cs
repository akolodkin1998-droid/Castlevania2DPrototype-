using UnityEngine;

namespace Castlevania2D.Level
{
    /// <summary>
    /// Scrolls scenic background sprites slower than the camera on X.
    /// Far layers (sky, clouds) follow the camera more; nearer forest/roots stay closer to the world.
    /// Y is left alone so the village sky does not drop into the dungeon.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class SceneBackgroundParallax2D : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private bool affectY;
        [SerializeField] private NameFollowOverride[] followOverrides;

        private Transform[] layers;
        private Vector3[] origins;
        private float[] follows;
        private Vector3 cameraOrigin;
        private bool initialized;
        private int layerCount;

        private void LateUpdate()
        {
            if (!TryResolveCamera(out Transform cam))
            {
                return;
            }

            if (!initialized)
            {
                CollectLayers();
                cameraOrigin = cam.position;
                initialized = true;
            }

            Vector3 cameraDelta = cam.position - cameraOrigin;
            for (int i = 0; i < layerCount; i++)
            {
                Transform layer = layers[i];
                if (layer == null)
                {
                    continue;
                }

                Vector3 origin = origins[i];
                float follow = follows[i];
                float x = origin.x + cameraDelta.x * follow;
                float y = affectY ? origin.y + cameraDelta.y * follow : origin.y;
                layer.position = new Vector3(x, y, origin.z);
            }
        }

        private bool TryResolveCamera(out Transform cam)
        {
            if (cameraTransform != null)
            {
                cam = cameraTransform;
                return true;
            }

            Camera main = Camera.main;
            if (main != null)
            {
                cameraTransform = main.transform;
                cam = cameraTransform;
                return true;
            }

            cam = null;
            return false;
        }

        private void CollectLayers()
        {
            layerCount = 0;
            CountMatching(transform);
            layers = layerCount > 0 ? new Transform[layerCount] : System.Array.Empty<Transform>();
            origins = layerCount > 0 ? new Vector3[layerCount] : System.Array.Empty<Vector3>();
            follows = layerCount > 0 ? new float[layerCount] : System.Array.Empty<float>();

            int write = 0;
            FillMatching(transform, ref write);
            SortByHierarchyDepth();
        }

        private void CountMatching(Transform current)
        {
            if (!current.gameObject.activeInHierarchy)
            {
                return;
            }

            if (current != transform && TryResolveCameraFollow(current.name, out _))
            {
                layerCount++;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                CountMatching(current.GetChild(i));
            }
        }

        private void FillMatching(Transform current, ref int write)
        {
            if (!current.gameObject.activeInHierarchy)
            {
                return;
            }

            if (current != transform && TryResolveCameraFollow(current.name, out float follow))
            {
                layers[write] = current;
                origins[write] = current.position;
                follows[write] = follow;
                write++;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                FillMatching(current.GetChild(i), ref write);
            }
        }

        private void SortByHierarchyDepth()
        {
            for (int i = 1; i < layerCount; i++)
            {
                Transform layer = layers[i];
                Vector3 origin = origins[i];
                float follow = follows[i];
                int depth = GetDepth(layer);
                int j = i - 1;
                while (j >= 0 && GetDepth(layers[j]) > depth)
                {
                    layers[j + 1] = layers[j];
                    origins[j + 1] = origins[j];
                    follows[j + 1] = follows[j];
                    j--;
                }

                layers[j + 1] = layer;
                origins[j + 1] = origin;
                follows[j + 1] = follow;
            }
        }

        private static int GetDepth(Transform t)
        {
            int depth = 0;
            while (t != null)
            {
                depth++;
                t = t.parent;
            }

            return depth;
        }

        private bool TryResolveCameraFollow(string objectName, out float cameraFollow)
        {
            if (followOverrides != null)
            {
                for (int i = 0; i < followOverrides.Length; i++)
                {
                    NameFollowOverride overrideRule = followOverrides[i];
                    if (string.IsNullOrEmpty(overrideRule.namePrefix))
                    {
                        continue;
                    }

                    if (objectName.StartsWith(overrideRule.namePrefix, System.StringComparison.Ordinal))
                    {
                        cameraFollow = Mathf.Clamp01(overrideRule.cameraFollow);
                        return true;
                    }
                }
            }

            return TryResolveDefaultCameraFollow(objectName, out cameraFollow);
        }

        internal static bool TryResolveDefaultCameraFollow(string objectName, out float cameraFollow)
        {
            cameraFollow = 0f;
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            if (objectName.StartsWith("Red Sky", System.StringComparison.Ordinal) ||
                objectName == "Sky")
            {
                cameraFollow = 0.92f;
                return true;
            }

            if (objectName.StartsWith("Cloud", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.78f;
                return true;
            }

            if (objectName.StartsWith("Mountains 4", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.62f;
                return true;
            }

            if (objectName.StartsWith("Mountains 3", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.52f;
                return true;
            }

            if (objectName.StartsWith("Mountains 2", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.42f;
                return true;
            }

            if (objectName.StartsWith("Mountains", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.32f;
                return true;
            }

            if (objectName.StartsWith("UndergroundLine3", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.82f;
                return true;
            }

            if (objectName.StartsWith("UndergroundLine2", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.48f;
                return true;
            }

            if (objectName.StartsWith("UndergroundLine1", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.18f;
                return true;
            }

            if (objectName.StartsWith("ForestLine3", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.82f;
                return true;
            }

            if (objectName.StartsWith("ForestLine2", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.48f;
                return true;
            }

            if (objectName.StartsWith("ForestLine1", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.18f;
                return true;
            }

            if (objectName.StartsWith("VillageLine2", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.82f;
                return true;
            }

            if (objectName.StartsWith("VillageLine1", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.18f;
                return true;
            }

            if (objectName.StartsWith("Background5", System.StringComparison.Ordinal) ||
                objectName.StartsWith("Background2", System.StringComparison.Ordinal) ||
                objectName.StartsWith("Background3", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.5f;
                return true;
            }

            if (objectName.StartsWith("BackGroundForest", System.StringComparison.Ordinal) ||
                objectName.StartsWith("BackgroundForest", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.22f;
                return true;
            }

            if (objectName.StartsWith("Roots", System.StringComparison.Ordinal))
            {
                cameraFollow = 0.16f;
                return true;
            }

            return false;
        }

        [System.Serializable]
        private struct NameFollowOverride
        {
            public string namePrefix;
            [Range(0f, 1f)] public float cameraFollow;
        }
    }
}
