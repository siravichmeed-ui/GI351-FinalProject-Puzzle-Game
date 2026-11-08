
using UnityEngine;
using System.Collections.Generic;

public class PipeManager : MonoBehaviour
{
    public static PipeManager Instance;

    [Header("Pipe Prefabs")]
    public GameObject straightPrefab;
    public GameObject cornerPrefab;
    public GameObject endPrefab;

    [Header("Board Settings")]
    public Transform boardParent;

    private PipeTile[] pipes = new PipeTile[9];

    public bool IsSolved { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GeneratePuzzle();
    }

    // Generate a new puzzle
    public void GeneratePuzzle()
    {
        List<int> path = new List<int>();

        bool[] visited = new bool[9];

        visited[0] = true;
        path.Add(0);

        if (!FindPath(0, visited, path))
        {
            Debug.LogError("Cannot generate path!");
            return;
        }

        pipes = new PipeTile[9];

        // Generate all 9 tiles
        for (int i = 0; i < 9; i++)
        {
            int pathIndex = path.IndexOf(i);

            if (pathIndex != -1)
            {
                GeneratePathPipe(i, path, pathIndex);
            }
            else
            {
                GenerateRandomPipe(i);
            }
        }

        IsSolved = false;

        Debug.Log("Puzzle Generated!");
    }

    // Create pipes that form a valid solution
    private void GeneratePathPipe(
        int index,
        List<int> path,
        int pathIndex)
    {
        int dirA;
        int dirB = -1;

        // Start
        if (pathIndex == 0)
        {
            dirA = GetDirection(index, path[pathIndex + 1]);
        }
        // End
        else if (pathIndex == path.Count - 1)
        {
            dirA = GetDirection(index, path[pathIndex - 1]);
        }
        // Middle
        else
        {
            dirA = GetDirection(index, path[pathIndex - 1]);
            dirB = GetDirection(index, path[pathIndex + 1]);
        }

        PipeTile.PipeType type;

        if (dirB == -1)
        {
            type = PipeTile.PipeType.End;
        }
        else if ((dirA + 2) % 4 == dirB)
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

        int correctRotation = FindCorrectRotation(
            tile, dirA, dirB
        );

        // Scramble the correct pipe direction
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

    // Fill empty cells with random pipes
    private void GenerateRandomPipe(int index)
    {
        int randomType = Random.Range(0, 3);

        PipeTile.PipeType type =
            (PipeTile.PipeType)randomType;

        GameObject prefab = GetPrefab(type);

        GameObject obj = Instantiate(
            prefab,
            boardParent,
            false
        );

        PipeTile tile = obj.GetComponent<PipeTile>();

        pipes[index] = tile;

        tile.Initialize(type, Random.Range(0, 4));
    }

    // Find the correct rotation
    private int FindCorrectRotation(
        PipeTile tile,
        int dirA,
        int dirB)
    {
        for (int r = 0; r < 4; r++)
        {
            tile.Initialize(tile.pipeType, r);

            bool correct = true;

            for (int d = 0; d < 4; d++)
            {
                bool expected = d == dirA || d == dirB;

                bool actual = tile.HasConnection(
                    (PipeTile.Direction)d
                );

                if (expected != actual)
                {
                    correct = false;
                    break;
                }
            }

            if (correct)
                return r;
        }

        return 0;
    }

    // Get prefab by type
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

    // Get direction between two cells
    private int GetDirection(int from, int to)
    {
        int fromRow = from / 3;
        int fromCol = from % 3;

        int toRow = to / 3;
        int toCol = to % 3;

        if (toRow < fromRow)
            return 0; // Up

        if (toCol > fromCol)
            return 1; // Right

        if (toRow > fromRow)
            return 2; // Down

        return 3; // Left
    }

    // Find a random valid path from 0 to 8
    private bool FindPath(
        int current,
        bool[] visited,
        List<int> path)
    {
        if (current == 8)
            return true;

        int row = current / 3;
        int col = current % 3;

        List<int> neighbors = new List<int>();

        if (row > 0) neighbors.Add(current - 3);
        if (col < 2) neighbors.Add(current + 1);
        if (row < 2) neighbors.Add(current + 3);
        if (col > 0) neighbors.Add(current - 1);

        // Shuffle neighbors
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

            visited[next] = true;
            path.Add(next);

            if (FindPath(next, visited, path))
                return true;

            path.RemoveAt(path.Count - 1);
            visited[next] = false;
        }

        return false;
    }

    // Check connection from Start to End
    public void CheckConnection()
    {
        IsSolved = IsPathConnected();

        if (IsSolved)
        {
            Debug.Log("PUZZLE SOLVED!");
        }
        else
        {
            Debug.Log("Path not completed!");
        }
    }

    private bool IsPathConnected()
    {
        if (pipes == null || pipes.Length != 9)
            return false;

        bool[] visited = new bool[9];

        Queue<int> queue = new Queue<int>();

        queue.Enqueue(0);
        visited[0] = true;

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();

            if (current == 8)
                return true;

            int row = current / 3;
            int col = current % 3;

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

                if (nextRow < 0 || nextRow >= 3 ||
                    nextCol < 0 || nextCol >= 3)
                    continue;

                int next = nextRow * 3 + nextCol;

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

        return false;

    }

    private void PlaceTile(GameObject obj, int index)
    {
        int row = index / 3;
        int col = index % 3;

        obj.transform.localPosition = new Vector3(
            col * 1.5f,
            -row * 1.5f,
            0f
        );
    }

}
