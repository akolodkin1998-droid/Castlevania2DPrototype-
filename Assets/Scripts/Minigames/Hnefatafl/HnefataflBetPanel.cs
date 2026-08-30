using System;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.Minigames.Hnefatafl
{
    /// <summary>
    /// Left-side stake piles, slider 0-500, and confirm. Computer pile mirrors the player.
    /// </summary>
    public sealed class HnefataflBetPanel : MonoBehaviour
    {
        public const int MaxStake = 500;
        private const float SingleCoinSize = 66f;
        private const float PileSize = 220f;

        [SerializeField] private Slider stakeSlider;
        [SerializeField] private Text stakeValueText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private GameObject controlsRoot;
        [SerializeField] private Image playerPile;
        [SerializeField] private Image opponentPile;

        private Sprite[] pileSprites = Array.Empty<Sprite>();
        private int wallet;
        private bool locked;

        public event Action<int> BetConfirmed;

        public void Configure(
            Sprite[] sprites,
            Slider slider,
            Text valueText,
            Button confirm,
            GameObject controls,
            Image playerPileImage,
            Image opponentPileImage)
        {
            pileSprites = sprites ?? Array.Empty<Sprite>();
            stakeSlider = slider;
            stakeValueText = valueText;
            confirmButton = confirm;
            controlsRoot = controls;
            playerPile = playerPileImage;
            opponentPile = opponentPileImage;

            if (stakeSlider != null)
            {
                stakeSlider.onValueChanged.RemoveListener(OnSliderChanged);
                stakeSlider.onValueChanged.AddListener(OnSliderChanged);
                stakeSlider.wholeNumbers = true;
                stakeSlider.minValue = 0f;
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }
        }

        public void EnterBetting(int coinCount)
        {
            locked = false;
            wallet = Mathf.Max(0, coinCount);
            if (controlsRoot != null)
            {
                controlsRoot.SetActive(true);
            }

            if (stakeSlider != null)
            {
                int max = Mathf.Min(MaxStake, wallet);
                stakeSlider.minValue = 0f;
                stakeSlider.maxValue = max;
                stakeSlider.SetValueWithoutNotify(0f);
                stakeSlider.interactable = true;
            }

            ApplyStakeVisual(0);
        }

        public void LockPiles(int stake)
        {
            locked = true;
            if (controlsRoot != null)
            {
                controlsRoot.SetActive(false);
            }

            ApplyStakeVisual(stake);
        }

        private void OnDestroy()
        {
            if (stakeSlider != null)
            {
                stakeSlider.onValueChanged.RemoveListener(OnSliderChanged);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
            }
        }

        private void OnSliderChanged(float value)
        {
            if (locked)
            {
                return;
            }

            ApplyStakeVisual(Mathf.RoundToInt(value));
        }

        private void OnConfirmClicked()
        {
            if (locked)
            {
                return;
            }

            int stake = CurrentStake();
            if (stake < 0 || stake > wallet)
            {
                return;
            }

            BetConfirmed?.Invoke(stake);
        }

        private int CurrentStake()
        {
            return stakeSlider == null ? 0 : Mathf.RoundToInt(stakeSlider.value);
        }

        private void ApplyStakeVisual(int amount)
        {
            amount = Mathf.Clamp(amount, 0, MaxStake);
            if (stakeValueText != null)
            {
                stakeValueText.text = amount.ToString();
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = !locked && amount >= 0 && amount <= wallet;
            }

            Sprite sprite = SpriteForAmount(amount);
            bool show = sprite != null;
            float size = amount == 1 ? SingleCoinSize : PileSize;
            ApplyPile(playerPile, sprite, show, size);
            ApplyPile(opponentPile, sprite, show, size);
        }

        private Sprite SpriteForAmount(int amount)
        {
            if (amount <= 0 || pileSprites.Length == 0)
            {
                return null;
            }

            int index;
            if (amount == 1)
            {
                index = 0;
            }
            else if (amount < 50)
            {
                index = 1;
            }
            else if (amount < 100)
            {
                index = 2;
            }
            else if (amount < 200)
            {
                index = 3;
            }
            else
            {
                index = 4;
            }

            return pileSprites[Mathf.Min(index, pileSprites.Length - 1)];
        }

        private static void ApplyPile(Image image, Sprite sprite, bool show, float size)
        {
            if (image == null)
            {
                return;
            }

            image.enabled = show;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(size, size);
        }
    }
}
