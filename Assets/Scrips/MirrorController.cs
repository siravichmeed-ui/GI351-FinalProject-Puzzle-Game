
using UnityEngine;

public class MirrorController : MonoBehaviour
{
    private Camera mainCamera;

    private bool isRotating;
    private bool canRotate = true;

    private float previousAngle;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void OnMouseDown()
    {
        if (!canRotate)
            return;

        isRotating = true;
        previousAngle = GetMouseAngle();
    }

    private void OnMouseDrag()
    {
        if (!canRotate || !isRotating)
            return;

        float currentAngle = GetMouseAngle();

        float difference = Mathf.DeltaAngle(
            previousAngle,
            currentAngle
        );

        transform.Rotate(0f, 0f, difference);

        previousAngle = currentAngle;
    }

    private void OnMouseUp()
    {
        isRotating = false;
    }

    private float GetMouseAngle()
    {
        Vector3 mousePosition =
            mainCamera.ScreenToWorldPoint(
                Input.mousePosition
            );

        Vector2 direction =
            mousePosition - transform.position;

        return Mathf.Atan2(
            direction.y,
            direction.x
        ) * Mathf.Rad2Deg;
    }

    // Lock mirror rotation
    public void LockRotation()
    {
        canRotate = false;
        isRotating = false;
    }

    // Unlock mirror rotation
    public void UnlockRotation()
    {
        canRotate = true;
    }
}
