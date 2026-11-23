using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD ของร้าน: วัน เวลา เงิน ค่าเช่า สถานะ — ช่องไหนไม่ใส่ก็ข้าม</summary>
public class ShopHUD : MonoBehaviour
{
    public TMP_Text dayText;
    public TMP_Text timeText;
    [Tooltip("ถ้า ShopMoneyManager มีช่องเงินของตัวเองอยู่แล้ว (นับเลขวิ่ง) HUD จะไม่แตะช่องนี้")]
    public TMP_Text moneyText;
    public TMP_Text rentText;
    public TMP_Text statusText;

    [Header("Optional")]
    public TMP_Text earningsText;
    public TMP_Text debtText;
    [Tooltip("แถบความคืบหน้าของวัน (Image Type = Filled)")]
    public Image dayProgressFill;

    private ShopDayManager shop;

    private void Update()
    {
        if (shop == null)
        {
            shop = ShopDayManager.Instance;

            if (shop == null)
                return;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (dayText != null)
            dayText.text = "DAY " + shop.currentDay;

        if (timeText != null)
            timeText.text = shop.CurrentTime;

        ShopMoneyManager wallet = ShopMoneyManager.Instance;

        if (moneyText != null && wallet != null && !wallet.HasMoneyText)
            moneyText.text = "$ " + wallet.CurrentMoney.ToString("N0");

        if (rentText != null)
            rentText.text = "Rent: $ " + shop.RentDueTonight.ToString("N0");

        if (earningsText != null)
            earningsText.text = "Today: $ " + shop.TodayEarnings.ToString("N0");

        if (debtText != null)
        {
            if (shop.unpaidRent > 0)
            {
                debtText.text = "DEBT: $ " + shop.unpaidRent.ToString("N0");
                debtText.color = Color.red;
            }
            else
            {
                debtText.text = "";
            }
        }

        if (dayProgressFill != null)
            dayProgressFill.fillAmount = shop.Progress01;

        if (statusText != null)
        {
            switch (shop.Phase)
            {
                case ShopPhase.Open:
                    statusText.text = "SHOP OPEN";
                    statusText.color = Color.green;
                    break;

                case ShopPhase.Closing:
                    statusText.text = "CLOSING...";
                    statusText.color = new Color(1f, 0.8f, 0.2f);
                    break;

                default:
                    statusText.text = "SHOP CLOSED";
                    statusText.color = Color.red;
                    break;
            }
        }
    }
}
