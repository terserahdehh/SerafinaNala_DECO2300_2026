/*
 * Makes the existing Help instructions readable and scrollable.
 * HelpMenu adds/configures this active game component when setting up Help.
 * It reuses the title, text and Close button, including the button's event.
 * It creates scroll controls once and reuses them if setup runs again.
 * HelpMenu still opens/closes Help and blocks other controls behind it.
 * This also runs in Editor Play Mode. A RectTransform is a UI rectangle with
 * position, size and edge attachments called anchors. A ScrollRect moves text
 * inside a visible window. A mask clips text outside that window. TextMeshPro
 * is the text component used for the instructions and generated labels.
 * UI means the buttons and text the player sees and uses.
 */
// Use the text labels shown in the experience menus.
using TMPro;
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use Unity canvas graphics, buttons and scrolling controls.
using UnityEngine.UI;

// Makes the existing Help instructions readable and scrollable.
public sealed class HelpPanelLayout : MonoBehaviour
{
    // Keep the existing Help panel rectangle.
    private RectTransform panel;
    // Keep the existing Help title so layout preserves its existing wording.
    private TMP_Text title;
    // Keep the existing instruction text that becomes scrollable.
    private TMP_Text instructions;
    // Keep the existing Help close-button rectangle.
    private RectTransform close;
    // Keep the vertical ScrollRect holding the instruction text.
    private ScrollRect scroll;
    // Remember whether Help layout has all required references and is ready.
    private bool configured;

