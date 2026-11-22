using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวตนของสินค้า 1 ชิ้น: customerID (เจ้าของ) + itemID (ชนิด) + instanceID (ชิ้นนี้ชิ้นเดียว)
/// </summary>
public class ItemPersistenceData : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("ลูกค้าเจ้าของสินค้าชิ้นนี้")]
    public string customerID;
    public ItemData itemData;
    [Tooltip("ID เฉพาะของสินค้าชิ้นนี้")]
    public string instanceID;

    [Header("Restore")]
    public bool restoredOnBoard;

    [Header("Puzzle Progress (ความจริงอยู่ที่ ItemSessionData)")]
    public bool puzzleSolved;

    public string ItemID => itemData != null ? itemData.itemID : string.Empty;

    // =====================================
    // REGISTRY  (แทน FindObjectsOfType)
    // =====================================

    private static readonly List<ItemPersistenceData> registry =
        new List<ItemPersistenceData>();

    public static IReadOnlyList<ItemPersistenceData> All => registry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => registry.Clear();

    public static ItemPersistenceData Find(string instanceID)
    {
        if (string.IsNullOrEmpty(instanceID))
            return null;

        foreach (ItemPersistenceData item in registry)
        {
            if (item != null && item.instanceID == instanceID)
                return item;
        }

        return null;
    }

    private void Awake()
    {
        if (!registry.Contains(this))
            registry.Add(this);
    }

    private void OnDestroy()
    {
        registry.Remove(this);
    }

    // =====================================
    // INIT
    // =====================================

    public void Initialize(string ownerCustomerID, ItemData data)
    {
        customerID = ownerCustomerID;
        itemData = data;
        instanceID = Guid.NewGuid().ToString("N");
        restoredOnBoard = false;
        puzzleSolved = false;
    }
}
