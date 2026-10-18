using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class QuestUI
{
    private static Material overlayMaterial;
    public static readonly Color Ink = new Color(.13f, .15f, .19f, 1f);

    public static Canvas CreateCanvas(string name, Camera eye, Vector2 size, int order)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        ConfigureCanvas(canvas, eye, size, order);
        return canvas;
    }

    public static void ConfigureCanvas(Canvas canvas, Camera eye, Vector2 size, int order)
    {
        canvas.gameObject.layer = 5;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eye;
        canvas.sortingOrder = order;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        ((RectTransform)canvas.transform).sizeDelta = size;
        canvas.transform.localScale = Vector3.one * .001f;
        var raycaster = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (raycaster == null) raycaster = canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        raycaster.enabled = true;
        raycaster.checkFor3DOcclusion = false;
        raycaster.checkFor2DOcclusion = false;
        var desktopRaycaster = canvas.GetComponent<GraphicRaycaster>();
#if UNITY_EDITOR
        if (desktopRaycaster == null) desktopRaycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        desktopRaycaster.enabled = true;
        desktopRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
#else
        if (desktopRaycaster != null) desktopRaycaster.enabled = false;
#endif
    }

    public static void Layout(RectTransform rect, Transform parent, Vector2 position, Vector2 size)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.anchoredPosition3D = new Vector3(position.x, position.y, 0f);
        rect.sizeDelta = size;
    }

    // Modal buttons stay in place while the user looks around and aims the ray.
    public static void PlaceInFront(Canvas canvas, Camera eye, Vector3 offset)
    {
        if (canvas == null || eye == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        var rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        canvas.transform.SetPositionAndRotation(eye.transform.position + rotation * offset, rotation);
    }

    public static Image Background(string name, Transform parent, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        var image = go.GetComponent<Image>();
        Layout(image.rectTransform, parent, Vector2.zero, size);
        image.color = color;
        image.raycastTarget = false;
        ApplyOverlay(go);
        return image;
    }

    public static TMP_Text Text(string name, Transform parent, string text, Vector2 position,
        Vector2 size, float fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = 5;
        var label = go.GetComponent<TextMeshProUGUI>();
        Layout(label.rectTransform, parent, position, size);
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        ApplyOverlay(go);
        return label;
    }

    public static Button Button(string name, Transform parent, string text, Vector2 position,
        Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var image = Background(name, parent, size, new Color(.92f, .86f, .75f, 1f));
        image.rectTransform.anchoredPosition = position;
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        Text(name + " Label", image.transform, text, Vector2.zero, size - Vector2.one * 12f, 25f, Ink);
        return button;
    }

    public static void ApplyOverlay(GameObject root)
    {
        if (overlayMaterial == null)
        {
            var shader = Resources.Load<Shader>("QuestOverlayUI");
            if (shader != null) overlayMaterial = new Material(shader);
        }
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic is TMP_Text text)
            {
                // Clone the font material: never change the shared font asset.
                var material = text.fontMaterial;
                var shader = Resources.Load<Shader>("QuestTextOverlay");
                if (shader != null) material.shader = shader;
                material.renderQueue = 3000;
            }
            else if (overlayMaterial != null) graphic.material = overlayMaterial;
        }
    }
}
