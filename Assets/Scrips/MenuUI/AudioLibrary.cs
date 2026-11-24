
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AudioEntry
{
    public string id;
    public AudioClip clip;
}

public class AudioLibrary : MonoBehaviour
{
    [Header("Background Music")]
    public List<AudioEntry> bgmList = new List<AudioEntry>();

    [Header("Sound Effects")]
    public List<AudioEntry> sfxList = new List<AudioEntry>();

    [Header("Ambient")]
    public List<AudioEntry> ambientList = new List<AudioEntry>();

    [Header("Character")]
    public List<AudioEntry> characterList = new List<AudioEntry>();

    private AudioClip GetClip(List<AudioEntry> list, string id)
    {
        AudioEntry entry = list.Find(x => x.id == id);

        if (entry == null || entry.clip == null)
        {
            Debug.LogWarning("Audio not found: " + id);
            return null;
        }

        return entry.clip;
    }

    public AudioClip GetBGM(string id) => GetClip(bgmList, id);
    public AudioClip GetSFX(string id) => GetClip(sfxList, id);
    public AudioClip GetAmbient(string id) => GetClip(ambientList, id);
    public AudioClip GetCharacter(string id) => GetClip(characterList, id);
}
