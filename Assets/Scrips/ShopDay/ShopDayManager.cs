using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ShopPhase
{
    Open,      // ร้านเปิด นาฬิกาเดิน ลูกค้าเข้า
    Closing,   // หมดเวลา รอลูกค้าที่ค้างอยู่ให้เสร็จ
    Summary,   // แสดงสรุปวัน (เกมหยุด)
    GameOver   // ล้มละลาย รอผู้เล่นกดต่อ
}

/// <summary>ผลสรุปของวันที่เพิ่งจบ (ใช้แสดงใน DaySummaryPanel)</summary>
public class DaySummary
{
    public int day;
    public int earnings;
    public int rentDue;
    public bool rentPaid;
    public int moneyAfter;
    public int unpaidRent;
    public int debtDays;
    public int graceDaysLeft;
    public bool bankrupt;
}

/// <summary>
/// ระบบวัน-เวลา-ค่าเช่า
///  - เงินอยู่ที่ ShopMoneyManager (กระเป๋าเดียว) ลูกค้าจ่าย/ค่าเช่าหักที่เดียวกัน
///  - ร้านเปิด 08:00-18:00 (ค่าเริ่มต้น 1 วินาทีจริง = 1 นาทีในเกม)
///  - หมดเวลา: ไม่รับลูกค้าใหม่ -> รอคนที่ค้างอยู่ -> หักค่าเช่า -> สรุปวัน -> วันใหม่
///  - จ่ายไม่ไหวเกิน maxDebtDays วัน = ล้มละลาย (Game Over)
/// ทำงานเฉพาะในซีนร้าน (นาฬิกาหยุดเมื่ออยู่ในซีนล้างคำสาป) และเซฟ/โหลดผ่าน ShopSaveManager
/// </summary>
public class ShopDayManager : MonoBehaviour
{
    public static ShopDayManager Instance { get; private set; }

    [Header("Day Settings")]
    public int currentDay = 1;

    [Header("Shop Time")]
    public int openHour = 8;
    public int closeHour = 18;
    [Tooltip("กี่วินาทีจริงต่อ 1 นาทีในเกม (1 = วันละ 10 นาทีจริง)")]
    public float secondsPerGameMinute = 1f;
    [Tooltip("หลังปิดร้าน รอลูกค้าที่ค้างอยู่ได้นานสุดกี่วินาทีจริง")]
    public float closingGraceSeconds = 30f;

    [Header("Rent")]
    public int dailyRent = 1000;
    public int unpaidRent = 0;
    public int debtDays = 0;
    public int maxDebtDays = 1;

    [Header("Game Over")]
    public string gameOverSceneName = "GameOver";

    // ---------------------------------------------------------------

    public ShopPhase Phase { get; private set; } = ShopPhase.Open;
    public bool IsShopOpen => Phase == ShopPhase.Open;
    public bool IsGameOver => Phase == ShopPhase.GameOver;

    /// <summary>เงินปัจจุบัน — อ่านจากกระเป๋าเดียว (ShopMoneyManager)</summary>
    public int Money => ShopMoneyManager.Instance != null
        ? ShopMoneyManager.Instance.CurrentMoney
        : 0;

    public int TodayEarnings { get; private set; }
    public int OutstandingRent => unpaidRent;
    public int RentDueTonight => unpaidRent + dailyRent;
    public float ElapsedMinutes => elapsedGameMinutes;
    public DaySummary LastSummary { get; private set; }

    public float Progress01 =>
        Mathf.Clamp01(elapsedGameMinutes / Mathf.Max(TotalMinutes, 1f));

    private float TotalMinutes => (closeHour - openHour) * 60f;

    public string CurrentTime
    {
        get
        {
            int minutes = Mathf.Min(
                Mathf.FloorToInt(elapsedGameMinutes),
                Mathf.FloorToInt(TotalMinutes)
            );

            int total = openHour * 60 + minutes;

            return $"{total / 60:00}:{total % 60:00}";
        }
    }

    public event Action OnShopUpdated;
    public event Action OnDayStarted;
    public event Action OnShopClosing;
    public event Action OnDayEnded;
    public event Action OnBankrupt;
    public event Action<DaySummary> OnDaySummaryReady;

