using System.Collections;
using UnityEngine;
using TMPro;

public class CustomerInteraction : MonoBehaviour
{
    [Header("Dialogue Settings")]
    public GameObject dialogueBubble;
    public CanvasGroup dialogueCanvasGroup;
    public TMP_Text dialogueText;

    [Header("Customer Dialogue")]
    [TextArea(2, 4)]
    public string customerMessage =
        "สวัสดีค่ะ!\nฉันมีของบางอย่างอยากให้คุณช่วยดูหน่อย...";

    [Header("Fade Settings")]
    public float fadeDuration = 0.25f;

    [Header("Typing Settings")]
    public float typingSpeed = 35f;
    [Tooltip("หยุดเพิ่มหลังเจอ . ! ? (… จะหยุดทุกจุด)")]
    public float punctuationPause = 0.18f;
    public float newlinePause = 0.3f;
    [Tooltip("เล่นเสียงพิมพ์ทุกๆ กี่ตัวอักษร")]
    public int blipEvery = 2;

    [Header("Pop Animation")]
    public float popDuration = 0.22f;
    public float popStartScale = 0.75f;
    [Tooltip("องศาที่กล่องเอียงตอนเด้งออกมา")]
    public float popWobbleAngle = 4f;

    [Header("Hide Animation")]
    public float hideEndScale = 0.85f;

    [Header("Alert Icon (optional) เช่น เครื่องหมาย !")]
    public Transform alertIcon;
    public float alertFloatHeight = 0.08f;
    public float alertFloatSpeed = 4f;

    [Header("References (optional)")]
    public CustomerMovement movement;

    [Header("Audio (optional)")]
    public AudioClip popSfx;
    public AudioClip typeBlipSfx;
    public AudioClip closeSfx;

    private bool canInteract = true;
    private bool orderAccepted = false;
    private bool isTyping = false;

    private Coroutine dialogueCoroutine;
    private Coroutine fadeCoroutine;
    private Coroutine alertCoroutine;

    private RectTransform dialogueRect;
    private AudioSource audioSource;

    private Vector3 alertBasePos;
    private Vector3 alertBaseScale;
    private bool alertVisible = false;

    public ItemDelivery itemDelivery;

    void Start()
    {
        if (dialogueBubble == null)
        {
            Debug.LogError("CustomerInteraction: Dialogue Bubble is missing!", this);
            return;
        }

        dialogueRect = dialogueBubble.GetComponent<RectTransform>();

        dialogueBubble.SetActive(false);

        if (dialogueCanvasGroup != null)
            dialogueCanvasGroup.alpha = 0;

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (movement == null)
            movement = GetComponent<CustomerMovement>();

        if (itemDelivery == null)
            itemDelivery = GetComponent<ItemDelivery>();

        if (alertIcon != null)
        {
            alertBasePos = alertIcon.localPosition;
            alertBaseScale = alertIcon.localScale;
            alertIcon.gameObject.SetActive(false);
        }

        if (movement != null)
        {
            if (movement.HasArrived)
                ShowAlert();
            else
                movement.OnArrived += ShowAlert;
        }

        Debug.Log("CustomerInteraction Ready: " + gameObject.name, this);
    }
    public void ConfigureCustomer(
        CustomerData customer,
        ItemData item,
        Transform itemSpawnPoint,
        Transform itemTargetPoint)
    {
        if (customer == null || item == null)
        {
            Debug.LogError("Missing Customer or Item Data!");
            return;
        }

        customerMessage = customer.customerMessage;

        if (movement == null)
            movement = GetComponent<CustomerMovement>();

        if (itemDelivery == null)
            itemDelivery = GetComponent<ItemDelivery>();

        if (itemDelivery != null)
        {
            itemDelivery.Configure(
                item,
                itemSpawnPoint,
                itemTargetPoint
            );
        }
        else
        {
            Debug.LogError("ItemDelivery is missing!");
        }
    }



    void OnDestroy()
    {
        if (movement != null)
            movement.OnArrived -= ShowAlert;
    }

    void Update()
    {
        // ไอคอนลอยขึ้นลงเบาๆ
        if (alertVisible && alertIcon != null)
        {
            alertIcon.localPosition = alertBasePos +
                Vector3.up * Mathf.Sin(Time.time * alertFloatSpeed) * alertFloatHeight;
        }
    }

