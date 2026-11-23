
using UnityEngine;
using TMPro;

public class ShopHUD : MonoBehaviour
{
    public TMP_Text dayText;
    public TMP_Text timeText;
    public TMP_Text moneyText;
    public TMP_Text rentText;
    public TMP_Text statusText;

    private ShopDayManager shop;

    private void Start()
    {
        shop = ShopDayManager.Instance;
        UpdateUI();
    }

    private void Update()
    {
        if (shop == null)
        {
            shop = ShopDayManager.Instance;
            return;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (shop == null) return;

        dayText.text = "DAY " + shop.currentDay;

        timeText.text = shop.CurrentTime;

        moneyText.text = "$ " + shop.money;

        rentText.text = "Rent: $ " +
            (shop.unpaidRent + shop.dailyRent);

        if (shop.IsShopOpen)
        {
            statusText.text = "SHOP OPEN";
            statusText.color = Color.green;
        }
        else
        {
            statusText.text = "SHOP CLOSED";
            statusText.color = Color.red;
        }
    }
}
