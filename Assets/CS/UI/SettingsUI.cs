using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Buttons")]
    [SerializeField] private Button openSettingsBtn;
    [SerializeField] private Button closeSettingsBtn;

    private void Start()
    {
        // إخفاء اللوحة في البداية
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // ربط أزرار الفتح والإغلاق
        if (openSettingsBtn != null) openSettingsBtn.onClick.AddListener(OpenSettings);
        if (closeSettingsBtn != null) closeSettingsBtn.onClick.AddListener(CloseSettings);

        // استرجاع القيم وربط أحداث المنزلقات
        if (SoundEffectsManager.Instance != null)
        {
            if (musicSlider != null)
            {
                musicSlider.value = SoundEffectsManager.Instance.GetBGMVolume();
                musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.value = SoundEffectsManager.Instance.GetSFXVolume();
                sfxSlider.onValueChanged.AddListener(OnSFXSliderChanged);
            }
        }
    }

    public void OpenSettings()
    {
        if (SoundEffectsManager.Instance != null) SoundEffectsManager.Instance.PlayButtonClick();
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (SoundEffectsManager.Instance != null) SoundEffectsManager.Instance.PlayButtonClick();
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void OnMusicSliderChanged(float val)
    {
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.SetBGMVolume(val);
        }
    }

    private void OnSFXSliderChanged(float val)
    {
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.SetSFXVolume(val);
        }
    }
}