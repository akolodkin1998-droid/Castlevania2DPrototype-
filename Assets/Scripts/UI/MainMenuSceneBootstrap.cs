using UnityEngine;

namespace Castlevania2D.UI
{
    public sealed class MainMenuSceneBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (FindFirstObjectByType<MainMenuView>() == null)
            {
                MainMenuRuntimeFactory.Create();
            }
        }
    }
}
