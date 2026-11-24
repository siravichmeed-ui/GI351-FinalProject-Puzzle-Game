
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    [SerializeField] private string soundID = "ButtonHover";

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlaySound();
    }

    public void OnSelect(BaseEventData eventData)
    {
        PlaySound();
    }

    private void PlaySound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(soundID);
        }
    }
}