    private float elapsedGameMinutes;
    private float closingTimer;
    private CustomerSpawnManager spawnManager;

    // =====================================
    // LIFETIME
    // =====================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (Instance != this)
            return;

        spawnManager = FindFirstObjectByType<CustomerSpawnManager>();

        if (ShopMoneyManager.Instance != null)
            ShopMoneyManager.Instance.OnMoneyChanged += HandleMoneyChanged;
        else
            Debug.LogError("ShopDayManager: ไม่พบ ShopMoneyManager ในซีน (ต้องมีเพื่อใช้เป็นกระเป๋าเงิน)");

        // แผงสรุปวันสร้าง UI เองด้วยโค้ด ไม่ต้องตั้งค่าอะไรเพิ่ม
        if (FindFirstObjectByType<DaySummaryPanel>() == null)
            gameObject.AddComponent<DaySummaryPanel>();

        StartNewDay();
    }

    private void OnDestroy()
    {
        if (ShopMoneyManager.Instance != null)
            ShopMoneyManager.Instance.OnMoneyChanged -= HandleMoneyChanged;

        // กัน timeScale ค้าง 0 ถ้าออกจากซีนตอนแผงสรุปเปิดอยู่
        if (Instance == this &&
            (Phase == ShopPhase.Summary || Phase == ShopPhase.GameOver))
            Time.timeScale = 1f;

        if (Instance == this)
            Instance = null;
    }

    // =====================================
    // UPDATE
    // =====================================

    private void Update()
    {
        if (Phase == ShopPhase.Summary || Phase == ShopPhase.GameOver)
            return;

        // รอเซฟโหลดเสร็จก่อนค่อยเดินนาฬิกา
        if (ShopSaveManager.Instance != null &&
            ShopSaveManager.Instance.IsRestoring)
            return;

        if (Phase == ShopPhase.Open)
        {
            elapsedGameMinutes +=
                Time.deltaTime / Mathf.Max(secondsPerGameMinute, 0.01f);

            if (elapsedGameMinutes >= TotalMinutes)
                BeginClosing();

            OnShopUpdated?.Invoke();
        }
        else if (Phase == ShopPhase.Closing)
        {
            closingTimer += Time.deltaTime;

            bool busy = spawnManager != null && spawnManager.HasActiveCustomers;

            if (!busy || closingTimer >= closingGraceSeconds)
                EndDay();
        }
    }

    private void HandleMoneyChanged(int oldMoney, int newMoney)
    {
        // นับเฉพาะรายได้ (ค่าเช่าที่หักเป็นยอดลดลง ไม่นับ)
        if (newMoney > oldMoney &&
            (Phase == ShopPhase.Open || Phase == ShopPhase.Closing))
            TodayEarnings += newMoney - oldMoney;
    }

    // =====================================
    // DAY FLOW
    // =====================================

    private void StartNewDay()
    {
        elapsedGameMinutes = 0f;
        closingTimer = 0f;
        TodayEarnings = 0;
        Phase = ShopPhase.Open;

        Debug.Log("Day " + currentDay + " Started!");

        OnDayStarted?.Invoke();
        OnShopUpdated?.Invoke();
    }

    private void BeginClosing()
    {
        Phase = ShopPhase.Closing;
        elapsedGameMinutes = TotalMinutes;
        closingTimer = 0f;

        // ลูกค้าที่ยังไม่ได้ฝากของ ให้เดินออกไป
        if (spawnManager != null)
            spawnManager.DismissWaitingCustomer();

        Debug.Log("Shop is closing...");

        OnShopClosing?.Invoke();
        OnShopUpdated?.Invoke();
    }

    private void EndDay()
    {
        int endedDay = currentDay;
        int earned = TodayEarnings;
        int rentDue = unpaidRent + dailyRent;

        unpaidRent = rentDue;

        bool paid = false;
        bool bankrupt = false;

        if (unpaidRent <= 0)
        {
            paid = true;
        }
        else if (ShopMoneyManager.Instance != null &&
                 ShopMoneyManager.Instance.SpendMoney(unpaidRent))
        {
            paid = true;
            unpaidRent = 0;
            debtDays = 0;
        }
        else
        {
            debtDays++;
            bankrupt = debtDays > maxDebtDays;
        }

        LastSummary = new DaySummary
        {
            day = endedDay,
            earnings = earned,
            rentDue = rentDue,
            rentPaid = paid,
            moneyAfter = Money,
            unpaidRent = unpaidRent,
            debtDays = debtDays,
            graceDaysLeft = paid || bankrupt
                ? 0
                : Mathf.Max(0, maxDebtDays - debtDays + 1),
            bankrupt = bankrupt
        };

        Time.timeScale = 0f;

        if (bankrupt)
        {
            Phase = ShopPhase.GameOver;

            Debug.Log("BANKRUPTCY! GAME OVER!");

            OnBankrupt?.Invoke();
        }
        else
        {
            // เลื่อนเป็นวันถัดไปแล้วเซฟเลย (ปิดเกมระหว่างดูสรุป = ได้วันใหม่เริ่มต้น ค่าเช่าไม่ถูกหักซ้ำ)
            currentDay++;
            elapsedGameMinutes = 0f;
            closingTimer = 0f;
            TodayEarnings = 0;

            Phase = ShopPhase.Summary;

            if (ShopSaveManager.Instance != null)
                ShopSaveManager.Instance.SaveGame();
        }

        OnDayEnded?.Invoke();
        OnDaySummaryReady?.Invoke(LastSummary);
        OnShopUpdated?.Invoke();
    }

    // =====================================
    // CALLED BY SUMMARY PANEL
    // =====================================

    public void ConfirmNextDay()
    {
        if (Phase != ShopPhase.Summary)
            return;

        Time.timeScale = 1f;

        StartNewDay();

        if (ShopSaveManager.Instance != null)
            ShopSaveManager.Instance.SaveGame();
    }

    public void ConfirmGameOver()
    {
        if (Phase != ShopPhase.GameOver)
            return;

        Time.timeScale = 1f;

        // เริ่มใหม่หมดในรอบหน้า (ไฟล์เซฟ เงิน ความคืบหน้าสินค้า)
        if (ShopSaveManager.Instance != null)
            ShopSaveManager.Instance.ClearSave();

        if (!string.IsNullOrEmpty(gameOverSceneName))
            SceneManager.LoadScene(gameOverSceneName);
        else
            SceneManager.LoadScene(0);
    }

    // =====================================
    // MONEY (ส่งต่อไปกระเป๋าเดียว)
    // =====================================

    public void AddMoney(int amount)
    {
        if (ShopMoneyManager.Instance != null)
            ShopMoneyManager.Instance.AddMoney(amount);
    }

    public bool TrySpendMoney(int amount)
    {
        return ShopMoneyManager.Instance != null &&
               ShopMoneyManager.Instance.SpendMoney(amount);
    }

    // =====================================
    // SAVE / RESTORE
    // =====================================

    public void CaptureState(ShopSaveData data)
    {
        if (data == null)
            return;

        data.day = new DaySaveData
        {
            currentDay = currentDay,
            elapsedGameMinutes = elapsedGameMinutes,
            unpaidRent = unpaidRent,
            debtDays = debtDays,
            todayEarnings = TodayEarnings
        };
    }

    public void RestoreState(DaySaveData saved)
    {
        // เซฟเก่าที่ไม่มีข้อมูลวัน -> เริ่มวันที่ 1 ตามปกติ
        if (saved == null || saved.currentDay <= 0)
            return;

        currentDay = saved.currentDay;
        elapsedGameMinutes = Mathf.Clamp(saved.elapsedGameMinutes, 0f, TotalMinutes);
        unpaidRent = Mathf.Max(0, saved.unpaidRent);
        debtDays = Mathf.Max(0, saved.debtDays);
        TodayEarnings = Mathf.Max(0, saved.todayEarnings);

        Phase = ShopPhase.Open;
        closingTimer = 0f;

        OnShopUpdated?.Invoke();
    }
}
