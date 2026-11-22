
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
    [Tooltip("ใส่ ItemData ทั้งหมดที่ใช้ในร้าน")]
    public List<ItemData> itemDatabase = new List<ItemData>();

    [Header("References")]
    public WorkArea workArea;

    [Header("Save Settings")]
    public float autoSaveInterval = 3f;

    private const string FileName = "shop_save.json";

    private float autoSaveTimer;
    private bool isRestoring;

    private string SavePath =>
        Path.Combine(Application.persistentDataPath, FileName);

    public bool IsRestoring => isRestoring;
    public bool HasSaveFile => File.Exists(SavePath);

    // =====================================
    // AWAKE
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
            workArea = FindObjectOfType<WorkArea>();

        BuildItemDatabaseFromCustomers();
    }

    // =====================================
    // START
    // =====================================

    private void Start()
    {
        LoadGame();
    }

    // =====================================
    // UPDATE / AUTO SAVE
    // =====================================

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
            FindObjectOfType<CustomerSpawnManager>();

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
    // SAVE GAME
    // =====================================

    public void SaveGame()
    {
        if (isRestoring)
            return;

        ShopSaveData data = new ShopSaveData();

        // SAVE CUSTOMER
        CustomerSpawnManager customerManager =
            FindObjectOfType<CustomerSpawnManager>();

        if (customerManager != null)
        {
            data.customer =
                customerManager.CaptureCustomerState();

            if (customerManager.CurrentCustomer != null)
            {
                CustomerMovement movement =
                    customerManager.CurrentCustomer
                    .GetComponent<CustomerMovement>();

                if (movement != null)
                {
                    data.customerMovement =
                        movement.CaptureMovementState();
                }
            }
        }

        // SAVE ALL ITEMS
        ItemPersistenceData[] allItems =
            FindObjectsOfType<ItemPersistenceData>(true);

        foreach (ItemPersistenceData persistence in allItems)
        {
            if (persistence == null ||
                persistence.itemData == null ||
                string.IsNullOrEmpty(persistence.instanceID))
                continue;

            Transform itemTransform = persistence.transform;

            Rigidbody2D rb =
                persistence.GetComponent<Rigidbody2D>();

            ItemSaveData savedItem = new ItemSaveData();

            savedItem.itemID = persistence.itemData.itemID;
            savedItem.instanceID = persistence.instanceID;

            Vector3 position = itemTransform.position;
            Quaternion rotation = itemTransform.rotation;
            Vector3 scale = itemTransform.localScale;

            savedItem.posX = position.x;
            savedItem.posY = position.y;
            savedItem.posZ = position.z;

            savedItem.rotX = rotation.x;
            savedItem.rotY = rotation.y;
            savedItem.rotZ = rotation.z;
            savedItem.rotW = rotation.w;

            savedItem.scaleX = scale.x;
            savedItem.scaleY = scale.y;
            savedItem.scaleZ = scale.z;

            savedItem.wasOnWorkArea =
                workArea != null &&
                workArea.IsCurrentItem(persistence.gameObject);

            savedItem.curseFound =
                ItemSessionData.IsCurseFoundFor(
                    persistence.instanceID
                );

            savedItem.puzzleSolved =
                ItemSessionData.IsPuzzleSolvedFor(
                    persistence.instanceID
                );

            if (rb != null)
            {
                savedItem.velocityX = rb.linearVelocity.x;
                savedItem.velocityY = rb.linearVelocity.y;
                savedItem.angularVelocity = rb.angularVelocity;
                savedItem.gravityScale = rb.gravityScale;
                savedItem.bodyType = (int)rb.bodyType;
            }

            data.items.Add(savedItem);
        }

        // WRITE JSON FILE
        try
        {
            string json = JsonUtility.ToJson(data, true);

            File.WriteAllText(SavePath, json);

            Debug.Log("Shop Game Saved Successfully.");
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "Shop Save Failed: " + exception.Message
            );
        }
    }

    // =====================================
    // LOAD GAME
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
            string json = File.ReadAllText(SavePath);

            ShopSaveData data =
                JsonUtility.FromJson<ShopSaveData>(json);

            if (data == null || data.items == null)
            {
                Debug.LogWarning("Shop Save Data is Invalid.");
                isRestoring = false;
                return;
            }

            // REMOVE EXISTING ITEMS
            ItemPersistenceData[] existingItems =
                FindObjectsOfType<ItemPersistenceData>(true);

            foreach (ItemPersistenceData existing in existingItems)
            {
                if (existing != null)
                    Destroy(existing.gameObject);
            }

            StartCoroutine(RestoreItems(data));
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "Shop Load Failed: " + exception.Message
            );

            isRestoring = false;
        }
    }

    // =====================================
    // RESTORE ITEMS AND CUSTOMER
    // =====================================

    private IEnumerator RestoreItems(ShopSaveData data)
    {
        // WAIT FOR OLD ITEMS TO BE DESTROYED
        yield return null;

        // RESTORE ITEMS
        foreach (ItemSaveData savedItem in data.items)
        {
            if (savedItem == null)
                continue;

            ItemData itemData = FindItemData(savedItem.itemID);

            if (itemData == null || itemData.itemPrefab == null)
            {
                Debug.LogWarning(
                    "Cannot find ItemData: " + savedItem.itemID
                );

                continue;
            }

            Vector3 position = new Vector3(
                savedItem.posX,
                savedItem.posY,
                savedItem.posZ
            );

            Quaternion rotation = new Quaternion(
                savedItem.rotX,
                savedItem.rotY,
                savedItem.rotZ,
                savedItem.rotW
            );

            GameObject itemObject = Instantiate(
                itemData.itemPrefab,
                position,
                rotation
            );

            itemObject.transform.localScale = new Vector3(
                savedItem.scaleX,
                savedItem.scaleY,
                savedItem.scaleZ
            );

            ItemPersistenceData persistence =
                itemObject.GetComponent<ItemPersistenceData>();

            if (persistence == null)
            {
                persistence =
                    itemObject.AddComponent<ItemPersistenceData>();
            }

            // KEEP ORIGINAL INSTANCE ID
            persistence.itemData = itemData;
            persistence.instanceID = savedItem.instanceID;
            persistence.restoredOnBoard = true;

            DraggableItem draggable =
                itemObject.GetComponent<DraggableItem>();

            if (draggable != null)
            {
                draggable.itemID = itemData.itemID;
                draggable.itemName = itemData.itemName;
                draggable.cursedSprite = itemData.cursedSprite;
                draggable.cleanedSprite = itemData.cleanedSprite;
                draggable.workArea = workArea;

                draggable.RestoreAfterLoad(
                    savedItem.wasOnWorkArea
                );
            }

            // RESTORE SPRITE
            SpriteRenderer spriteRenderer =
                itemObject.GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = savedItem.puzzleSolved
                    ? itemData.cleanedSprite
                    : itemData.cursedSprite;
            }

            // RESTORE PHYSICS
            Rigidbody2D rb =
                itemObject.GetComponent<Rigidbody2D>();

            if (rb != null)
            {
                rb.bodyType =
                    (RigidbodyType2D)savedItem.bodyType;

                rb.gravityScale = savedItem.gravityScale;

                rb.linearVelocity = new Vector2(
                    savedItem.velocityX,
                    savedItem.velocityY
                );

                rb.angularVelocity =
                    savedItem.angularVelocity;
            }

            // RESTORE WORK AREA
            if (savedItem.wasOnWorkArea && workArea != null)
            {
                if (!workArea.TryPlaceItem(itemObject))
                {
                    Debug.LogWarning(
                        "Could not restore Item to WorkArea: " +
                        itemData.itemName
                    );
                }
            }

            // RESTORE PER-ITEM PROGRESS
            if (savedItem.curseFound)
            {
                ItemSessionData.MarkCurseFound(
                    savedItem.instanceID
                );
            }

            if (savedItem.puzzleSolved)
            {
                ItemSessionData.MarkPuzzleSolved(
                    savedItem.instanceID
                );
            }
        }

        // RESTORE CUSTOMER
        CustomerSpawnManager customerManager =
            FindObjectOfType<CustomerSpawnManager>();

        if (customerManager != null && data.customer != null)
        {
            customerManager.RestoreSavedCustomer(
                data.customer,
                data.customerMovement
            );
        }

        // CLEAR OLD PENDING ITEM
        ItemSessionData.ClearPendingWorkAreaItem();

        // FINISH RESTORE
        isRestoring = false;

        Debug.Log(
            "Shop Restore Completed. Items: " +
            data.items.Count
        );
    }

    // =====================================
    // RESET SAVE
    // =====================================

    public void ResetGameSave()
    {
        if (isRestoring)
        {
            Debug.LogWarning("Cannot reset while restoring!");
            return;
        }

        Debug.Log("Starting Game Reset...");

        try
        {
            // 1. ลบไฟล์ Save หลัก
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }

            // 2. ล้างข้อมูลความคืบหน้า Item ทั้งหมด
            ItemSessionData.ResetAllProgress();

            // 3. รีเซ็ต Timer Auto Save
            autoSaveTimer = 0f;

            // 4. โหลด Scene ปัจจุบันใหม่
            int currentScene =
                SceneManager.GetActiveScene().buildIndex;

            Debug.Log("Game Reset Completed. Reloading Scene...");

            SceneManager.LoadScene(currentScene);
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "Reset Game Failed: " + exception.Message
            );
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
        {
            Instance = null;
        }
    }
}
