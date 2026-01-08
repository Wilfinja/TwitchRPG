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
    [SerializeField] private float notificationDuration = 5f;
    [SerializeField] private float fadeTime = 0.5f;

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
    }

    public void ShowNotification(string message, Color? color = null)
    {
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
        GameObject notification = Instantiate(notificationPrefab, notificationContainer);

        // Try TextMeshPro first, fall back to regular Text
        TextMeshProUGUI tmpText = notification.GetComponentInChildren<TextMeshProUGUI>();
        Text textComponent = null;

        if (tmpText != null)
        {
            tmpText.text = message;

            tmpText.overflowMode = TextOverflowModes.Overflow;
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
            }
        }

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

        // Wait
        yield return new WaitForSeconds(notificationDuration);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            yield return null;
        }

        Destroy(notification);
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.6f, 0.6f, 0.6f); // Gray
            case ItemRarity.Uncommon: return new Color(0.2f, 0.8f, 0.2f); // Green
            case ItemRarity.Rare: return new Color(0.2f, 0.4f, 1f); // Blue
            case ItemRarity.Epic: return new Color(0.64f, 0.21f, 0.93f); // Purple
            case ItemRarity.Legendary: return new Color(1f, 0.5f, 0f); // Orange
            case ItemRarity.Unique: return new Color(0f, 1f, 1f); // Cyan
            default: return Color.white;
        }
    }

    // Quick access methods
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
