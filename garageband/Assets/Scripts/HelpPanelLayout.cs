using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Owns layout only. HelpMenu keeps its existing visibility, input and modal behaviour.
public sealed class HelpPanelLayout : MonoBehaviour
{
    private RectTransform panel;
    private TMP_Text title;
    private TMP_Text instructions;
    private RectTransform close;
    private ScrollRect scroll;
    private bool configured;

    public void Configure()
    {
        configured = false;
        panel = (RectTransform)transform;
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "Title") title = text;
            if (text.name == "Instructions") instructions = text;
        }
        close = transform.Find("Close Button") as RectTransform;
        if (title == null || instructions == null || close == null)
        {
            Debug.LogError("Help panel needs its existing Title, Instructions and Close Button children.", this);
            return;
        }

        // Reuse generated children if Configure is called again; never duplicate the content/buttons.
        var area = Child("Instructions Scroll Area", panel);
        Edges(area, 36f, 112f, 76f, 84f);
        var hitArea = GetOrAdd<Image>(area);
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        GetOrAdd<RectMask2D>(area);
        scroll = GetOrAdd<ScrollRect>(area);
        scroll.viewport = area;
        scroll.content = instructions.rectTransform;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        scroll.scrollSensitivity = 40f;

        instructions.rectTransform.SetParent(area, false);
        instructions.rectTransform.localRotation = Quaternion.identity;
        instructions.rectTransform.localScale = Vector3.one;
        instructions.rectTransform.anchorMin = new Vector2(0f, 1f);
        instructions.rectTransform.anchorMax = Vector2.one;
        instructions.rectTransform.pivot = new Vector2(.5f, 1f);
        instructions.rectTransform.anchoredPosition = Vector2.zero;
        instructions.rectTransform.sizeDelta = Vector2.zero;
        instructions.enableAutoSizing = false;
        instructions.fontSize = 32f;
        instructions.color = Color.white;
        instructions.alignment = TextAlignmentOptions.TopLeft;
        instructions.textWrappingMode = TextWrappingModes.Normal;
        instructions.overflowMode = TextOverflowModes.Overflow; // The viewport mask clips scroll content.
        instructions.lineSpacing = 5f;
        instructions.paragraphSpacing = 12f;
        instructions.margin = new Vector4(0f, 10f, 0f, 18f);
        instructions.raycastTarget = false;
        instructions.maskable = true;
        // Normalise spacing only; retain all authored instructions and controller mappings.
        instructions.text = System.Text.RegularExpressions.Regex.Replace(instructions.text,
            @"\n[ \t]*\n(?:[ \t]*\n)+", "\n\n");
        instructions.text = System.Text.RegularExpressions.Regex.Replace(instructions.text,
            @"(?m)^(\d+)\.[ \t]*", "$1. ");

        Edges(title.rectTransform, 36f, 24f, 120f, 0f);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.pivot = new Vector2(.5f, 1f);
        title.rectTransform.offsetMin = new Vector2(36f, -88f);
        title.rectTransform.offsetMax = new Vector2(-120f, -24f);
        title.fontSize = 48f;
        title.enableAutoSizing = false;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.color = Color.white;
        title.raycastTarget = false;
        title.overflowMode = TextOverflowModes.Truncate;

        close.SetParent(panel, false);
        close.localRotation = Quaternion.identity;
        close.localScale = Vector3.one;
        close.anchorMin = close.anchorMax = close.pivot = Vector2.one;
        close.anchoredPosition3D = new Vector3(-24f, -24f, 0f);
        close.sizeDelta = new Vector2(64f, 64f);
        close.SetAsLastSibling(); // Keep the authored artwork and persistent CloseHelp listener.

        var track = Child("Instructions Scrollbar", panel);
        Edges(track, 0f, 112f, 28f, 84f);
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = Vector2.one;
        track.offsetMin = new Vector2(-52f, 84f);
        track.offsetMax = new Vector2(-28f, -112f);
        var trackImage = GetOrAdd<Image>(track);
        trackImage.color = new Color(1f, 1f, 1f, .14f);
        var handle = Child("Handle", track);
        Edges(handle, 0f, 0f, 0f, 0f);
        var handleImage = GetOrAdd<Image>(handle);
        handleImage.color = new Color(1f, 1f, 1f, .9f);
        var bar = GetOrAdd<Scrollbar>(track);
        bar.handleRect = handle;
        bar.targetGraphic = handleImage;
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        FooterButton("Help Scroll Up", "Up", -184f, 1f);
        FooterButton("Help Scroll Down", "Down", -88f, -1f);
        var hint = Child("Help Scroll Hint", panel);
        var label = GetOrAdd<TextMeshProUGUI>(hint);
        label.font = instructions.font;
        label.text = "Scroll for more  •  drag or use Up / Down";
        label.fontSize = 22f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        hint.anchorMin = hint.anchorMax = hint.pivot = Vector2.zero;
        hint.anchoredPosition = new Vector2(36f, 22f);
        hint.sizeDelta = new Vector2(570f, 44f);
        configured = true;
        QuestUI.ApplyOverlay(gameObject);
        Refresh(true);
    }

    private void FooterButton(string name, string caption, float x, float direction)
    {
        var rect = Child(name, panel);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(x, 22f);
        rect.sizeDelta = new Vector2(88f, 44f);
        var image = GetOrAdd<Image>(rect);
        image.color = new Color(1f, 1f, 1f, .14f);
        var button = GetOrAdd<Button>(rect);
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.RemoveAllListeners(); // Only these generated scroll buttons are configured here.
        button.onClick.AddListener(() => ScrollPage(direction));
        var textRect = Child("Label", rect);
        Edges(textRect, 4f, 4f, 4f, 4f);
        var label = GetOrAdd<TextMeshProUGUI>(textRect);
        label.font = instructions.font;
        label.text = caption;
        label.fontSize = 22f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private void ScrollPage(float direction)
    {
        Refresh(false);
        float hidden = scroll.content.rect.height - scroll.viewport.rect.height;
        if (hidden > .01f)
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition +
                direction * scroll.viewport.rect.height * .75f / hidden);
    }

    public void Refresh(bool resetToTop)
    {
        if (!configured) return;
        // Measure at the actual viewport width; keep readable font size as the text grows.
        panel.ForceUpdateRectTransforms();
        scroll.viewport.ForceUpdateRectTransforms();
        float width = Mathf.Max(1f, scroll.viewport.rect.width);
        float preferred = instructions.GetPreferredValues(instructions.text, width, Mathf.Infinity).y + 28f;
        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(scroll.viewport.rect.height, preferred));
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        scroll.StopMovement();
        if (resetToTop) scroll.verticalNormalizedPosition = 1f;
    }

    private void OnEnable() { Refresh(true); }
    private void OnRectTransformDimensionsChange() { if (configured) Refresh(false); }

    private static RectTransform Child(string name, RectTransform parent)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static T GetOrAdd<T>(RectTransform rect) where T : Component
    {
        var component = rect.GetComponent<T>();
        return component != null ? component : rect.gameObject.AddComponent<T>();
    }

    private static void Edges(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
