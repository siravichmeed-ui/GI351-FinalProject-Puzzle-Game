
using UnityEngine;
using System.Collections.Generic;

public static class ItemSessionData
{
    public static string CurrentItemID { get; private set; }
    public static string CurrentItemName { get; private set; }
    public static Sprite CurrentItemSprite { get; private set; }

    public static bool IsCurseFound { get; private set; }
    public static bool IsPuzzleSolved { get; private set; }

    private const string SaveKey = "CursedShop_ItemProgress";

    [System.Serializable]
    private class ItemProgress
    {
        public string itemID;
        public bool curseFound;
        public bool puzzleSolved;
    }

    [System.Serializable]
    private class SaveData
    {
        public List<ItemProgress> items = new List<ItemProgress>();
    }

    private static Dictionary<string, ItemProgress> itemProgress =
        new Dictionary<string, ItemProgress>();

    private static bool hasLoaded = false;

    private static void LoadProgress()
    {
        if (hasLoaded)
            return;

        hasLoaded = true;
        itemProgress.Clear();

        if (!PlayerPrefs.HasKey(SaveKey))
            return;

        string json = PlayerPrefs.GetString(SaveKey);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        if (data == null || data.items == null)
            return;

        foreach (ItemProgress item in data.items)
        {
            if (item != null && !string.IsNullOrEmpty(item.itemID))
            {
                itemProgress[item.itemID] = item;
            }
        }

        Debug.Log("Loaded Item Progress: " + itemProgress.Count);
    }

    private static void SaveProgress()
    {
        SaveData data = new SaveData();

        foreach (ItemProgress item in itemProgress.Values)
        {
            data.items.Add(item);
        }

        string json = JsonUtility.ToJson(data);

        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();

        Debug.Log("Item Progress Saved!");
    }

    public static void SetCurrentItem(string itemID, string itemName, Sprite itemSprite)
    {
        LoadProgress();

        if (string.IsNullOrEmpty(itemID))
        {
            Debug.LogError("Item ID cannot be empty!");
            return;
        }

        CurrentItemID = itemID;
        CurrentItemName = itemName;
        CurrentItemSprite = itemSprite;

        if (!itemProgress.ContainsKey(itemID))
        {
            itemProgress.Add(itemID, new ItemProgress
            {
                itemID = itemID,
                curseFound = false,
                puzzleSolved = false
            });
        }

        ItemProgress progress = itemProgress[itemID];

        IsCurseFound = progress.curseFound;
        IsPuzzleSolved = progress.puzzleSolved;

        Debug.Log("Current Item: " + CurrentItemName);
        Debug.Log("Item ID: " + CurrentItemID);
        Debug.Log("Puzzle Solved: " + IsPuzzleSolved);
    }

    public static void MarkCurseFound()
    {
        LoadProgress();

        if (!HasCurrentItem())
        {
            Debug.LogError("No Current Item!");
            return;
        }

        ItemProgress progress = GetCurrentProgress();

        progress.curseFound = true;
        IsCurseFound = true;

        SaveProgress();
    }

    public static void MarkPuzzleSolved()
    {
        LoadProgress();

        if (!HasCurrentItem())
        {
            Debug.LogError("No Current Item!");
            return;
        }

        ItemProgress progress = GetCurrentProgress();

        progress.curseFound = true;
        progress.puzzleSolved = true;

        IsCurseFound = true;
        IsPuzzleSolved = true;

        SaveProgress();

        Debug.Log("Puzzle Solved: " + CurrentItemName);
    }

    public static bool IsPuzzleSolvedFor(string itemID)
    {
        LoadProgress();

        if (string.IsNullOrEmpty(itemID))
            return false;

        if (!itemProgress.ContainsKey(itemID))
            return false;

        return itemProgress[itemID].puzzleSolved;
    }

    public static bool IsCurseFoundFor(string itemID)
    {
        LoadProgress();

        if (string.IsNullOrEmpty(itemID))
            return false;

        if (!itemProgress.ContainsKey(itemID))
            return false;

        return itemProgress[itemID].curseFound;
    }

    private static ItemProgress GetCurrentProgress()
    {
        if (!itemProgress.ContainsKey(CurrentItemID))
        {
            itemProgress.Add(CurrentItemID, new ItemProgress
            {
                itemID = CurrentItemID,
                curseFound = false,
                puzzleSolved = false
            });
        }

        return itemProgress[CurrentItemID];
    }

    public static bool HasCurrentItem()
    {
        return !string.IsNullOrEmpty(CurrentItemID);
    }
#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ResetOnPlayMode()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        itemProgress.Clear();

        CurrentItemID = null;
        CurrentItemName = null;
        CurrentItemSprite = null;

        IsCurseFound = false;
        IsPuzzleSolved = false;

        hasLoaded = true;

        Debug.Log("All Game Data Cleared!");
    }
#endif
}
