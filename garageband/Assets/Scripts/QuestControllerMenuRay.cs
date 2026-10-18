using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

// A controller-only menu ray: independent of hand tracking and drumstick selection.
[DefaultExecutionOrder(-200)]
public class QuestControllerMenuRay : MonoBehaviour
{
    private InputAction position;
    private InputAction rotation;
    private InputAction trigger;
    private InputAction triggerValue;
    private InputAction scroll;
    private XRInputValueReader<Vector2> scrollReader;
    // Full deflection: ~600 panel units/second with the current XR multiplier (5)
    // and Help ScrollRect sensitivity (40), independent of headset refresh rate.
    private const float ScrollUnitsPerSecond = 3f;

    public void Initialize(bool left, InputActionAsset inputActions)
    {
        string path = left ? "<XRController>{LeftHand}" : "<XRController>{RightHand}";
        position = new InputAction("Menu Aim Position", InputActionType.Value, path + "/pointerPosition", expectedControlType: "Vector3");
        rotation = new InputAction("Menu Aim Rotation", InputActionType.Value, path + "/pointerRotation", expectedControlType: "Quaternion");
        trigger = new InputAction("Menu Click", InputActionType.Button, path + "/triggerPressed");
        triggerValue = new InputAction("Menu Click Value", InputActionType.Value, path + "/trigger");
        string scrollPath = left ? "XRI Left Interaction/UI Scroll" : "XRI Right Interaction/UI Scroll";
        var sourceScroll = inputActions != null ? inputActions.FindAction(scrollPath, false) : null;
        if (sourceScroll != null)
        {
            // Retain the project's bindings/processors without depending on the disabled
            // ControllerInputActionManager or changing shared action enable/disable state.
            scroll = sourceScroll.Clone();
        }
        else Debug.LogError("Menu ray is missing the existing " + scrollPath + " action. Assign Menu Input Actions on DrumModeVisuals.", this);
        var ray = GetComponent<XRRayInteractor>();
        ray.enableUIInteraction = true;
        ray.blockUIOnInteractableSelection = false;
        ray.interactionLayers = 0;
        ray.raycastMask = 1 << 5;
        ray.uiPressInput = new XRInputButtonReader("Menu Click") {
            inputSourceMode = XRInputButtonReader.InputSourceMode.InputAction,
            inputActionPerformed = trigger,
            inputActionValue = triggerValue
        };
        scrollReader = new XRInputValueReader<Vector2>("Menu UI Scroll")
        {
            inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue
        };
        ray.uiScrollInput = scrollReader;
        var line = GetComponent<LineRenderer>();
        if (line != null)
        {
            var shader = Resources.Load<Shader>("QuestOverlayUI");
            if (shader != null)
            {
                var material = new Material(shader);
                material.renderQueue = 3100;
                line.material = material;
            }
        }
        // AddComponent may have already called OnEnable before Initialize assigned actions.
        if (isActiveAndEnabled) EnableActions();
    }

    private void EnableActions()
    {
        position?.Enable(); rotation?.Enable(); trigger?.Enable(); triggerValue?.Enable(); scroll?.Enable();
    }

    private void OnEnable()
    {
        EnableActions();
    }

    private void Update()
    {
        if (scrollReader != null)
        {
            // XRUIInputModule dispatches this only to the UI under this ray. Horizontal
            // input is ignored because Help is vertical, and the action/device deadzone
            // is retained. Do not scroll the ScrollRect directly or enable locomotion.
            Vector2 axis = scroll != null ? scroll.ReadValue<Vector2>() : Vector2.zero;
            scrollReader.manualValue = new Vector2(0f, axis.y) * (ScrollUnitsPerSecond * Time.unscaledDeltaTime);
        }
        if (position == null || rotation == null || position.activeControl == null) return;
        transform.localPosition = position.ReadValue<Vector3>();
        transform.localRotation = rotation.ReadValue<Quaternion>();
    }

    private void OnDisable()
    {
        if (scrollReader != null) scrollReader.manualValue = Vector2.zero;
        position?.Disable(); rotation?.Disable(); trigger?.Disable(); triggerValue?.Disable(); scroll?.Disable();
    }

    private void OnDestroy()
    {
        position?.Dispose(); rotation?.Dispose(); trigger?.Dispose(); triggerValue?.Dispose(); scroll?.Dispose();
    }
}
