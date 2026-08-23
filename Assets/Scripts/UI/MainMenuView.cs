using System;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private Button startButton;

        public event Action StartRequested;

        private void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
            }
        }

        private void OnDestroy()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartClicked);
            }
        }

        private void OnStartClicked()
        {
            StartRequested?.Invoke();
        }

        public void Configure(Button button)
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartClicked);
            }

            startButton = button;
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
            }
        }

#if UNITY_EDITOR
        public void EditorAssignStartButton(Button button)
        {
            Configure(button);
        }
#endif
    }
}
