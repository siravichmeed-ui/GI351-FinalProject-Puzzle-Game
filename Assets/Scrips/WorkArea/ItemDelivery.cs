
using System.Collections;
using UnityEngine;

public class ItemDelivery : MonoBehaviour
{
    [Header("Item")]
    public GameObject cursedItem;

    [Header("Points")]
    public Transform spawnPoint;
    public Transform targetPoint;

    [Header("Delivery Animation")]
    public float deliveryDuration = 0.8f;
    public float arcHeight = 0.6f;

    private bool hasDelivered = false;

    public void DeliverItem()
    {
        if (hasDelivered) return;

        if (cursedItem == null ||
            spawnPoint == null ||
            targetPoint == null)
        {
            Debug.LogError("ItemDelivery: Missing References!");
            return;
        }

        hasDelivered = true;

        StartCoroutine(DeliveryAnimation());
    }

    IEnumerator DeliveryAnimation()
    {
        cursedItem.SetActive(true);

        Rigidbody2D rb = cursedItem.GetComponent<Rigidbody2D>();

        // ปิดฟิสิกส์ระหว่างส่งของ
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        Vector3 startPosition = spawnPoint.position;
        Vector3 endPosition = targetPoint.position;

        cursedItem.transform.position = startPosition;

        float timer = 0f;

        while (timer < deliveryDuration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / deliveryDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 position = Vector3.Lerp(
                startPosition,
                endPosition,
                smoothT
            );

            // ทำให้วัตถุลอยเป็นเส้นโค้ง
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            cursedItem.transform.position = position;

            yield return null;
        }

        cursedItem.transform.position = endPosition;

        // เปิดฟิสิกส์ให้ตกลงบนโต๊ะ
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 1f;
        }

        Debug.Log("Item delivered successfully!");
    }
}