    // Arrange the existing Help content and add scrolling. Reuse any generated
    // controls so repeated setup cannot duplicate them.
    public void Configure()
    {
        // Hold off resize/refresh callbacks until this complete layout pass
        // succeeds.
        configured = false;
        // Use this object's UI rectangle as the existing Help panel.
        panel = (RectTransform)transform;
        // Find the existing text objects even if some children start inactive.
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
        {
            // Identify and keep the existing title by its child name.
            if (text.name == "Title") title = text;
            // Identify and keep the existing instruction text rather than
            // recreating its content.
            if (text.name == "Instructions") instructions = text;
        }
        // Find the existing close control so its persistent CloseHelp event
        // survives layout.
        close = transform.Find("Close Button") as RectTransform;
        // Stop layout if any required title, instruction text or close control
        // is missing.
        if (title == null || instructions == null || close == null)
        {
            // Report the missing required setup so it can be corrected in the
            // Inspector.
            Debug.LogError("Help panel needs its existing Title, Instructions and Close Button children.", this);
            // Stop layout because the existing title, instructions or Close
            // button is missing.
            return;
        }

        // Reuse/create the named scroll viewport so repeated setup never
        // duplicates it.
        var area = Child("Instructions Scroll Area", panel);
        // Inset the reading area, reserving space for title, close button and
        // footer.
        Edges(area, 36f, 112f, 76f, 84f);
        // Add/reuse a viewport Image to receive pointer drag and scroll input.
        var hitArea = GetOrAdd<Image>(area);
        // Make that input surface see-through so it does not cover the room or
        // instruction text.
        hitArea.color = Color.clear;
        // Keep the see-through surface able to receive pointer input so the
        // scroll view still receives input.
        hitArea.raycastTarget = true;
        // Clip text outside the reading rectangle without creating a dark
        // full-screen image.
        GetOrAdd<RectMask2D>(area);
        // Add/reuse the ScrollRect on this viewport.
        scroll = GetOrAdd<ScrollRect>(area);
        // Use the same reading rectangle as its viewport.
        scroll.viewport = area;
        // Use the existing instruction text rectangle as the moving scroll
        // content.
        scroll.content = instructions.rectTransform;
        // Disable sideways scrolling because the instructions are read
        // vertically.
        scroll.horizontal = false;
        // Allow scrolling down through the whole instruction text.
        scroll.vertical = true;
        // Clamp at text edges instead of allowing elastic overscroll.
        scroll.movementType = ScrollRect.MovementType.Clamped;
        // Stop coasting when the player stops scrolling.
        scroll.inertia = false;
        // Keep the scroll sensitivity expected by the controller-ray input
        // scaling.
        scroll.scrollSensitivity = 40f;

        // Move the existing instruction text into the viewport, discarding its
        // old local offset.
        instructions.rectTransform.SetParent(area, false);
        // Set the local UI rotation without changing its parent's facing.
        instructions.rectTransform.localRotation = Quaternion.identity;
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        instructions.rectTransform.localScale = Vector3.one;
        // Set the lower-left anchor relative to the parent rectangle.
        instructions.rectTransform.anchorMin = new Vector2(0f, 1f);
        // Set the upper-right anchor relative to the parent rectangle.
        instructions.rectTransform.anchorMax = Vector2.one;
        // Set which point inside the UI rectangle its position refers to.
        instructions.rectTransform.pivot = new Vector2(.5f, 1f);
        // Place the rectangle relative to its anchors in panel units.
        instructions.rectTransform.anchoredPosition = Vector2.zero;
        // Set the UI rectangle's width/height adjustment in panel units.
        instructions.rectTransform.sizeDelta = Vector2.zero;
        // Choose whether this label fits its font size to the available
        // rectangle.
        instructions.enableAutoSizing = false;
        // Set the label's text size in panel units to keep the message
        // readable.
        instructions.fontSize = 32f;
        // Set the visible UI colour, including transparency where specified.
        instructions.color = Color.white;
        // Align the text within its rectangle for this part of the menu.
        instructions.alignment = TextAlignmentOptions.TopLeft;
        // Allow normal line wrapping rather than forcing instructions outside
        // the panel.
        instructions.textWrappingMode = TextWrappingModes.Normal;
        // Let the text extend through its content area; the viewport mask clips
        // what is visible.
        instructions.overflowMode = TextOverflowModes.Overflow;
        // Add space between lines so the Help instructions are easier to
        // follow.
        instructions.lineSpacing = 5f;
        // Separate instruction paragraphs so sections do not run together.
        instructions.paragraphSpacing = 12f;
        // Inset the text within its content rectangle, including top and bottom
        // reading space.
        instructions.margin = new Vector4(0f, 10f, 0f, 18f);
        // Choose whether this graphic receives pointer hits or lets them reach
        // its controls.
        instructions.raycastTarget = false;
        // Allow the scroll viewport mask to clip this text outside the reading
        // area.
        instructions.maskable = true;
        // Replace extra blank lines so the Help instructions have consistent
        // paragraph gaps.
        instructions.text = System.Text.RegularExpressions.Regex.Replace(instructions.text,
            @"\n[ \t]*\n(?:[ \t]*\n)+", "\n\n");
        // Keep one space after each numbered heading without changing its
        // words.
        instructions.text = System.Text.RegularExpressions.Regex.Replace(instructions.text,
            @"(?m)^(\d+)\.[ \t]*", "$1. ");

        // Inset the title while reserving room on its right for the close
        // artwork.
        Edges(title.rectTransform, 36f, 24f, 120f, 0f);
        // Set the lower-left anchor relative to the parent rectangle.
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        // Set the upper-right anchor relative to the parent rectangle.
        title.rectTransform.anchorMax = Vector2.one;
        // Set which point inside the UI rectangle its position refers to.
        title.rectTransform.pivot = new Vector2(.5f, 1f);
        // Set the left/bottom inset relative to the parent anchors.
        title.rectTransform.offsetMin = new Vector2(36f, -88f);
        // Set the right/top inset relative to the parent anchors.
        title.rectTransform.offsetMax = new Vector2(-120f, -24f);
        // Set the label's text size in panel units to keep the message
        // readable.
        title.fontSize = 48f;
        // Choose whether this label fits its font size to the available
        // rectangle.
        title.enableAutoSizing = false;
        // Align the text within its rectangle for this part of the menu.
        title.alignment = TextAlignmentOptions.MidlineLeft;
        // Set the visible UI colour, including transparency where specified.
        title.color = Color.white;
        // Choose whether this graphic receives pointer hits or lets them reach
        // its controls.
        title.raycastTarget = false;
        // Choose how text beyond the rectangle is handled; the Help viewport
        // also clips it.
        title.overflowMode = TextOverflowModes.Truncate;

        // Keep the existing close button directly under the main Help panel.
        close.SetParent(panel, false);
        // Set the local UI rotation without changing its parent's facing.
        close.localRotation = Quaternion.identity;
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        close.localScale = Vector3.one;
        // Set the lower-left anchor relative to the parent rectangle.
        close.anchorMin = close.anchorMax = close.pivot = Vector2.one;
        // Place the UI relative to its anchors with an explicit depth.
        close.anchoredPosition3D = new Vector3(-24f, -24f, 0f);
        // Set the UI rectangle's width/height adjustment in panel units.
        close.sizeDelta = new Vector2(64f, 64f);
        // Draw that close button last without replacing its artwork or
        // persistent callback.
        close.SetAsLastSibling();

        // Reuse/create the named scrollbar track alongside the reading area.
        var track = Child("Instructions Scrollbar", panel);
        // Give the track the reading area's top/bottom margins.
        Edges(track, 0f, 112f, 28f, 84f);
        // Set the lower-left anchor relative to the parent rectangle.
        track.anchorMin = new Vector2(1f, 0f);
        // Set the upper-right anchor relative to the parent rectangle.
        track.anchorMax = Vector2.one;
        // Set the left/bottom inset relative to the parent anchors.
        track.offsetMin = new Vector2(-52f, 84f);
        // Set the right/top inset relative to the parent anchors.
        track.offsetMax = new Vector2(-28f, -112f);
        // Add/reuse an Image for the track's faint background.
        var trackImage = GetOrAdd<Image>(track);
        // Set the visible UI colour, including transparency where specified.
        trackImage.color = new Color(1f, 1f, 1f, .14f);
        // Reuse/create a handle rectangle inside that track.
        var handle = Child("Handle", track);
        // Stretch the handle before Scrollbar calculates its size and position.
        Edges(handle, 0f, 0f, 0f, 0f);
        // Add/reuse the handle's more visible Image.
        var handleImage = GetOrAdd<Image>(handle);
        // Set the visible UI colour, including transparency where specified.
        handleImage.color = new Color(1f, 1f, 1f, .9f);
        // Add/reuse the Scrollbar on the track rectangle.
        var bar = GetOrAdd<Scrollbar>(track);
        // Tell it which rectangle moves as the handle.
        bar.handleRect = handle;
        // Use the handle image for pointer interaction feedback.
        bar.targetGraphic = handleImage;
        // Increase its value from bottom to top, matching the vertical
        // ScrollRect convention.
        bar.direction = Scrollbar.Direction.BottomToTop;
        // Keep this bar on the pointer/trigger interaction path, not automatic
        // button automatic button selection.
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        // Connect this scrollbar to the reading area's vertical scrolling.
        scroll.verticalScrollbar = bar;
        // Leave the scrollbar visible as a clue that more instructions may be
        // below.
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // Create/reuse Up with a positive page-scroll direction.
        FooterButton("Help Scroll Up", "Up", -184f, 1f);
        // Create/reuse Down with the opposite page-scroll direction.
        FooterButton("Help Scroll Down", "Down", -88f, -1f);
        // Reuse/create a rectangle for the hint beside the footer buttons.
        var hint = Child("Help Scroll Hint", panel);
        // Add/reuse its TextMeshPro label rather than making duplicate hint
        // text.
        var label = GetOrAdd<TextMeshProUGUI>(hint);
        // Reuse the instruction font so generated Help controls match the
        // existing text.
        label.font = instructions.font;
        // Explain the available scrolling controls without changing the
        // instruction content.
        label.text = "Scroll for more  •  drag or use Up / Down";
        // Set the label's text size in panel units to keep the message
        // readable.
        label.fontSize = 22f;
        // Set the visible UI colour, including transparency where specified.
        label.color = Color.white;
        // Align the text within its rectangle for this part of the menu.
        label.alignment = TextAlignmentOptions.MidlineLeft;
        // Choose whether this graphic receives pointer hits or lets them reach
        // its controls.
        label.raycastTarget = false;
        // Set the lower-left anchor relative to the parent rectangle.
        hint.anchorMin = hint.anchorMax = hint.pivot = Vector2.zero;
        // Place the rectangle relative to its anchors in panel units.
        hint.anchoredPosition = new Vector2(36f, 22f);
        // Set the UI rectangle's width/height adjustment in panel units.
        hint.sizeDelta = new Vector2(570f, 44f);
        // Allow Refresh callbacks now that all required layout references are
        // ready.
        configured = true;
        // Render this UI using the shared overlay shaders so room geometry does
        // not hide it.
        QuestUI.ApplyOverlay(gameObject);
        // Measure the finished layout and begin at the top of the instructions.
        Refresh(true);
    }

