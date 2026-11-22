using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public class ShopSaveManager : MonoBehaviour
{
    public static ShopSaveManager Instance { get; private set; }

    [Header("Item Database")]
    [Tooltip("ใส่ ItemData ทั้งหมดที่ใช้ในร้าน (ระบบจะเติมจาก CustomerData ให้อัตโนมัติ)")]
    public List<ItemData> itemDatabase = new List<ItemData>();

    [Header("References")]
    public WorkArea workArea;

    [Header("Save Settings")]
    public float autoSaveInterval = 3f;

    private const string FileName = "shop_save.json";
    private const int CurrentVersion = 2;

    private float autoSaveTimer;
    private bool isRestoring;

    private string SavePath =>
        Path.Combine(Application.persistentDataPath, FileName);

    public bool IsRestoring => isRestoring;
    public bool HasSaveFile => File.Exists(SavePath);

    // =====================================
    // AWAKE / START
    // =====================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (workArea == null)
            workArea = FindFirstObjectByType<WorkArea>();

        BuildItemDatabaseFromCustomers();
    }

    private void Start()
    {
        LoadGame();
    }

    private void Update()
    {
        if (autoSaveInterval <= 0f || isRestoring)
            return;

        autoSaveTimer += Time.unscaledDeltaTime;

        if (autoSaveTimer >= autoSaveInterval)
        {
            autoSaveTimer = 0f;
            SaveGame();
        }
    }

    // =====================================
    // ITEM DATABASE
    // =====================================

    private void BuildItemDatabaseFromCustomers()
    {
        CustomerSpawnManager spawnManager =
            FindFirstObjectByType<CustomerSpawnManager>();

        if (spawnManager == null || spawnManager.customers == null)
            return;

        foreach (CustomerData customer in spawnManager.customers)
        {
            if (customer == null || customer.possibleItems == null)
                continue;

            foreach (ItemData item in customer.possibleItems)
            {
                if (item != null && !itemDatabase.Contains(item))
                    itemDatabase.Add(item);
            }
        }
    }

    private ItemData FindItemData(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        foreach (ItemData item in itemDatabase)
        {
            if (item != null && item.itemID == id)
                return item;
        }

        return null;
    }

    // =====================================
    // SAVE
    // =====================================

    public void SaveGame()
    {
        if (isRestoring)
            return;

        ShopSaveData data = new ShopSaveData { version = CurrentVersion };

        CustomerSpawnManager customerManager =
            FindFirstObjectByType<CustomerSpawnManager>();

        if (customerManager != null)
            customerManager.CaptureState(data);

        foreach (ItemPersistenceData persistence in ItemPersistenceData.All)
        {
            if (persistence == null ||
                persistence.itemData == null ||
                string.IsNullOrEmpty(persistence.instanceID))
                continue;

            if (customerManager != null &&
                customerManager.ShouldExcludeItemFromSave(persistence.instanceID))
                continue;

            Transform t = persistence.transform;
            Rigidbody2D rb = persistence.GetComponent<Rigidbody2D>();

            ItemSaveData saved = new ItemSaveData
            {
                customerID = persistence.customerID,
                itemID = persistence.itemData.itemID,
                instanceID = persistence.instanceID,

                posX = t.position.x,
                posY = t.position.y,
                posZ = t.position.z,

                rotX = t.rotation.x,
                rotY = t.rotation.y,
                rotZ = t.rotation.z,
                rotW = t.rotation.w,

                scaleX = t.localScale.x,
                scaleY = t.localScale.y,
                scaleZ = t.localScale.z,

                wasOnWorkArea =
                    workArea != null &&
                    workArea.IsCurrentItem(persistence.gameObject),

                curseFound = ItemSessionData.IsCurseFoundFor(persistence.instanceID),
                puzzleSolved = ItemSessionData.IsPuzzleSolvedFor(persistence.instanceID)
            };

            if (rb != null)
            {
                saved.velocityX = rb.linearVelocity.x;
                saved.velocityY = rb.linearVelocity.y;
                saved.angularVelocity = rb.angularVelocity;
                saved.gravityScale = rb.gravityScale;
                saved.bodyType = (int)rb.bodyType;
            }

            data.items.Add(saved);
        }

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }
        catch (System.Exception exception)
        {
            Debug.LogError("Shop Save Failed: " + exception.Message);
        }
    }

    // =====================================
    // LOAD
    // =====================================

    public void LoadGame()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("No Shop Save Found. Starting New Game.");
            return;
        }

        isRestoring = true;

        try
        {
            ShopSaveData data =
                JsonUtility.FromJson<ShopSaveData>(File.ReadAllText(SavePath));

            if (data == null || data.items == null)
            {
                Debug.LogWarning("Shop Save Data is Invalid.");
                isRestoring = false;
                return;
            }

            foreach (ItemPersistenceData existing in
                     new List<ItemPersistenceData>(ItemPersistenceData.All))
            {
                if (existing != null)
                    Destroy(existing.gameObject);
            }

            StartCoroutine(RestoreItems(data));
        }
        catch (System.Exception exception)
        {
            Debug.LogError("Shop Load Failed: " + exception.Message);
            isRestoring = false;
        }
    }

    private IEnumerator RestoreItems(ShopSaveData data)
    {
        // รอให้ของเก่าถูกทำลาย
        yield return null;

        foreach (ItemSaveData saved in data.items)
        {
            if (saved == null)
                continue;

            ItemData itemData = FindItemData(saved.itemID);

            if (itemData == null || itemData.itemPrefab == null)
            {
                Debug.LogWarning("Cannot find ItemData: " + saved.itemID);
                continue;
            }

            GameObject itemObject = Instantiate(
                itemData.itemPrefab,
                new Vector3(saved.posX, saved.posY, saved.posZ),
                new Quaternion(saved.rotX, saved.rotY, saved.rotZ, saved.rotW)
            );

            itemObject.transform.localScale =
                new Vector3(saved.scaleX, saved.scaleY, saved.scaleZ);

            ItemPersistenceData persistence =
                itemObject.GetComponent<ItemPersistenceData>();

            if (persistence == null)
                persistence = itemObject.AddComponent<ItemPersistenceData>();

            // ใช้ identity เดิมทั้งหมด
            persistence.customerID = saved.customerID;
            persistence.itemData = itemData;
            persistence.instanceID = saved.instanceID;
            persistence.restoredOnBoard = true;

            DraggableItem draggable = itemObject.GetComponent<DraggableItem>();

            if (draggable != null)
            {
                draggable.itemID = itemData.itemID;
                draggable.itemName = itemData.itemName;
                draggable.cursedSprite = itemData.cursedSprite;
                draggable.cleanedSprite = itemData.cleanedSprite;
                draggable.workArea = workArea;

                draggable.RestoreAfterLoad(saved.wasOnWorkArea);
            }

            SpriteRenderer spriteRenderer =
                itemObject.GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = saved.puzzleSolved
                    ? itemData.cleanedSprite
                    : itemData.cursedSprite;
            }

            Rigidbody2D rb = itemObject.GetComponent<Rigidbody2D>();

            if (rb != null)
            {
                // เซฟตอนลาก/ตอนกำลังส่งของ body จะเป็น Kinematic gravity 0
                // ตอนโหลดไม่มีใครลากอยู่ ให้กลับเป็น Dynamic ปกติ กันของลอยค้าง
                bool wasKinematic =
                    saved.bodyType == (int)RigidbodyType2D.Kinematic;

                rb.bodyType = wasKinematic
                    ? RigidbodyType2D.Dynamic
                    : (RigidbodyType2D)saved.bodyType;

                rb.gravityScale = wasKinematic && draggable != null
                    ? draggable.OriginalGravityScale
                    : saved.gravityScale;

                rb.linearVelocity = wasKinematic
                    ? Vector2.zero
                    : new Vector2(saved.velocityX, saved.velocityY);

                rb.angularVelocity = wasKinematic ? 0f : saved.angularVelocity;
            }

            if (saved.wasOnWorkArea && workArea != null)
            {
                if (!workArea.TryPlaceItem(itemObject))
                {
                    Debug.LogWarning(
                        "Could not restore Item to WorkArea: " + itemData.itemName
                    );
                }
            }

            if (saved.curseFound)
                ItemSessionData.MarkCurseFound(saved.instanceID);

            if (saved.puzzleSolved)
                ItemSessionData.MarkPuzzleSolved(saved.instanceID);
        }

        // ออเดอร์ที่รอรับคืน + ลูกค้าที่อยู่ในร้าน
        CustomerSpawnManager customerManager =
            FindFirstObjectByType<CustomerSpawnManager>();

        if (customerManager != null)
            customerManager.RestoreState(data);

        // ล้างความคืบหน้าของสินค้าที่ไม่มีอยู่ในเซฟแล้ว (ถูกลูกค้ารับไปแล้ว)
        HashSet<string> keep = new HashSet<string>();

        foreach (ItemSaveData saved in data.items)
        {
            if (saved != null && !string.IsNullOrEmpty(saved.instanceID))
                keep.Add(saved.instanceID);
        }

        ItemSessionData.PruneExcept(keep);

        ItemSessionData.ClearPendingWorkAreaItem();

        isRestoring = false;

        Debug.Log("Shop Restore Completed. Items: " + data.items.Count);
    }

    // =====================================
    // RESET
    // =====================================

    public void ResetGameSave()
    {
        if (isRestoring)
        {
            Debug.LogWarning("Cannot reset while restoring!");
            return;
        }

        try
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);

            ItemSessionData.ResetAllProgress();

            if (ShopMoneyManager.Instance != null)
                ShopMoneyManager.Instance.ResetMoney();

            autoSaveTimer = 0f;

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("Reset Game Failed: " + exception.Message);
        }
    }

    // =====================================
    // APPLICATION EVENTS
    // =====================================

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            SaveGame();
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
