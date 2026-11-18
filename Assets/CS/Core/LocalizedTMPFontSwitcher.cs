using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class LocalizedTMPFontSwitcher : MonoBehaviour
{
    [Header("Target Text")]
    [SerializeField] private TMP_Text targetText;

    [Header("Localized Font")]
    [SerializeField] private LocalizedTmpFont localizedFont;

    private void OnEnable()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        localizedFont.AssetChanged += OnFontChanged;
    }

    private void OnDisable()
    {
        localizedFont.AssetChanged -= OnFontChanged;
    }

    private void OnFontChanged(TMP_FontAsset font)
    {
        if (targetText == null || font == null)
            return;

        targetText.font = font;

        targetText.SetAllDirty();
    }
}