    // Create/reuse a named scroll button. name identifies it; caption is Up or
    // Down. x positions it along the footer and direction selects scrolling up
    // or down.
    private void FooterButton(string name, string caption, float x, float direction)
    {
        // Reuse/create this named footer button so setup can safely run again.
        var rect = Child(name, panel);
        // Set the lower-left anchor relative to the parent rectangle.
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        // Place the rectangle relative to its anchors in panel units.
        rect.anchoredPosition = new Vector2(x, 22f);
        // Set the UI rectangle's width/height adjustment in panel units.
        rect.sizeDelta = new Vector2(88f, 44f);
        // Add/reuse a background Image for the generated button.
        var image = GetOrAdd<Image>(rect);
        // Set the visible UI colour, including transparency where specified.
        image.color = new Color(1f, 1f, 1f, .14f);
        // Add/reuse the button's interaction component.
        var button = GetOrAdd<Button>(rect);
        // Use its background Image for hover/press visual feedback.
        button.targetGraphic = image;
        // Keep footer clicks on pointer/trigger input rather than gamepad
        // automatic button selection.
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        // Clear only this generated button's game-created listeners before
        // assigning its single scroll callback.
        button.onClick.RemoveAllListeners();
        // Connect this button to the supplied positive or negative page-scroll
        // direction.
        button.onClick.AddListener(() => ScrollPage(direction));
        // Reuse/create the generated caption rectangle.
        var textRect = Child("Label", rect);
        // Inset the caption so it stays inside its button background.
        Edges(textRect, 4f, 4f, 4f, 4f);
        // Add/reuse a TextMeshPro label for that caption.
        var label = GetOrAdd<TextMeshProUGUI>(textRect);
        // Reuse the instruction font so generated Help controls match the
        // existing text.
        label.font = instructions.font;
        // Show the supplied Up or Down caption on this generated button.
        label.text = caption;
        // Set the label's text size in panel units to keep the message
        // readable.
        label.fontSize = 22f;
        // Set the visible UI colour, including transparency where specified.
        label.color = Color.white;
        // Align the text within its rectangle for this part of the menu.
        label.alignment = TextAlignmentOptions.Center;
        // Choose whether this graphic receives pointer hits or lets them reach
        // its controls.
        label.raycastTarget = false;
    }

