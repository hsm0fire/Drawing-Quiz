using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private bool playHoverSound = false;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        // إضافة مستمع لحدث النقر تلقائياً
        button.onClick.AddListener(OnButtonClicked);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayButtonClick();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playHoverSound && button.interactable && SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayButtonHover();
        }
    }
}