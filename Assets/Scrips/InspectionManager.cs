
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

    private bool isInspectionMode = false;
    private bool isPuzzleOpened = false;

    private GameObject currentPuzzle;
    private PipeManager currentPipeManager;

    // =====================================
    // START
    // =====================================

    void Start()
    {
        if (cursePoint != null)
        {
            cursePoint.SetActive(false);
        }
    }

    // =====================================
    // UPDATE
    // =====================================

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) && !isPuzzleOpened)
        {
            ToggleInspectionMode();
        }
    }

    // =====================================
    // INSPECTION MODE
    // =====================================

    public void ToggleInspectionMode()
    {
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

        if (puzzlePrefab == null)
        {
            Debug.LogError("Puzzle Prefab is not assigned!");
            return;
        }

        if (!ItemSessionData.HasCurrentItem())
        {
            Debug.LogError("No Current Item Found!");
            return;
        }

        ItemSessionData.MarkCurseFound();

        isInspectionMode = false;
        isPuzzleOpened = true;

        if (cursePoint != null)
        {
            cursePoint.SetActive(false);
        }

        Vector3 spawnPosition = puzzleSpawnPoint != null
            ? puzzleSpawnPoint.position
            : Vector3.zero;

        currentPuzzle = Instantiate(
            puzzlePrefab,
            spawnPosition,
            Quaternion.identity
        );

        currentPipeManager =
            currentPuzzle.GetComponentInChildren<PipeManager>();

        if (currentPipeManager == null)
        {
            Debug.LogError("PipeManager not found in Puzzle Prefab!");

            Destroy(currentPuzzle);
            currentPuzzle = null;
            isPuzzleOpened = false;

            return;
        }

        currentPipeManager.OnPuzzleSolved += HandlePuzzleSolved;

        Debug.Log("Puzzle Opened!");
    }

    // =====================================
    // PUZZLE SOLVED
    // =====================================

    private void HandlePuzzleSolved()
    {
        if (!isPuzzleOpened)
            return;

        Debug.Log("Puzzle Solved Successfully!");

        // บันทึกสถานะไอเทม
        ItemSessionData.MarkPuzzleSolved();

        // ปิด Puzzle Prefab
        if (currentPipeManager != null)
        {
            currentPipeManager.OnPuzzleSolved -= HandlePuzzleSolved;
        }

        if (currentPuzzle != null)
        {
            Destroy(currentPuzzle);
        }

        currentPuzzle = null;
        currentPipeManager = null;

        isPuzzleOpened = false;

        // กลับไป ShopScene
        Debug.Log("Returning to Shop Scene...");

        SceneManager.LoadScene(shopSceneName);
    }

}