    // Move the instructions by three quarters of one visible page. A positive
    // direction scrolls up; a negative one scrolls down.
    private void ScrollPage(float direction)
    {
        // Refresh content measurement before deciding how far one page scroll
        // should move.
        Refresh(false);
        // Calculate how much taller the instruction content is than the
        // viewport.
        float hidden = scroll.content.rect.height - scroll.viewport.rect.height;
        // Scroll only when some content actually lies beyond the visible area.
        if (hidden > .01f)
            // Start from the current scroll fraction and clamp the new fraction
            // to its limits.
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition +
                direction * scroll.viewport.rect.height * .75f / hidden);
    }

    // Measure instruction height at the current panel width. If resetToTop is
    // true, start at the first line; otherwise keep the current reading
    // position.
    public void Refresh(bool resetToTop)
    {
        // Skip measurement until the layout has all required objects and
        // references.
        if (!configured) return;
        // Refresh panel rectangle measurements before calculating text
        // wrapping.
        panel.ForceUpdateRectTransforms();
        // Refresh the viewport rectangle too so its dimensions are current.
        scroll.viewport.ForceUpdateRectTransforms();
        // Use at least a one-unit width to avoid invalid text measurement.
        float width = Mathf.Max(1f, scroll.viewport.rect.width);
        // Ask TextMeshPro for wrapped height at this width and add a small
        // bottom allowance.
        float preferred = instructions.GetPreferredValues(instructions.text, width, Mathf.Infinity).y + 28f;
        // Resize only the moving content's vertical dimension.
        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(scroll.viewport.rect.height, preferred));
        // Rebuild layout immediately so scrollbar calculations use those new
        // dimensions.
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        // Stop residual scrolling from a previous drag or interaction.
        scroll.StopMovement();
        // Return to the top only when the caller requested a newly opened
        // reading session.
        if (resetToTop) scroll.verticalNormalizedPosition = 1f;
    }

    // Return to the top when the Help panel becomes active.
    private void OnEnable() { Refresh(true); }
    // Re-measure the text after its panel changes size without asking for a
    // return to the top.
    private void OnRectTransformDimensionsChange() { if (configured) Refresh(false); }

    // Look for a UI child called name inside parent. Return that rectangle, or
    // make one if it is missing.
    private static RectTransform Child(string name, RectTransform parent)
    {
        // Look for a UI rectangle with this exact child name under the supplied
        // parent.
        var existing = parent.Find(name) as RectTransform;
        // Return an existing child instead of making a duplicate UI object.
        if (existing != null) return existing;
        // Create a rectangle only when the named child is missing.
        var go = new GameObject(name, typeof(RectTransform));
        // Put this generated UI object on Unity's UI layer for canvas ray
        // interaction.
        go.layer = 5;
        // Get the new object's RectTransform so callers can set its anchors and
        // size.
        var rect = (RectTransform)go.transform;
        // Parent the new rectangle without keeping an unrelated room offset.
        rect.SetParent(parent, false);
        // Return the existing/new rectangle so the caller can complete its
        // layout.
        return rect;
    }

    // Look for a component of type T on rect. Return the existing component, or
    // add one if missing.
    private static T GetOrAdd<T>(RectTransform rect) where T : Component
    {
        // Look for the requested component on this UI rectangle.
        var component = rect.GetComponent<T>();
        // Return it when present, otherwise add exactly one component of type
        // T.
        return component != null ? component : rect.gameObject.AddComponent<T>();
    }

    // Stretch rect inside its parent. left, top, right and bottom are the gaps
    // to leave from the parent's corresponding edges.
    private static void Edges(RectTransform rect, float left, float top, float right, float bottom)
    {
        // Set the local UI rotation without changing its parent's facing.
        rect.localRotation = Quaternion.identity;
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        rect.localScale = Vector3.one;
        // Set the lower-left anchor relative to the parent rectangle.
        rect.anchorMin = Vector2.zero;
        // Set the upper-right anchor relative to the parent rectangle.
        rect.anchorMax = Vector2.one;
        // Set the left/bottom inset relative to the parent anchors.
        rect.offsetMin = new Vector2(left, bottom);
        // Set the right/top inset relative to the parent anchors.
        rect.offsetMax = new Vector2(-right, -top);
    }
}
