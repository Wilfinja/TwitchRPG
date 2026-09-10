using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime-created speech bubble that floats above a character.
/// Attach via ChatBubble.CreateFor(parentTransform, offset) — no prefab required.
/// </summary>
public class ChatBubble : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float displayDuration = 5f;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private int maxCharacters = 140;

    [Header("Size")]
    [SerializeField] private float fontSize = 0.5f;
    [SerializeField] private float maxWidth = 3f;   // wrap threshold — messages wider than this wrap to a new line
    [SerializeField] private float padding = 0.05f; // added per side; total growth per axis = padding * 2

    [Header("Color")]
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField] private float outlineWidth = 0.2f;
    [SerializeField] private Color backgroundColor = new Color(0.15f, 0.18f, 0.42f, 1f); // opaque navy

    [Header("Sorting — must match/exceed your character sprite layer")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 100;

    private TextMeshPro textMesh;
    private SpriteRenderer backgroundRenderer;
    private Coroutine hideRoutine;

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
        // Background capsule (behind the text) — 9-sliced so width can change per message
        // without distorting the rounded end caps.
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(transform, worldPositionStays: false);
        bgGO.transform.localPosition = Vector3.zero;

        backgroundRenderer = bgGO.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = CreateCapsuleSprite();
        backgroundRenderer.drawMode = SpriteDrawMode.Sliced;
        backgroundRenderer.color = backgroundColor;
        backgroundRenderer.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
        backgroundRenderer.sortingOrder = sortingOrder - 1; // behind the text

        // Text
        textMesh = gameObject.AddComponent<TextMeshPro>();
        textMesh.fontSize = fontSize;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.textWrappingMode = TextWrappingModes.Normal;
        textMesh.color = textColor;
        textMesh.outlineWidth = outlineWidth;
        textMesh.outlineColor = outlineColor;
        textMesh.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
        textMesh.sortingOrder = sortingOrder;

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Bakes a pill/capsule sprite once. Border on left/right only (top/bottom = 0) keeps the
    /// rounded end caps a fixed radius regardless of how wide the Sliced sprite is stretched.
    /// </summary>
    private static Sprite CreateCapsuleSprite()
    {
        const int texHeight = 64;
        const int texWidth = 128; // baseline 2:1 — actual on-screen aspect is set later via .size
        float radius = texHeight / 2f;
        const float edgeSoftness = 1.5f;

        Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[texWidth * texHeight];
        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float dist;

                if (px < radius)
                    dist = radius - Vector2.Distance(new Vector2(px, py), new Vector2(radius, radius));
                else if (px > texWidth - radius)
                    dist = radius - Vector2.Distance(new Vector2(px, py), new Vector2(texWidth - radius, radius));
                else
                    dist = radius - Mathf.Abs(py - radius);

                float alpha = Mathf.Clamp01(dist / edgeSoftness + 0.5f);
                pixels[y * texWidth + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        // Border: left/right = radius (pixels) keeps the caps fixed size when Sliced; top/bottom = 0
        Vector4 border = new Vector4(radius, 0, radius, 0);
        return Sprite.Create(tex, new Rect(0, 0, texWidth, texHeight), new Vector2(0.5f, 0.5f), texHeight, 0, SpriteMeshType.FullRect, border);
    }

    public void ShowMessage(string message)
    {
        if (textMesh == null) return;

        string clean = Sanitize(message);

        // Set the wrap width BEFORE assigning text — wrapping is driven by sizeDelta.x
        // at the moment .text is set, not by any later calculation.
        textMesh.rectTransform.sizeDelta = new Vector2(maxWidth, 0f);
        textMesh.text = clean;
        textMesh.ForceMeshUpdate(); // build the mesh now so preferredWidth/Height are valid this frame

        // Read back the ACTUAL rendered size after wrapping was applied, not a predicted one.
        Vector2 fitted = new Vector2(textMesh.preferredWidth, textMesh.preferredHeight);

        Vector2 bgSize = fitted + new Vector2(padding * 2f, padding * 2f);
        bgSize.x = Mathf.Max(bgSize.x, bgSize.y); // keep width >= height so the two end caps don't overlap
        backgroundRenderer.size = bgSize;

        textMesh.color = textColor;
        backgroundRenderer.color = backgroundColor;
        gameObject.SetActive(true);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private string Sanitize(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

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

            Color tc = textColor; tc.a = textColor.a * a;
            textMesh.color = tc;

            Color bc = backgroundColor; bc.a = backgroundColor.a * a;
            backgroundRenderer.color = bc;

            yield return null;
        }

        gameObject.SetActive(false);
        hideRoutine = null;
    }
}
