
using UnityEngine;
using System.Collections.Generic;

public static class ItemSessionData
{
    // =====================================
    // CURRENT ITEM
    // =====================================

    public static string CurrentItemID { get; private set; }
    public static string CurrentItemInstanceID { get; private set; }
    public static string CurrentItemName { get; private set; }
    public static Sprite CurrentItemSprite { get; private set; }

    public static bool IsCurseFound { get; private set; }
    public static bool IsPuzzleSolved { get; private set; }

    // =====================================
    // PENDING ITEM
    // =====================================

    public static ItemData PendingItemData { get; private set; }
    public static string PendingInstanceID { get; private set; }

    public static Vector3 PendingItemPosition { get; private set; }
    public static Quaternion PendingItemRotation { get; private set; }

    public static bool HasPendingWorkAreaItem { get; private set; }

    // =====================================
    // SAVE DATA
    // =====================================

    private const string SaveKey = "CursedShop_InstanceProgress";

    [System.Serializable]
    private class ItemProgress
    {
        public string instanceID;
        public bool curseFound;
        public bool puzzleSolved;
    }

    [System.Serializable]
    private class SaveData
    {
        public List<ItemProgress> items = new List<ItemProgress>();
    }

    private static Dictionary<string, ItemProgress> progress =
        new Dictionary<string, ItemProgress>();

    private static bool hasLoaded;

    // =====================================
    // LOAD
    // =====================================

    private static void LoadProgress()
    {
        if (hasLoaded)
            return;

        hasLoaded = true;

        progress.Clear();

        if (!PlayerPrefs.HasKey(SaveKey))
            return;

        SaveData data = JsonUtility.FromJson<SaveData>(
            PlayerPrefs.GetString(SaveKey)
        );

        if (data == null || data.items == null)
            return;

        foreach (ItemProgress item in data.items)
        {
            if (item != null &&
                !string.IsNullOrEmpty(item.instanceID))
            {
                progress[item.instanceID] = item;
            }
        }
    }

    // =====================================
    // SAVE
    // =====================================

    private static void SaveProgress()
    {
        SaveData data = new SaveData();

        foreach (ItemProgress item in progress.Values)
        {
            data.items.Add(item);
        }

        PlayerPrefs.SetString(
            SaveKey,
            JsonUtility.ToJson(data)
        );

        PlayerPrefs.Save();
    }

    // =====================================
    // REGISTER NEW ITEM
    // =====================================

    public static void RegisterNewItem(string instanceID)
    {
        LoadProgress();

        if (string.IsNullOrEmpty(instanceID))
            return;

        progress[instanceID] = new ItemProgress
        {
            instanceID = instanceID,
            curseFound = false,
            puzzleSolved = false
        };

        SaveProgress();

        Debug.Log("Registered new Item: " + instanceID);
    }

    // =====================================
    // SET CURRENT ITEM
    // =====================================

    public static void SetCurrentItem(
        string instanceID,
        string itemID,
        string itemName,
        Sprite itemSprite)
    {
        LoadProgress();

        if (string.IsNullOrEmpty(instanceID))
        {
            Debug.LogError("Instance ID is missing!");
            return;
        }

        CurrentItemInstanceID = instanceID;
        CurrentItemID = itemID;
        CurrentItemName = itemName;
        CurrentItemSprite = itemSprite;

        if (!progress.ContainsKey(instanceID))
        {
            RegisterNewItem(instanceID);
        }

        ItemProgress item = progress[instanceID];

        IsCurseFound = item.curseFound;
        IsPuzzleSolved = item.puzzleSolved;
    }

    // =====================================
    // MARK CURSE FOUND
    // =====================================


    // รองรับโค้ดเดิมที่เรียกโดยไม่ส่ง instanceID
    // ใช้ Instance ID ของ Item ที่กำลังเปิดอยู่
    public static void MarkCurseFound()
    {
        MarkCurseFound(CurrentItemInstanceID);
    }

    public static void MarkPuzzleSolved()
    {
        MarkPuzzleSolved(CurrentItemInstanceID);
    }

    public static void MarkCurseFound(string instanceID)
    {
        LoadProgress();

        if (string.IsNullOrEmpty(instanceID))
            return;

        if (!progress.ContainsKey(instanceID))
        {
            progress[instanceID] = new ItemProgress
            {
                instanceID = instanceID,
                curseFound = false,
                puzzleSolved = false
            };
        }

        progress[instanceID].curseFound = true;

        if (CurrentItemInstanceID == instanceID)
            IsCurseFound = true;

        SaveProgress();
    }

    public static void MarkPuzzleSolved(string instanceID)
    {
        LoadProgress();

        if (string.IsNullOrEmpty(instanceID))
            return;

        if (!progress.ContainsKey(instanceID))
        {
            progress[instanceID] = new ItemProgress
            {
                instanceID = instanceID,
                curseFound = false,
                puzzleSolved = false
            };
        }

        progress[instanceID].curseFound = true;
        progress[instanceID].puzzleSolved = true;

        if (CurrentItemInstanceID == instanceID)
        {
            IsCurseFound = true;
            IsPuzzleSolved = true;
        }

        SaveProgress();
    }


    // =====================================
    // CHECK PUZZLE
    // =====================================

    public static bool IsPuzzleSolvedFor(string instanceID)
    {
        LoadProgress();

        return !string.IsNullOrEmpty(instanceID) &&
               progress.ContainsKey(instanceID) &&
               progress[instanceID].puzzleSolved;
    }

    public static bool IsCurseFoundFor(string instanceID)
    {
        LoadProgress();

        return !string.IsNullOrEmpty(instanceID) &&
               progress.ContainsKey(instanceID) &&
               progress[instanceID].curseFound;
    }

    // =====================================
    // CURRENT PROGRESS
    // =====================================

    private static ItemProgress GetCurrentProgress()
    {
        if (!progress.ContainsKey(CurrentItemInstanceID))
        {
            progress[CurrentItemInstanceID] = new ItemProgress
            {
                instanceID = CurrentItemInstanceID,
                curseFound = false,
                puzzleSolved = false
            };
        }

        return progress[CurrentItemInstanceID];
    }

    public static bool HasCurrentItem()
    {
        return !string.IsNullOrEmpty(CurrentItemInstanceID);
    }

    // =====================================
    // SAVE PENDING ITEM
    // =====================================

    public static void SavePendingWorkAreaItem(
        ItemData data,
        string instanceID,
        Vector3 position,
        Quaternion rotation)
    {
        if (data == null ||
            string.IsNullOrEmpty(instanceID))
        {
            Debug.LogError("Cannot save pending Item!");
            return;
        }

        PendingItemData = data;
        PendingInstanceID = instanceID;

        PendingItemPosition = position;
        PendingItemRotation = rotation;

        HasPendingWorkAreaItem = true;
    }

    // =====================================
    // CLEAR PENDING ITEM
    // =====================================

    public static void ClearPendingWorkAreaItem()
    {
        PendingItemData = null;
        PendingInstanceID = null;

        HasPendingWorkAreaItem = false;
    }


    public static void ResetAllProgress()
    {
        progress.Clear();
        hasLoaded = true;

        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();

        CurrentItemID = null;
        CurrentItemInstanceID = null;
        CurrentItemName = null;
        CurrentItemSprite = null;

        IsCurseFound = false;
        IsPuzzleSolved = false;

        PendingItemData = null;
        PendingInstanceID = null;
        PendingItemPosition = Vector3.zero;
        PendingItemRotation = Quaternion.identity;

        HasPendingWorkAreaItem = false;

        Debug.Log("All Item Progress Reset.");
    }

}
