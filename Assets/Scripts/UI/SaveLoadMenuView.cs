using System;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    [DisallowMultipleComponent]
    public sealed class SaveLoadMenuView : MonoBehaviour
    {
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private MenuButtonSlideFeedback[] slotFeedbacks;
        [SerializeField] private Text[] slotTimestampLabels;

        public event Action SaveRequested;
        public event Action LoadRequested;
        public event Action CloseRequested;
        public event Action<int> SlotSelected;

        public int SelectedSlotIndex { get; private set; } = -1;
        public int SlotCount => slotTimestampLabels != null ? slotTimestampLabels.Length : 0;
        public bool IsDeathMode { get; private set; }

        private void Awake()
        {
            AddListeners();
        }

        private void OnDestroy()
        {
            RemoveListeners();
        }

        private void AddListeners()
        {
            if (saveButton != null)
            {
                saveButton.onClick.AddListener(OnSaveClicked);
            }

            if (loadButton != null)
            {
                loadButton.onClick.AddListener(OnLoadClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (slotButtons == null)
            {
                return;
            }

            if (slotButtons.Length > 0 && slotButtons[0] != null)
            {
                slotButtons[0].onClick.AddListener(OnSlot0Clicked);
            }

            if (slotButtons.Length > 1 && slotButtons[1] != null)
            {
                slotButtons[1].onClick.AddListener(OnSlot1Clicked);
            }

            if (slotButtons.Length > 2 && slotButtons[2] != null)
            {
                slotButtons[2].onClick.AddListener(OnSlot2Clicked);
            }

            if (slotButtons.Length > 3 && slotButtons[3] != null)
            {
                slotButtons[3].onClick.AddListener(OnSlot3Clicked);
            }

            if (slotButtons.Length > 4 && slotButtons[4] != null)
            {
                slotButtons[4].onClick.AddListener(OnSlot4Clicked);
            }
        }

        private void RemoveListeners()
        {
            if (saveButton != null)
            {
                saveButton.onClick.RemoveListener(OnSaveClicked);
            }

            if (loadButton != null)
            {
                loadButton.onClick.RemoveListener(OnLoadClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
            }

            if (slotButtons == null)
            {
                return;
            }

            if (slotButtons.Length > 0 && slotButtons[0] != null)
            {
                slotButtons[0].onClick.RemoveListener(OnSlot0Clicked);
            }

            if (slotButtons.Length > 1 && slotButtons[1] != null)
            {
                slotButtons[1].onClick.RemoveListener(OnSlot1Clicked);
            }

            if (slotButtons.Length > 2 && slotButtons[2] != null)
            {
                slotButtons[2].onClick.RemoveListener(OnSlot2Clicked);
            }

            if (slotButtons.Length > 3 && slotButtons[3] != null)
            {
                slotButtons[3].onClick.RemoveListener(OnSlot3Clicked);
            }

            if (slotButtons.Length > 4 && slotButtons[4] != null)
            {
                slotButtons[4].onClick.RemoveListener(OnSlot4Clicked);
            }
        }

        private void OnSaveClicked()
        {
            if (IsDeathMode || SelectedSlotIndex < 0)
            {
                return;
            }

            SaveRequested?.Invoke();
        }

        private void OnLoadClicked()
        {
            if (SelectedSlotIndex < 0)
            {
                return;
            }

            LoadRequested?.Invoke();
        }

        private void OnCloseClicked()
        {
            CloseRequested?.Invoke();
        }

        private void OnSlot0Clicked()
        {
            SelectSlot(0);
        }

        private void OnSlot1Clicked()
        {
            SelectSlot(1);
        }

        private void OnSlot2Clicked()
        {
            SelectSlot(2);
        }

        private void OnSlot3Clicked()
        {
            SelectSlot(3);
        }

        private void OnSlot4Clicked()
        {
            SelectSlot(4);
        }

        private void SelectSlot(int slotIndex)
        {
            SelectedSlotIndex = slotIndex;
            if (slotFeedbacks != null)
            {
                for (int i = 0; i < slotFeedbacks.Length; i++)
                {
                    if (slotFeedbacks[i] != null)
                    {
                        slotFeedbacks[i].SetSelected(i == slotIndex);
                    }
                }
            }

            SlotSelected?.Invoke(slotIndex);
        }

        public void SetSlotText(int slotIndex, string text)
        {
            if (slotTimestampLabels == null
                || slotIndex < 0
                || slotIndex >= slotTimestampLabels.Length)
            {
                return;
            }

            Text label = slotTimestampLabels[slotIndex];
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }

        public void SetDeathMode(bool enabled)
        {
            IsDeathMode = enabled;
            if (saveButton == null)
            {
                return;
            }

            MenuButtonSlideFeedback feedback =
                saveButton.GetComponent<MenuButtonSlideFeedback>();
            if (feedback != null)
            {
                feedback.SetVisible(!enabled);
            }
            else
            {
                saveButton.gameObject.SetActive(!enabled);
            }
        }

#if UNITY_EDITOR
        public void EditorAssignButtons(
            Button newSaveButton,
            Button newLoadButton,
            Button newCloseButton,
            Button[] newSlotButtons,
            MenuButtonSlideFeedback[] newSlotFeedbacks,
            Text[] newSlotTimestampLabels)
        {
            saveButton = newSaveButton;
            loadButton = newLoadButton;
            closeButton = newCloseButton;
            slotButtons = newSlotButtons;
            slotFeedbacks = newSlotFeedbacks;
            slotTimestampLabels = newSlotTimestampLabels;
        }
#endif
    }
}
