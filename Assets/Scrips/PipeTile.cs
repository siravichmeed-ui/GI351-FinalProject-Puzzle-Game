
using UnityEngine;

public class PipeTile : MonoBehaviour
{
    public enum PipeType
    {
        Straight,
        Corner,
        End
    }

    public enum Direction
    {
        Up,
        Right,
        Down,
        Left
    }

    [Header("Pipe Settings")]
    public PipeType pipeType;

    [Header("Pipe Visual")]
    public Transform pipeVisual;
    public GameObject center;
    public GameObject line1;
    public GameObject line2;

    private int rotationIndex = 0;

    private void Awake()
    {
        SetupPipe();
    }

    public void Initialize(PipeType type, int rotation)
    {
        pipeType = type;

        SetupPipe();

        rotationIndex = rotation % 4;

        ApplyRotation();
    }

    private void OnMouseDown()
    {
        if (PipeManager.Instance != null &&
            PipeManager.Instance.IsSolved)
            return;

        rotationIndex = (rotationIndex + 1) % 4;

        ApplyRotation();

        if (PipeManager.Instance != null)
        {
            PipeManager.Instance.CheckConnection();
        }
    }

    private void ApplyRotation()
    {
        pipeVisual.localRotation =
            Quaternion.Euler(0, 0, -rotationIndex * 90f);
    }

    public bool HasConnection(Direction direction)
    {
        int dir = (int)direction;

        switch (pipeType)
        {
            case PipeType.Straight:
                return dir == rotationIndex % 4 ||
                       dir == (rotationIndex + 2) % 4;

            case PipeType.Corner:
                return dir == rotationIndex % 4 ||
                       dir == (rotationIndex + 1) % 4;

            case PipeType.End:
                return dir == rotationIndex % 4;
        }

        return false;
    }

    private void SetupPipe()
    {
        center.SetActive(true);
        line1.SetActive(true);
        line2.SetActive(false);

        line1.transform.localRotation = Quaternion.identity;
        line2.transform.localRotation = Quaternion.identity;

        switch (pipeType)
        {
            case PipeType.Straight:

                line1.transform.localPosition = Vector3.up * 0.25f;
                line1.transform.localScale =
                    new Vector3(0.2f, 0.5f, 1);

                line2.SetActive(true);
                line2.transform.localPosition = Vector3.down * 0.25f;
                line2.transform.localScale =
                    new Vector3(0.2f, 0.5f, 1);

                break;

            case PipeType.Corner:

                line1.transform.localPosition = Vector3.up * 0.25f;
                line1.transform.localScale =
                    new Vector3(0.2f, 0.5f, 1);

                line2.SetActive(true);
                line2.transform.localPosition = Vector3.right * 0.25f;
                line2.transform.localScale =
                    new Vector3(0.5f, 0.2f, 1);

                break;

            case PipeType.End:

                line1.transform.localPosition = Vector3.up * 0.25f;
                line1.transform.localScale =
                    new Vector3(0.2f, 0.5f, 1);

                break;
        }
    }
}
