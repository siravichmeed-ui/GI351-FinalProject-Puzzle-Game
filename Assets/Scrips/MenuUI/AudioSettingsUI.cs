
using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    //[SerializeField] private Slider ambientSlider;
    //[SerializeField] private Slider characterSlider;

    private void Start()
    {
        // ตั้งค่า Slider
        masterSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        bgmSlider.value = PlayerPrefs.GetFloat("BGMVolume", 0.7f);
        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
        //ambientSlider.value = PlayerPrefs.GetFloat("AmbientVolume", 0.6f);
        //characterSlider.value = PlayerPrefs.GetFloat("CharacterVolume", 0.8f);

        // เชื่อม Slider กับ AudioManager
        masterSlider.onValueChanged.AddListener(
            AudioManager.Instance.SetMasterVolume);

        bgmSlider.onValueChanged.AddListener(
            AudioManager.Instance.SetBGMVolume);

        sfxSlider.onValueChanged.AddListener(
            AudioManager.Instance.SetSFXVolume);

        //ambientSlider.onValueChanged.AddListener(
        //    AudioManager.Instance.SetAmbientVolume);

        //characterSlider.onValueChanged.AddListener(
        //    AudioManager.Instance.SetCharacterVolume);

        // ปิดหน้า Settings ตอนเริ่มเกม
        settingsPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

}
