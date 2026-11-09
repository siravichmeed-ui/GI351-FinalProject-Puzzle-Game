
using System.Collections;
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
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    [Header("Pipe Settings")]
    [SerializeField] private Transform pipeVisual;
    [SerializeField] private GameObject center;
    [SerializeField] private GameObject line1;
    [SerializeField] private GameObject line2;

    private PipeType pipeType;
    private int rotationIndex;

    private Coroutine powerEffect;

    [Header("Energy Colors")]
    [SerializeField]
    private Color normalColor =
        new Color(0.25f, 0.25f, 0.25f);

    [SerializeField]
    private Color energyColor =
        new Color(0.1f, 0.8f, 1f);

    [SerializeField]
    private Color glowColor =
        new Color(0.5f, 0.95f, 1f);

    [Header("Smooth Animation")]
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseStrength = 0.25f;

    private SpriteRenderer centerRenderer;
    private SpriteRenderer line1Renderer;
    private SpriteRenderer line2Renderer;

    public int RotationIndex => rotationIndex;

    private void Awake()
    {
        centerRenderer = center.GetComponent<SpriteRenderer>();
        line1Renderer = line1.GetComponent<SpriteRenderer>();
        line2Renderer = line2.GetComponent<SpriteRenderer>();
    }

    public void Initialize(PipeType type, int rotation)
    {
        SetupPipe(type);

        rotationIndex = rotation;
        ApplyRotation();
        SetPowered(false);
    }

    private void SetupPipe(PipeType type)
    {
        pipeType = type;

        if (line2 != null)
        {
            line2.SetActive(type != PipeType.End);
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
                return dir == rotationIndex ||
                       dir == (rotationIndex + 2) % 4;

            case PipeType.Corner:
                return dir == rotationIndex ||
                       dir == (rotationIndex + 1) % 4;

            case PipeType.End:
                return dir == rotationIndex;

            default:
                return false;
        }
    }

    private void OnMouseDown()
    {
        if (PipeManager.Instance == null)
            return;

        if (PipeManager.Instance.IsSolved)
            return;

        rotationIndex = (rotationIndex + 1) % 4;

        ApplyRotation();

        PipeManager.Instance.CheckConnection();
    }

    // เริ่มหรือหยุดเอฟเฟกต์พลังงาน
    public void SetPowered(bool powered)
    {
        if (powerEffect != null)
        {
            StopCoroutine(powerEffect);
            powerEffect = null;
        }

        powerEffect = StartCoroutine(AnimatePower(powered));
    }

    private IEnumerator AnimatePower(bool powered)
    {
        SpriteRenderer[] renderers =
        {
            centerRenderer,
            line1Renderer,
            line2Renderer
        };

        Color targetColor = powered ? energyColor : normalColor;

        Color[] startColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                startColors[i] = renderers[i].color;
        }

        // ค่อย ๆ เปลี่ยนสีอย่าง Smooth
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / fadeDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                renderers[i].color =
                    Color.Lerp(startColors[i], targetColor, t);
            }

            yield return null;
        }

        // กำหนดสีสุดท้ายให้ตรงกับเป้าหมาย
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr != null)
                sr.color = targetColor;
        }

        // ถ้าไม่มีพลังงาน ให้หยุดตรงนี้
        if (!powered)
        {
            powerEffect = null;
            yield break;
        }

        // เอฟเฟกต์เรืองแสงเบา ๆ ต่อเนื่อง
        while (true)
        {
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;

            pulse *= pulseStrength;

            Color currentGlow =
                Color.Lerp(energyColor, glowColor, pulse);

            foreach (SpriteRenderer sr in renderers)
            {
                if (sr != null)
                    sr.color = currentGlow;
            }

            yield return null;
        }
    }
}
