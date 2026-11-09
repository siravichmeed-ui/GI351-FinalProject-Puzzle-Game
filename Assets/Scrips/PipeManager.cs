
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PipeManager : MonoBehaviour
{
    public static PipeManager Instance;

    public enum Difficulty
    {
        Random,
        Easy,
        Normal,
        Hard
    }

    [Header("Difficulty")]
    public Difficulty difficulty = Difficulty.Random;

    private int boardSize;
    private int minimumPathLength;

    [Header("Pipe Prefabs")]
    public GameObject straightPrefab;
    public GameObject cornerPrefab;
    public GameObject endPrefab;

    [Header("Board References")]
    public Transform boardParent;
    public Transform startPoint;
    public Transform endPoint;

    [Header("Board Settings")]
    public float spacing = 1.5f;

    private PipeTile[] pipes;

    private int startRow;
    private int endRow;

    private int startIndex;
    private int endIndex;

    public bool IsSolved { get; private set; }

    private Coroutine energyAnimation;

    public int BoardSize => boardSize;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GeneratePuzzle();
    }

    // =====================================
    // GENERATE PUZZLE
    // =====================================

    public void GeneratePuzzle()
    {
        GeneratePuzzle(difficulty);
    }

    public void GeneratePuzzle(Difficulty selectedDifficulty)
    {
        ClearBoard();

        IsSolved = false;

        // Select difficulty
        if (selectedDifficulty == Difficulty.Random)
        {
            selectedDifficulty =
                (Difficulty)Random.Range(1, 4);
        }

        // Set board size and path length
        switch (selectedDifficulty)
        {
            case Difficulty.Easy:
                boardSize = 3;
                minimumPathLength = 3;
                break;

            case Difficulty.Normal:
                boardSize = 5;
                minimumPathLength = 7;
                break;

            case Difficulty.Hard:
                boardSize = 5;
                minimumPathLength = 12;
                break;
        }

        int totalPipes = boardSize * boardSize;

        // Random Start and End rows
        startRow = Random.Range(0, boardSize);
        endRow = Random.Range(0, boardSize);

        startIndex = startRow * boardSize;
        endIndex = endRow * boardSize + (boardSize - 1);

        // Move Start and End markers
        float half = (boardSize - 1) * 0.5f;

        startPoint.localPosition = new Vector3(
            -(half + 0.75f) * spacing,
            (half - startRow) * spacing,
            0
        );

        endPoint.localPosition = new Vector3(
            (half + 0.75f) * spacing,
            (half - endRow) * spacing,
            0
        );

        // Find a valid path
        List<int> path = new List<int>();
        bool[] visited = new bool[totalPipes];

        visited[startIndex] = true;
        path.Add(startIndex);

        if (!FindPath(startIndex, visited, path))
        {
            Debug.LogError("Cannot find a valid path!");
            return;
        }

        // Create pipes
        pipes = new PipeTile[totalPipes];

        for (int i = 0; i < totalPipes; i++)
        {
            int pathIndex = path.IndexOf(i);

            if (pathIndex >= 0)
            {
                GeneratePathPipe(i, path, pathIndex);
            }
            else
            {
                GenerateRandomPipe(i);
            }
        }

        Debug.Log("New Puzzle Generated!");
        Debug.Log("Difficulty: " + selectedDifficulty);
        Debug.Log("Board Size: " + boardSize + " x " + boardSize);
        Debug.Log("Start Row: " + startRow);
        Debug.Log("End Row: " + endRow);
        Debug.Log("Path Length: " + path.Count);
        Debug.Log("Path: " + string.Join(" -> ", path));

        CheckConnection();
    }

    // =====================================
    // FIND RANDOM VALID PATH
    // =====================================

    private bool FindPath(
        int current,
        bool[] visited,
        List<int> path)
    {
        if (current == endIndex)
        {
            return path.Count >= minimumPathLength;
        }

        int row = current / boardSize;
        int col = current % boardSize;

        List<int> neighbors = new List<int>();

        if (row > 0)
            neighbors.Add(current - boardSize);

        if (col < boardSize - 1)
            neighbors.Add(current + 1);

        if (row < boardSize - 1)
            neighbors.Add(current + boardSize);

        if (col > 0)
            neighbors.Add(current - 1);

        // Shuffle neighbor order
        for (int i = 0; i < neighbors.Count; i++)
        {
            int random = Random.Range(i, neighbors.Count);

            int temp = neighbors[i];
            neighbors[i] = neighbors[random];
            neighbors[random] = temp;
        }

        foreach (int next in neighbors)
        {
            if (visited[next])
                continue;

            int nextCol = next % boardSize;

            // Do not enter another left boundary cell
            if (nextCol == 0 && next != startIndex)
                continue;

            // Do not enter another right boundary cell
            if (nextCol == boardSize - 1 && next != endIndex)
                continue;

            visited[next] = true;
            path.Add(next);

            if (FindPath(next, visited, path))
                return true;

            path.RemoveAt(path.Count - 1);
            visited[next] = false;
        }

        return false;
    }

    // =====================================
    // GENERATE CORRECT PATH PIPES
    // =====================================

    private void GeneratePathPipe(
        int index,
        List<int> path,
        int pathIndex)
    {
        int dirA;
        int dirB;

        if (pathIndex == 0)
        {
            // Start connects to the outside LEFT
            dirA = 3;

            dirB = GetDirection(
                index,
                path[pathIndex + 1]
            );
        }
        else if (pathIndex == path.Count - 1)
        {
            // End connects to the outside RIGHT
            dirA = 1;

            dirB = GetDirection(
                index,
                path[pathIndex - 1]
            );
        }
        else
        {
            // Middle pipe
            dirA = GetDirection(
                index,
                path[pathIndex - 1]
            );

            dirB = GetDirection(
                index,
                path[pathIndex + 1]
            );
        }

        // Select pipe type
        PipeTile.PipeType type;

        if ((dirA + 2) % 4 == dirB)
        {
            type = PipeTile.PipeType.Straight;
        }
        else
        {
            type = PipeTile.PipeType.Corner;
        }

        GameObject prefab = GetPrefab(type);

        GameObject obj = Instantiate(
            prefab,
            boardParent,
            false
        );

        PlaceTile(obj, index);

        PipeTile tile = obj.GetComponent<PipeTile>();

        pipes[index] = tile;

        // Find the correct rotation
        int correctRotation = FindCorrectRotation(
            tile,
            type,
            dirA,
            dirB
        );

        // Scramble the correct rotation
        int scramble;

        if (type == PipeTile.PipeType.Straight)
        {
            scramble = Random.Range(0, 2) == 0 ? 1 : 3;
        }
        else
        {
            scramble = Random.Range(1, 4);
        }

        tile.Initialize(
            type,
            (correctRotation + scramble) % 4
        );
    }

    // =====================================
    // GENERATE RANDOM EXTRA PIPES
    // =====================================

    private void GenerateRandomPipe(int index)
    {
        PipeTile.PipeType type =
            (PipeTile.PipeType)Random.Range(0, 3);

        GameObject prefab = GetPrefab(type);

        GameObject obj = Instantiate(
            prefab,
            boardParent,
            false
        );

        PlaceTile(obj, index);

        PipeTile tile = obj.GetComponent<PipeTile>();

        pipes[index] = tile;

        tile.Initialize(
            type,
            Random.Range(0, 4)
        );
    }

    // =====================================
    // FIND CORRECT ROTATION
    // =====================================

    private int FindCorrectRotation(
        PipeTile tile,
        PipeTile.PipeType type,
        int dirA,
        int dirB)
    {
        for (int r = 0; r < 4; r++)
        {
            tile.Initialize(type, r);

            bool hasA = tile.HasConnection(
                (PipeTile.Direction)dirA
            );

            bool hasB = tile.HasConnection(
                (PipeTile.Direction)dirB
            );

            if (hasA && hasB)
                return r;
        }

        return 0;
    }

    // =====================================
    // POSITION PIPES
    // =====================================

    private void PlaceTile(GameObject obj, int index)
    {
        int row = index / boardSize;
        int col = index % boardSize;

        float half = (boardSize - 1) * 0.5f;

        float x = (col - half) * spacing;
        float y = (half - row) * spacing;

        obj.transform.localPosition = new Vector3(
            x,
            y,
            0
        );
    }

    // =====================================
    // GET PREFAB
    // =====================================

    private GameObject GetPrefab(PipeTile.PipeType type)
    {
        switch (type)
        {
            case PipeTile.PipeType.Straight:
                return straightPrefab;

            case PipeTile.PipeType.Corner:
                return cornerPrefab;

            case PipeTile.PipeType.End:
                return endPrefab;
        }

        return null;
    }

    // =====================================
    // GET DIRECTION BETWEEN TWO CELLS
    // =====================================

    private int GetDirection(int from, int to)
    {
        int fromRow = from / boardSize;
        int fromCol = from % boardSize;

        int toRow = to / boardSize;
        int toCol = to % boardSize;

        if (toRow < fromRow)
            return 0; // Up

        if (toCol > fromCol)
            return 1; // Right

        if (toRow > fromRow)
            return 2; // Down

        return 3; // Left
    }

    // =====================================
    // CHECK CONNECTION
    // =====================================

    public void CheckConnection()
    {
        if (pipes == null || pipes.Length != boardSize * boardSize)
            return;

        if (energyAnimation != null)
        {
            StopCoroutine(energyAnimation);
            energyAnimation = null;
        }

        IsSolved = false;

        // Turn off energy on every pipe
        for (int i = 0; i < pipes.Length; i++)
        {
            pipes[i].SetPowered(false);
        }

        // Start must connect to the left
        if (!pipes[startIndex].HasConnection(
            PipeTile.Direction.Left))
            return;

        bool[] visited = new bool[pipes.Length];

        Queue<int> queue = new Queue<int>();

        List<int> energyOrder = new List<int>();

        queue.Enqueue(startIndex);
        visited[startIndex] = true;

        // Find connected pipes
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();

            energyOrder.Add(current);

            int row = current / boardSize;
            int col = current % boardSize;

            for (int d = 0; d < 4; d++)
            {
                if (!pipes[current].HasConnection(
                    (PipeTile.Direction)d))
                    continue;

                int nextRow = row;
                int nextCol = col;

                if (d == 0) nextRow--;
                if (d == 1) nextCol++;
                if (d == 2) nextRow++;
                if (d == 3) nextCol--;

                if (nextRow < 0 || nextRow >= boardSize ||
                    nextCol < 0 || nextCol >= boardSize)
                    continue;

                int next = nextRow * boardSize + nextCol;

                if (visited[next])
                    continue;

                int opposite = (d + 2) % 4;

                if (pipes[next].HasConnection(
                    (PipeTile.Direction)opposite))
                {
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }
        }

        // Check if energy reaches End
        if (visited[endIndex] &&
            pipes[endIndex].HasConnection(
                PipeTile.Direction.Right))
        {
            IsSolved = true;
            Debug.Log("PUZZLE SOLVED!");
        }

        // Start energy animation
        energyAnimation = StartCoroutine(
            AnimateEnergy(energyOrder)
        );
    }

    // =====================================
    // ANIMATE ENERGY
    // =====================================

    private IEnumerator AnimateEnergy(List<int> energyOrder)
    {
        foreach (int index in energyOrder)
        {
            pipes[index].SetPowered(true);

            yield return new WaitForSeconds(0.30f);
        }

        energyAnimation = null;
    }

    // =====================================
    // CLEAR OLD PIPES
    // =====================================

    private void ClearBoard()
    {
        if (energyAnimation != null)
        {
            StopCoroutine(energyAnimation);
            energyAnimation = null;
        }

        for (int i = boardParent.childCount - 1; i >= 0; i--)
        {
            Transform child = boardParent.GetChild(i);

            if (child == startPoint || child == endPoint)
                continue;

            Destroy(child.gameObject);
        }

        pipes = null;
    }
}
