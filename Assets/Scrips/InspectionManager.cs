
using UnityEngine;
using UnityEngine.SceneManagement;

public class InspectionManager : MonoBehaviour
{
    [Header("Inspection Settings")]
    public GameObject cursePoint;

    [Header("Puzzle Settings")]
    public GameObject puzzlePrefab;
    public Transform puzzleSpawnPoint;

    [Header("Scene Settings")]
    public string shopSceneName = "ShopScene";

    private bool isInspectionMode;
    private bool isPuzzleOpened;

    private GameObject currentPuzzle;
    private PipeManager currentPipeManager;

    // =====================================
    // START
    // =====================================

    void Start()
    {
        isInspectionMode = false;
        isPuzzleOpened = false;

        if (cursePoint != null)
        {
            cursePoint.SetActive(false);
        }

        // ตรวจสอบว่ามี Item ส่งมาหรือไม่
        if (!ItemSessionData.HasCurrentItem())
        {
            Debug.LogError(
                "InspectionManager: No Item Received!"
            );
            return;
        }

        Debug.Log(
            "Received Item: " +
            ItemSessionData.CurrentItemName +
            " | ID: " +
            ItemSessionData.CurrentItemID
        );

        if (ItemSessionData.IsPuzzleSolved)
        {
            Debug.Log(
                "This Item Puzzle has already been solved!"
            );
        }
    }

    // =====================================
    // UPDATE
    // =====================================

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) &&
            !isPuzzleOpened)
        {
            ToggleInspectionMode();
        }
    }

    // =====================================
    // INSPECTION MODE
    // =====================================

    public void ToggleInspectionMode()
    {
        if (isPuzzleOpened)
            return;

        if (!ItemSessionData.HasCurrentItem())
            return;

        if (ItemSessionData.IsPuzzleSolved)
        {
            Debug.Log("This Item is already cleaned!");
            return;
        }

        isInspectionMode = !isInspectionMode;

        if (cursePoint != null)
        {
            cursePoint.SetActive(isInspectionMode);
        }

        Debug.Log(
            isInspectionMode
            ? "Inspection Mode: ON"
            : "Inspection Mode: OFF"
        );
    }

    // =====================================
    // ENTER PUZZLE
    // =====================================

    public void EnterPuzzle()
    {
        if (!isInspectionMode || isPuzzleOpened)
            return;

        if (!ItemSessionData.HasCurrentItem())
        {
            Debug.LogError("No Current Item Found!");
            return;
        }

        if (ItemSessionData.IsPuzzleSolved)
        {
            Debug.Log("Item already solved!");
            return;
        }

        if (puzzlePrefab == null)
        {
            Debug.LogError(
                "Puzzle Prefab is not assigned!"
            );
            return;
        }

        // บันทึกว่าพบคำสาปแล้ว
        ItemSessionData.MarkCurseFound();

        isInspectionMode = false;
        isPuzzleOpened = true;

        if (cursePoint != null)
        {
            cursePoint.SetActive(false);
        }

        Vector3 spawnPosition =
            puzzleSpawnPoint != null
            ? puzzleSpawnPoint.position
            : Vector3.zero;

        // ใช้ Puzzle Prefab ตัวเดิมทุก Item
        currentPuzzle = Instantiate(
            puzzlePrefab,
            spawnPosition,
            Quaternion.identity
        );

        currentPipeManager =
            currentPuzzle.GetComponentInChildren<PipeManager>();

        if (currentPipeManager == null)
        {
            Debug.LogError(
                "PipeManager not found in Puzzle Prefab!"
            );

            Destroy(currentPuzzle);

            currentPuzzle = null;
            isPuzzleOpened = false;

            return;
        }

        currentPipeManager.OnPuzzleSolved +=
            HandlePuzzleSolved;

        Debug.Log(
            "Puzzle Opened For: " +
            ItemSessionData.CurrentItemName
        );
    }

    // =====================================
    // PUZZLE SOLVED
    // =====================================

    private void HandlePuzzleSolved()
    {
        if (!isPuzzleOpened)
            return;

        Debug.Log(
            "Puzzle Solved: " +
            ItemSessionData.CurrentItemName
        );

        // บันทึกความสำเร็จให้ Item ID ปัจจุบัน
        ItemSessionData.MarkPuzzleSolved();

        // ยกเลิก Event
        if (currentPipeManager != null)
        {
            currentPipeManager.OnPuzzleSolved -=
                HandlePuzzleSolved;
        }

        // ลบ Puzzle
        if (currentPuzzle != null)
        {
            Destroy(currentPuzzle);
        }

        currentPuzzle = null;
        currentPipeManager = null;

        isPuzzleOpened = false;

        // กลับ ShopScene
        SceneManager.LoadScene(shopSceneName);
    }

    // =====================================
    // CLEANUP
    // =====================================

    void OnDestroy()
    {
        if (currentPipeManager != null)
        {
            currentPipeManager.OnPuzzleSolved -=
                HandlePuzzleSolved;
        }
    }
}
