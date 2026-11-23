using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class ShopMoneyManager : MonoBehaviour
{
    public static ShopMoneyManager Instance { get; private set; }

    [Header("Money Settings")]
    [SerializeField] private int startingMoney = 500;

    [Header("UI")]
    [SerializeField] private TMP_Text moneyText;

    [Header("Juice")]
    [SerializeField] private float countDuration = 0.6f;
    [SerializeField] private float punchScale = 1.3f;
    [SerializeField] private float punchDuration = 0.35f;
    [SerializeField] private AudioClip cashSfx;

    private const string MoneySaveKey = "CursedShop_Money";

    public int CurrentMoney { get; private set; }

    /// <summary>มีช่องแสดงเงินของตัวเองหรือไม่ (ShopHUD จะได้ไม่เขียนทับ)</summary>
    public bool HasMoneyText => moneyText != null;

    /// <summary>(เงินเดิม, เงินใหม่)</summary>
    public event Action<int, int> OnMoneyChanged;

    private int displayedMoney;
    private Coroutine animRoutine;
    private Vector3 textBaseScale = Vector3.one;
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CurrentMoney = PlayerPrefs.HasKey(MoneySaveKey)
            ? PlayerPrefs.GetInt(MoneySaveKey)
            : startingMoney;

        displayedMoney = CurrentMoney;

        if (moneyText != null)
            textBaseScale = moneyText.transform.localScale;

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        SetText(displayedMoney);
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("Money amount must be greater than zero.");
            return;
        }

        int old = CurrentMoney;

        CurrentMoney += amount;
        SaveMoney();

        OnMoneyChanged?.Invoke(old, CurrentMoney);

        if (cashSfx != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.97f, 1.05f);
            audioSource.PlayOneShot(cashSfx);
        }

        AnimateTo(CurrentMoney, true);

        Debug.Log("Received " + amount + " money. Total: " + CurrentMoney);
    }

    public bool SpendMoney(int amount)
    {
        if (amount <= 0 || CurrentMoney < amount)
            return false;

        int old = CurrentMoney;

        CurrentMoney -= amount;
        SaveMoney();

        OnMoneyChanged?.Invoke(old, CurrentMoney);

        AnimateTo(CurrentMoney, false);

        return true;
    }

    public void SaveMoney()
    {
        PlayerPrefs.SetInt(MoneySaveKey, CurrentMoney);
        PlayerPrefs.Save();
    }

    public void ResetMoney()
    {
        CurrentMoney = startingMoney;
        displayedMoney = CurrentMoney;

        PlayerPrefs.DeleteKey(MoneySaveKey);
        PlayerPrefs.Save();

        if (animRoutine != null)
            StopCoroutine(animRoutine);

        animRoutine = null;
        SetText(displayedMoney);
    }

    // =====================================
    // UI
    // =====================================

    private void SetText(int value)
    {
        if (moneyText != null)
            moneyText.text = value.ToString("N0");
    }

    private void AnimateTo(int target, bool punch)
    {
        if (moneyText == null)
            return;

        if (animRoutine != null)
            StopCoroutine(animRoutine);

        animRoutine = StartCoroutine(AnimateRoutine(target, punch));
    }

    private IEnumerator AnimateRoutine(int target, bool punch)
    {
        int start = displayedMoney;
        float t = 0f;
        float duration = Mathf.Max(countDuration, 0.01f);
        float punchTime = Mathf.Max(punchDuration, 0.01f);
        float total = Mathf.Max(duration, punch ? punchTime : 0f);

        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            float n = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - n, 3f);

            displayedMoney = Mathf.RoundToInt(Mathf.Lerp(start, target, eased));
            SetText(displayedMoney);

            if (punch)
            {
                float p = Mathf.Clamp01(t / punchTime);
                float s = 1f + (punchScale - 1f) * Mathf.Sin(p * Mathf.PI);

                moneyText.transform.localScale = textBaseScale * s;
            }

            yield return null;
        }

        displayedMoney = target;
        SetText(displayedMoney);
        moneyText.transform.localScale = textBaseScale;
        animRoutine = null;
    }

    private void OnApplicationQuit()
    {
        SaveMoney();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
