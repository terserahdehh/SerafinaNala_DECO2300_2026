/*
 * Helps the other game scripts create and arrange headset menus.
 * IntroSequence, DrumRecorder, PerformanceSequence and HelpMenu call these
 * functions for canvases, text, buttons and drawing UI over room objects.
 * This is active shared game code, not an attached scene component. It has no
 * own Update loop. It also supports mouse UI during Editor Play Mode.
 * A Canvas holds UI. A RectTransform describes a UI rectangle. Anchors attach
 * that rectangle to its parent, the object containing it. A raycaster checks
 * which button a pointer touches. Materials/shaders control how graphics draw.
 * UI means the buttons and text the player sees and uses.
 */
// Use the text labels shown in the experience menus.
using TMPro;
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use Unity canvas graphics, buttons and scrolling controls.
using UnityEngine.UI;
// Use XR canvas raycasters and the headset UI input module.
using UnityEngine.XR.Interaction.Toolkit.UI;

// Helps the other game scripts create and arrange headset menus.
public static class QuestUI
{
    // Keep the shared game-created material for non-text overlay graphics.
    private static Material overlayMaterial;
    // Keep the dark text colour used on light UI backgrounds.
    public static readonly Color Ink = new Color(.13f, .15f, .19f, 1f);

    // Create a UI canvas and return it. name identifies it, eye is the viewing
    // camera, size is its panel width/height, and order controls which canvas
    // draws on top.
    public static Canvas CreateCanvas(string name, Camera eye, Vector2 size, int order)
    {
        // Create a named UI object with its rectangle and canvas components.
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
        // Get the canvas that was just added to the new UI object.
        var canvas = root.GetComponent<Canvas>();
        // Apply the shared headset size, draw order and pointer-checking
        // component setup.
        ConfigureCanvas(canvas, eye, size, order);
        // Return the configured canvas so its caller can parent and position
        // it.
        return canvas;
    }

    // Set up canvas for eye, the viewing camera. size gives its width/height in
    // UI units, and order is its drawing priority. Enable headset pointers, or
    // desktop clicking in Editor.
    public static void ConfigureCanvas(Canvas canvas, Camera eye, Vector2 size, int order)
    {
        // Put this generated UI object on Unity's UI layer for canvas ray
        // interaction.
        canvas.gameObject.layer = 5;
        // Use a rectangle placed in the 3D room, rather than desktop
        // screen-overlay rendering.
        canvas.renderMode = RenderMode.WorldSpace;
        // Use the supplied headset camera for room canvas pointer calculations.
        canvas.worldCamera = eye;
        // Apply the caller's sorting order so overlapping UI layers draw
        // predictably.
        canvas.sortingOrder = order;
        // Include the extra texture-coordinate channel needed by text/UI
        // shaders.
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        // Set the caller's requested canvas width and height in panel units.
        ((RectTransform)canvas.transform).sizeDelta = size;
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        canvas.transform.localScale = Vector3.one * .001f;
        // Reuse an existing XR graphic pointer-checking component on this
        // canvas.
        var raycaster = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
        // Add it only when absent so repeated configuration does not duplicate
        // it.
        if (raycaster == null) raycaster = canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        // Enable XR UI pointer testing after setup or a performance transition.
        raycaster.enabled = true;
        // Do not let room 3D colliders hide headset menu clicks.
        raycaster.checkFor3DOcclusion = false;
        // Do not let 2D colliders block those canvas pointer tests either.
        raycaster.checkFor2DOcclusion = false;
        // Find any desktop mouse pointer-checking component so the current
        // build can enable or disable it.
        var desktopRaycaster = canvas.GetComponent<GraphicRaycaster>();
// Compile the following part only for testing inside the Unity Editor.
#if UNITY_EDITOR
        // In Editor, add a mouse pointer-checking component when one is
        // missing.
        if (desktopRaycaster == null) desktopRaycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        // Allow desktop UI clicks through that pointer-checking component
        // during Play Mode testing.
        desktopRaycaster.enabled = true;
        // Keep room colliders from blocking desktop canvas interaction.
        desktopRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
// Use this alternative setup in the built game, including on Quest.
#else
        // Disable the desktop pointer-checking component in player builds so it
        // does not compete with XR UI.
        if (desktopRaycaster != null) desktopRaycaster.enabled = false;
// End the Editor-specific choice; the following shared code is kept for both.
#endif
    }

    // Put rect inside parent. position is its left/right and up/down offset;
    // size is its width/height. Centre its anchors so placement is easy to
    // predict.
    public static void Layout(RectTransform rect, Transform parent, Vector2 position, Vector2 size)
    {
        // Parent the existing rectangle under the requested UI container
        // without retaining its old offset.
        rect.SetParent(parent, false);
        // Set the lower-left anchor relative to the parent rectangle.
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        // Set the local UI rotation without changing its parent's facing.
        rect.localRotation = Quaternion.identity;
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        rect.localScale = Vector3.one;
        // Place the UI relative to its anchors with an explicit depth.
        rect.anchoredPosition3D = new Vector3(position.x, position.y, 0f);
        // Set the UI rectangle's width/height adjustment in panel units.
        rect.sizeDelta = size;
    }