    // =================================
    // MOUSE
    // =================================

    void OnMouseEnter()
    {
        if (!canInteract || orderAccepted || movement == null)
            return;

        movement.TalkBounce(0.04f);
    }


    void OnMouseDown()
    {
        Debug.Log(
            "CUSTOMER CLICKED: " + gameObject.name +
            " | canInteract: " + canInteract +
            " | orderAccepted: " + orderAccepted,
            this
        );

        if (!canInteract || orderAccepted)
        {
            Debug.Log("Customer click blocked!", this);
            return;
        }

        if (isTyping)
        {
            SkipTyping();
            return;
        }

        if (dialogueBubble == null)
        {
            Debug.LogError("Dialogue Bubble is missing!", this);
            return;
        }

        if (dialogueBubble.activeSelf)
            return;

        OpenDialogue();
    }


    // =================================
    // ALERT ICON
    // =================================

    void ShowAlert()
    {
        if (alertIcon == null || orderAccepted || dialogueBubble.activeSelf) return;

        if (alertCoroutine != null) StopCoroutine(alertCoroutine);
        alertCoroutine = StartCoroutine(AlertPopIn());
    }

    void HideAlert()
    {
        if (alertIcon == null || !alertVisible) return;

        if (alertCoroutine != null) StopCoroutine(alertCoroutine);
        alertCoroutine = StartCoroutine(AlertPopOut());
    }

    IEnumerator AlertPopIn()
    {
        alertIcon.gameObject.SetActive(true);
        alertVisible = true;

        float timer = 0f;
        float duration = 0.3f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            alertIcon.localScale = alertBaseScale * EaseOutBack(t);

            yield return null;
        }

