using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime-created speech bubble that floats above a character.
/// Attach via ChatBubble.CreateFor(parentTransform, offset) — no prefab required.
/// </summary>
public class ChatBubble : MonoBehaviour
{
    [SerializeField] private float displayDuration = 5f;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private int maxCharacters = 140;

    private TextMeshPro textMesh;
    private CanvasGroup canvasGroupProxy; // not used for 3D TMP, alpha handled via color
    private Coroutine hideRoutine;
    private Color baseColor;

    public static ChatBubble CreateFor(Transform character, Vector3 localOffset)
    {
        GameObject bubbleGO = new GameObject("ChatBubble");
        bubbleGO.transform.SetParent(character, worldPositionStays: false);
        bubbleGO.transform.localPosition = localOffset;

        ChatBubble bubble = bubbleGO.AddComponent<ChatBubble>();
        bubble.Setup();
        return bubble;
    }

    private void Setup()
    {
        textMesh = gameObject.AddComponent<TextMeshPro>();
        textMesh.fontSize = 3f;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.textWrappingMode = TextWrappingModes.Normal;
        textMesh.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
        textMesh.color = Color.white;
        textMesh.outlineWidth = 0.2f;
        textMesh.outlineColor = Color.black;
        textMesh.sortingOrder = 100; // draw above character sprites

        baseColor = textMesh.color;
        gameObject.SetActive(false);
    }

    public void ShowMessage(string message)
    {
        if (textMesh == null) return;

        textMesh.text = Sanitize(message);
        textMesh.color = baseColor;
        gameObject.SetActive(true);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private string Sanitize(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        // Strip TMP rich-text tag characters so chat can't inject markup/exploit rendering
        string clean = message.Replace("<", "‹").Replace(">", "›");

        if (clean.Length > maxCharacters)
            clean = clean.Substring(0, maxCharacters) + "…";

        return clean;
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / fadeDuration);
            Color c = textMesh.color;
            c.a = a;
            textMesh.color = c;
            yield return null;
        }

        gameObject.SetActive(false);
        hideRoutine = null;
    }
}
