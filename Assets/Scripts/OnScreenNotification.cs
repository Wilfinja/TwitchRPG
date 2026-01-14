using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnScreenNotification : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform notificationContainer;
    [SerializeField] private float notificationDuration = 8f; // Increased for stats
    [SerializeField] private float fadeTime = 0.5f;
    [SerializeField] private float shopDisplayDuration = 14f;

    private Queue<string> messageQueue = new Queue<string>();
    private bool isShowingMessage = false;

    private static OnScreenNotification _instance;
    public static OnScreenNotification Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<OnScreenNotification>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[Notification] System initialized");
    }

    public void ShowNotification(string message, Color? color = null)
    {
        Debug.Log($"[Notification] Queuing message: {message.Substring(0, Mathf.Min(50, message.Length))}...");

        messageQueue.Enqueue(message);

        if (!isShowingMessage)
        {
            StartCoroutine(ProcessMessageQueue());
        }
    }

    public void ShowRPGNotification(string username, string message, ItemRarity? rarity = null)
    {
        Color color = Color.white;
        if (rarity.HasValue)
        {
            color = GetRarityColor(rarity.Value);
        }

        string formattedMessage = $"<b>{username}</b>: {message}";
        ShowNotification(formattedMessage, color);
    }

    private IEnumerator ProcessMessageQueue()
    {
        isShowingMessage = true;

        while (messageQueue.Count > 0)
        {
            string message = messageQueue.Dequeue();
            yield return StartCoroutine(DisplayMessage(message));
        }

        isShowingMessage = false;
    }

    private IEnumerator DisplayMessage(string message)
    {
        if (notificationPrefab == null)
        {
            Debug.LogError("[Notification] notificationPrefab is NULL!");
            yield break;
        }

        if (notificationContainer == null)
        {
            Debug.LogError("[Notification] notificationContainer is NULL!");
            yield break;
        }

        GameObject notification = Instantiate(notificationPrefab, notificationContainer);
        Debug.Log($"[Notification] Displaying: {message}");

        // Try TextMeshPro first
        TextMeshProUGUI tmpText = notification.GetComponentInChildren<TextMeshProUGUI>();
        Text textComponent = null;

        if (tmpText != null)
        {
            tmpText.text = message;

            // IMPORTANT: Enable word wrapping and proper overflow for long messages
            //tmpText.enableWordWrapping = true;
            tmpText.overflowMode = TextOverflowModes.Overflow;

            // Increase font size slightly if needed
            // tmpText.fontSize = 18;

            Debug.Log("[Notification] Using TextMeshPro");
        }
        else
        {
            textComponent = notification.GetComponentInChildren<Text>();
            if (textComponent != null)
            {
                textComponent.text = message;
                textComponent.supportRichText = true;
                textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
                textComponent.verticalOverflow = VerticalWrapMode.Overflow;

                Debug.Log("[Notification] Using legacy Text");
            }
            else
            {
                Debug.LogError("[Notification] No Text or TextMeshPro component found!");
            }
        }

        // Get or add CanvasGroup for fading
        CanvasGroup canvasGroup = notification.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = notification.AddComponent<CanvasGroup>();
        }

        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeTime);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Wait (longer for shop messages)
        float displayTime = message.Contains("DAILY SHOP") ? shopDisplayDuration : notificationDuration;
        yield return new WaitForSeconds(displayTime);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            yield return null;
        }

        Destroy(notification);
        Debug.Log("[Notification] Message destroyed");
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.6f, 0.6f, 0.6f);
            case ItemRarity.Uncommon: return new Color(0.2f, 0.8f, 0.2f);
            case ItemRarity.Rare: return new Color(0.2f, 0.4f, 1f);
            case ItemRarity.Epic: return new Color(0.64f, 0.21f, 0.93f);
            case ItemRarity.Legendary: return new Color(1f, 0.5f, 0f);
            case ItemRarity.Unique: return new Color(0f, 1f, 1f);
            default: return Color.white;
        }
    }

    public void ShowSuccess(string message)
    {
        ShowNotification($"✓ {message}", Color.green);
    }

    public void ShowError(string message)
    {
        ShowNotification($"✗ {message}", Color.red);
    }

    public void ShowInfo(string message)
    {
        ShowNotification(message, Color.cyan);
    }
}
