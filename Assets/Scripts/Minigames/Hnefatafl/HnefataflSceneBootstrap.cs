using UnityEngine;

namespace Castlevania2D.Minigames.Hnefatafl
{
    /// <summary>
    /// Builds the Hnefatafl table when the dedicated scene loads.
    /// NPC hub entry can later load this scene.
    /// </summary>
    public sealed class HnefataflSceneBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (FindFirstObjectByType<HnefataflGameController>() == null)
            {
                HnefataflRuntimeFactory.Create();
            }
        }
    }
}
