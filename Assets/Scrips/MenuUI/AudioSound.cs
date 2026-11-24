
using UnityEngine;

public class AudioSound : MonoBehaviour
{
    [Header("Background Music")]
    [SerializeField] private string sceneMusicID;

    [Header("UI Sound")]
    [SerializeField] private string clickSoundID = "ButtonClick";

    [Header("Settings")]
    [SerializeField] private bool playMusicOnStart = true;

    private void Start()
    {
        if (playMusicOnStart)
            PlaySceneMusic();
    }

    public void PlaySceneMusic()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(sceneMusicID);
    }

    public void PlayClickSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clickSoundID);
    }

    public void PlaySound(string soundID)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(soundID);
    }

    public void StopMusic()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopBGM();
    }
}
