
using UnityEngine;
using System;

public class ItemPersistenceData : MonoBehaviour
{
    [Header("Original Item Data")]
    public ItemData itemData;

    [Header("Unique Instance ID")]
    public string instanceID;

    [Header("Restore")]
    public bool restoredOnBoard;

    public void Initialize(ItemData data)
    {
        itemData = data;
        instanceID = Guid.NewGuid().ToString("N");
        restoredOnBoard = false;
    }
}
