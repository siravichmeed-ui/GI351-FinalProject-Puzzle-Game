
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Audio Library")]
    [SerializeField] private AudioLibrary audioLibrary;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource characterSource;

    [Header("Auto Play")]
    [SerializeField] private bool playBGMOnStart = true;
    [SerializeField] private string startBGM_ID = "MenuMusic";

    private const float MIN_DB = -80f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioLibrary == null)
            audioLibrary = GetComponent<AudioLibrary>();

        LoadVolume();
    }

    private void Start()
    {
        if (playBGMOnStart)
            PlayBGM(startBGM_ID);
    }

    private void SetVolume(string parameter, float value)
    {
        value = Mathf.Clamp01(value);

        float db = value <= 0.0001f
            ? MIN_DB
            : Mathf.Log10(value) * 20f;

        if (audioMixer != null)
            audioMixer.SetFloat(parameter, db);

        PlayerPrefs.SetFloat(parameter, value);
        PlayerPrefs.Save();
    }

    public void SetMasterVolume(float value)
    {
        SetVolume("MasterVolume", value);
    }

    public void SetBGMVolume(float value)
    {
        SetVolume("BGMVolume", value);
    }

    public void SetSFXVolume(float value)
    {
        SetVolume("SFXVolume", value);
    }

    public void SetAmbientVolume(float value)
    {
        SetVolume("AmbientVolume", value);
    }

    public void SetCharacterVolume(float value)
    {
        SetVolume("CharacterVolume", value);
    }

    private void LoadVolume()
    {
        LoadSavedVolume("MasterVolume", 1f);
        LoadSavedVolume("BGMVolume", 0.7f);
        LoadSavedVolume("SFXVolume", 0.8f);
        LoadSavedVolume("AmbientVolume", 0.6f);
        LoadSavedVolume("CharacterVolume", 0.8f);
    }

    private void LoadSavedVolume(string parameter, float defaultValue)
    {
        float value = PlayerPrefs.GetFloat(parameter, defaultValue);

        float db = value <= 0.0001f
            ? MIN_DB
            : Mathf.Log10(value) * 20f;

        if (audioMixer != null)
            audioMixer.SetFloat(parameter, db);
    }

    // BGM
    public void PlayBGM(string id)
    {
        if (audioLibrary == null || bgmSource == null)
            return;

        AudioClip clip = audioLibrary.GetBGM(id);

        if (clip == null)
            return;

        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return;

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }

    // SFX
    public void PlaySFX(string id)
    {
        if (audioLibrary == null || sfxSource == null)
            return;

        AudioClip clip = audioLibrary.GetSFX(id);

        if (clip != null)
            sfxSource.PlayOneShot(clip);
    }

    public void PlaySFXByID(string id)
    {
        PlaySFX(id);
    }

    // Ambient
    public void PlayAmbient(string id)
    {
        if (audioLibrary == null || ambientSource == null)
            return;

        AudioClip clip = audioLibrary.GetAmbient(id);

        if (clip == null)
            return;

        if (ambientSource.clip == clip && ambientSource.isPlaying)
            return;

        ambientSource.clip = clip;
        ambientSource.loop = true;
        ambientSource.Play();
    }

    public void StopAmbient()
    {
        if (ambientSource != null)
            ambientSource.Stop();
    }

    // Character
    public void PlayCharacter(string id)
    {
        if (audioLibrary == null || characterSource == null)
            return;

        AudioClip clip = audioLibrary.GetCharacter(id);

        if (clip != null)
            characterSource.PlayOneShot(clip);
    }

    // Methods for Inspector
    public void PlayBGMByID(string id)
    {
        PlayBGM(id);
    }

    public void PlayAmbientByID(string id)
    {
        PlayAmbient(id);
    }

    public void PlayCharacterByID(string id)
    {
        PlayCharacter(id);
    }
}
