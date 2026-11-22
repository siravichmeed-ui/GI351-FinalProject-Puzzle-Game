
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewCustomer",
    menuName = "Cursed Shop/Customer Data"
)]
public class CustomerData : ScriptableObject
{
    [Header("Customer Information")]
    public string customerID;
    public string customerName;

    [TextArea(2, 4)]
    public string customerMessage;

    [Header("Return Reward")]
    public int returnReward = 100;

    [Header("Customer Prefab")]
    public GameObject customerPrefab;

    [Header("Possible Items")]
    public List<ItemData> possibleItems = new List<ItemData>();
}