    // Place canvas ahead of eye, the player's view. offset is measured along
    // the level viewing directions. Keep the menu upright even if the headset
    // tilts.
    public static void PlaceInFront(Canvas canvas, Camera eye, Vector3 offset)
    {
        // Skip placement if either the canvas or viewing camera is missing.
        if (canvas == null || eye == null) return;
        // Flatten the current gaze direction so the menu stays level when the
        // headset tilts.
        Vector3 forward = Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up);
        // Use room forward if looking nearly straight up/down gives no
        // horizontal direction.
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        // Turn the flattened direction into a level facing in the room for the
        // menu.
        var rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        // Place it from the camera plus the rotated offset, facing along the
        // same level direction.
        canvas.transform.SetPositionAndRotation(eye.transform.position + rotation * offset, rotation);
    }

    // Create a coloured UI panel inside parent and return its Image. name
    // identifies it; size and color control its appearance. It starts
    // non-clickable.
    public static Image Background(string name, Transform parent, Vector2 size, Color color)
    {
        // Create a separate UI rectangle with an Image for this coloured
        // background.
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        // Put this generated UI object on Unity's UI layer for canvas ray
        // interaction.
        go.layer = 5;
        // Get the Image created on that new object.
        var image = go.GetComponent<Image>();
        // Centre and size the background under its requested parent.
        Layout(image.rectTransform, parent, Vector2.zero, size);
        // Set the visible UI colour, including transparency where specified.
        image.color = color;
        // Choose whether this graphic receives pointer hits or lets them reach
        // its controls.
        image.raycastTarget = false;
        // Use the shared overlay material so room geometry does not cover this
        // panel.
        ApplyOverlay(go);
        // Return the image so callers can adjust its rectangle or interaction
        // settings.
        return image;
    }

    // Create and return a TextMeshPro label. name identifies it; parent
    // contains it; text is its wording. position, size, fontSize and color
    // control how it looks.
    public static TMP_Text Text(string name, Transform parent, string text, Vector2 position,
        Vector2 size, float fontSize, Color color)
    {
        // Create a UI rectangle with a TextMeshPro text component.
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        // Put this generated UI object on Unity's UI layer for canvas ray
        // interaction.
        go.layer = 5;
        // Get the label component from that newly created object.
        var label = go.GetComponent<TextMeshProUGUI>();
        // Apply the caller's parent, position and dimensions to the label.
        Layout(label.rectTransform, parent, position, size);
        // Display the caller's supplied wording; no content is generated from
        // gameplay state here.
        label.text = text;
        // Set the label's text size in panel units to keep the message
        // readable.
        label.fontSize = fontSize;
        // Set the visible UI colour, including transparency where specified.
        label.color = color;
        // Align the text within its rectangle for this part of the menu.
        label.alignment = TextAlignmentOptions.Center;
        // Choose whether this graphic receives pointer hits or lets them reach
        // its controls.
        label.raycastTarget = false;
        // Apply the text overlay shader without changing the shared font asset.
        ApplyOverlay(go);
        // Return the new label so the caller can update its text later.
        return label;
    }

    // Make the graphics inside root draw over room geometry. Give text its own
    // font material so the shared font asset is not changed.
    public static void ApplyOverlay(GameObject root)
    {
        // Create the shared menu drawing material only if it has not been made
        // yet.
        if (overlayMaterial == null)
        {
            // Load the Resources shader used for non-text UI overlay graphics.
            var shader = Resources.Load<Shader>("QuestOverlayUI");
            // Create the shared game-created panel material only when the
            // shader is available.
            if (shader != null) overlayMaterial = new Material(shader);
        }
        // Apply overlay rendering to existing graphics, including at first
        // inactive children.
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            // Treat text separately because its font material carries glyph
            // texture information.
            if (graphic is TMP_Text text)
            {
                // Obtain this text's copy font material rather than editing the
                // shared font asset.
                var material = text.fontMaterial;
                // Load the TextMeshPro-compatible overlay shader from
                // Resources.
                var shader = Resources.Load<Shader>("QuestTextOverlay");
                // Replace the font copy's shader only when that overlay shader
                // was found.
                if (shader != null) material.shader = shader;
                // Draw this overlay material after ordinary room geometry.
                material.renderQueue = 3000;
            }
            // Use the shared non-text overlay material on other graphics when
            // it exists.
            else if (overlayMaterial != null) graphic.material = overlayMaterial;
        }
    }
}
