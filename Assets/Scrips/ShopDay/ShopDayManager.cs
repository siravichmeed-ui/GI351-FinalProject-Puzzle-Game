
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class ShopDayManager : MonoBehaviour
{
    public static ShopDayManager Instance;

    [Header("Day Settings")]
    public int currentDay = 1;

    [Header("Shop Time")]
    public int openHour = 8;
    public int closeHour = 18;

    // 1 วินาทีจริง = 1 นาทีในเกม
    private float elapsedGameMinutes = 0f;

    [Header("Money")]
    public int money = 500;
    public int dailyRent = 1000;

    [Header("Debt")]
    public int unpaidRent = 0;
    public int debtDays = 0;
    public int maxDebtDays = 1;

    [Header("Game Over")]
    public string gameOverSceneName = "GameOver";

    public bool IsShopOpen { get; private set; }
    public bool IsGameOver { get; private set; }

    public int OutstandingRent => unpaidRent;

    public event Action OnShopUpdated;
    public event Action OnDayEnded;
    public event Action OnBankrupt;

    private bool dayEnded = false;

    public string CurrentTime
    {
        get
        {
            int totalMinutes = openHour * 60 +
                Mathf.FloorToInt(elapsedGameMinutes);

            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;

            return $"{hour:00}:{minute:00}";
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartNewDay();
    }

    private void Update()
    {
        if (IsGameOver || dayEnded)
            return;

        // 1 วินาทีจริง = 1 นาทีในเกม
        elapsedGameMinutes += Time.deltaTime;

        float totalDayMinutes = (closeHour - openHour) * 60f;

        // ร้านเปิดตั้งแต่ 08:00 ถึงก่อน 18:00
        IsShopOpen = elapsedGameMinutes < totalDayMinutes;

        if (!IsShopOpen)
        {
            EndDay();
        }

        OnShopUpdated?.Invoke();
    }

    private void StartNewDay()
    {
        elapsedGameMinutes = 0f;
        dayEnded = false;
        IsShopOpen = true;

        Debug.Log("Day " + currentDay + " Started!");

        OnShopUpdated?.Invoke();
    }

    private void EndDay()
    {
        if (dayEnded)
            return;

        dayEnded = true;
        IsShopOpen = false;

        // เพิ่มค่าเช่าของวันนี้เข้าไปในยอดค้าง
        unpaidRent += dailyRent;

        Debug.Log("Day " + currentDay + " Ended!");

        // ตรวจสอบเงิน
        if (money >= unpaidRent)
        {
            money -= unpaidRent;

            Debug.Log("Rent Paid!");
            Debug.Log("Money Left: " + money);

            unpaidRent = 0;
            debtDays = 0;
        }
        else
        {
            debtDays++;

            Debug.Log("Rent Unpaid!");
            Debug.Log("Debt: " + unpaidRent);

            if (debtDays > maxDebtDays)
            {
                GameOver();
                return;
            }

            Debug.Log("You have one more day to pay!");
        }

        OnDayEnded?.Invoke();
        OnShopUpdated?.Invoke();

        StartNextDay();
    }

    private void StartNextDay()
    {
        currentDay++;

        StartNewDay();
    }

    public void AddMoney(int amount)
    {
        if (IsGameOver || amount <= 0)
            return;

        money += amount;

        Debug.Log("Earned: " + amount);
        Debug.Log("Total Money: " + money);

        OnShopUpdated?.Invoke();
    }

    public bool TrySpendMoney(int amount)
    {
        if (IsGameOver || amount <= 0 || money < amount)
            return false;

        money -= amount;

        OnShopUpdated?.Invoke();

        return true;
    }

    private void GameOver()
    {
        IsGameOver = true;
        IsShopOpen = false;

        Debug.Log("BANKRUPTCY! GAME OVER!");

        OnBankrupt?.Invoke();

        if (!string.IsNullOrEmpty(gameOverSceneName))
        {
            SceneManager.LoadScene(gameOverSceneName);
        }
    }
}