        alertIcon.localScale = alertBaseScale;
        alertCoroutine = null;
    }

    IEnumerator AlertPopOut()
    {
        alertVisible = false;

        float timer = 0f;
        float duration = 0.15f;
        Vector3 startScale = alertIcon.localScale;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            alertIcon.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);

            yield return null;
        }

        alertIcon.gameObject.SetActive(false);
        alertCoroutine = null;
    }

    // =================================
    // DIALOGUE
    // =================================

    public void OpenDialogue()
    {
        if (dialogueCoroutine != null) return;

        dialogueCoroutine = StartCoroutine(ShowDialogue());
    }

    IEnumerator ShowDialogue()
    {
        HideAlert();

        dialogueBubble.SetActive(true);

        dialogueCanvasGroup.alpha = 0;

        dialogueRect.localScale = Vector3.one * popStartScale;
        dialogueRect.localRotation = Quaternion.Euler(0f, 0f, popWobbleAngle);

        dialogueText.text = customerMessage;
        dialogueText.maxVisibleCharacters = 0;

        PlaySfx(popSfx, 1f);

        if (movement != null)
            movement.TalkBounce(0.08f);

        // Pop + Fade พร้อมกัน
        yield return StartCoroutine(PopAndFade());

        // เริ่มพิมพ์ข้อความ
        yield return StartCoroutine(TypeText());

        dialogueCoroutine = null;
    }

    IEnumerator PopAndFade()
    {
        float timer = 0f;

        while (timer < popDuration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / popDuration);
            float eased = EaseOutBack(t);

            // ขยายกล่อง
            dialogueRect.localScale = Vector3.one *
                Mathf.LerpUnclamped(popStartScale, 1f, eased);

            // เอียงแล้วค่อยๆ ตั้งตรง (เลย 0 นิดหน่อยตามแรงเด้ง)
            dialogueRect.localRotation = Quaternion.Euler(
                0f, 0f, popWobbleAngle * (1f - eased)
            );

            // Fade In
            dialogueCanvasGroup.alpha = t;

            yield return null;
        }

        dialogueRect.localScale = Vector3.one;
        dialogueRect.localRotation = Quaternion.identity;
        dialogueCanvasGroup.alpha = 1f;
    }

    IEnumerator TypeText()
    {
        isTyping = true;

        dialogueText.ForceMeshUpdate();

        TMP_TextInfo info = dialogueText.textInfo;
        int totalCharacters = info.characterCount;

        dialogueText.maxVisibleCharacters = 0;

        float baseDelay = 1f / Mathf.Max(typingSpeed, 1f);
        int blipCounter = 0;

        for (int i = 1; i <= totalCharacters; i++)
        {
            dialogueText.maxVisibleCharacters = i;

            char c = info.characterInfo[i - 1].character;

            // เสียงพิมพ์ + ลูกค้าเด้งตามจังหวะพูด
            if (!char.IsWhiteSpace(c))
            {
                blipCounter++;

                if (blipCounter % Mathf.Max(blipEvery, 1) == 0)
                    TypeBlip();
            }

            // หยุดตามจังหวะประโยค
            float delay = baseDelay;

            if (c == '\n')
                delay += newlinePause;
            else if (c == '.' || c == '!' || c == '?' || c == '…')
                delay += punctuationPause;
            else if (c == ',' || c == '，' || c == '、')
                delay += punctuationPause * 0.5f;

            yield return new WaitForSeconds(delay);
        }

        dialogueText.maxVisibleCharacters = totalCharacters;

        isTyping = false;
    }

    void TypeBlip()
    {
        PlaySfx(typeBlipSfx, 0.5f);

        if (movement != null)
            movement.TalkBounce(0.03f);
    }

    void SkipTyping()
    {
        dialogueText.maxVisibleCharacters =
            dialogueText.textInfo.characterCount;

        isTyping = false;

        if (movement != null)
            movement.TalkBounce(0.05f);

        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
            dialogueCoroutine = null;
        }
    }

    // =================================
    // ACCEPT / CLOSE
    // =================================

    public void AcceptOrder()
    {
        Debug.Log("AcceptOrder Called!", this);

        if (orderAccepted)
        {
            Debug.Log("Order already accepted!", this);
            return;
        }

        if (itemDelivery == null)
        {
            Debug.LogError("ItemDelivery is NOT assigned!", this);
            return;
        }

        orderAccepted = true;
        canInteract = false;

        Debug.Log("Accept Order Clicked!", this);

        itemDelivery.DeliverItem();

        if (movement != null)
            movement.Hop();

        CloseDialogue();
    }

    public void CloseDialogue()
    {
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
            dialogueCoroutine = null;
        }

        isTyping = false;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        PlaySfx(closeSfx, 1f);

        fadeCoroutine = StartCoroutine(HideDialogue());
    }

    IEnumerator HideDialogue()
    {
        float startAlpha = dialogueCanvasGroup.alpha;
        Vector3 startScale = dialogueRect.localScale;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / fadeDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            dialogueCanvasGroup.alpha =
                Mathf.Lerp(startAlpha, 0f, smooth);

            // หดเล็กลงตอนหายไป
            dialogueRect.localScale = Vector3.Lerp(
                startScale,
                Vector3.one * hideEndScale,
                t * t
            );

            yield return null;
        }

        dialogueCanvasGroup.alpha = 0f;
        dialogueRect.localScale = Vector3.one;
        dialogueRect.localRotation = Quaternion.identity;
        dialogueBubble.SetActive(false);

        fadeCoroutine = null;
    }

    // =================================
    // SAVE / RESTORE INTERACTION
    // =================================

    public bool GetOrderAcceptedState()
    {
        return orderAccepted;
    }

    public void RestoreInteractionState(bool savedOrderAccepted)
    {
        orderAccepted = savedOrderAccepted;
        canInteract = !savedOrderAccepted;

        // ถ้ารับออเดอร์ไปแล้ว ไม่ต้องแสดงไอคอนเตือน
        if (savedOrderAccepted)
        {
            alertVisible = false;

            if (alertIcon != null)
                alertIcon.gameObject.SetActive(false);

            if (dialogueBubble != null)
                dialogueBubble.SetActive(false);

            if (dialogueCanvasGroup != null)
                dialogueCanvasGroup.alpha = 0f;
        }
    }

    // =================================
    // HELPERS
    // =================================



    float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3)
                  + c1 * Mathf.Pow(t - 1f, 2);
    }

    void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null) return;

        audioSource.pitch = Random.Range(0.92f, 1.08f);
        audioSource.PlayOneShot(clip, volume);
    }
}