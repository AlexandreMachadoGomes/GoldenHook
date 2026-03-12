using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads grapple-specific input from GrappleInputActions.inputactions.
/// Lives on one GameObject in the scene (shared by both hand controllers).
///
/// Design: fire events use action.performed callbacks (reliable, frame-safe).
/// Retract state is polled via IsPressed() — safe to read in FixedUpdate.
///
/// CONFLICT NOTE: The default InputSystem_Actions.inputactions binds
/// <XRController>/trigger to Sprint. GrappleInputActions uses per-hand
/// qualified paths ({LeftHand}/{RightHand}), so both will fire on trigger.
/// Sprint has no XR effect in this project, so the conflict is harmless.
/// </summary>
public class XRHandGrappleInput : MonoBehaviour
{
    [Header("Assign from GrappleInputActions asset")]
    [SerializeField] InputActionReference leftFireAction;
    [SerializeField] InputActionReference rightFireAction;
    [SerializeField] InputActionReference leftRetractAction;
    [SerializeField] InputActionReference rightRetractAction;

    // --- Fire (one-shot) ---
    // Set by callback, consumed+cleared by GrappleHandController via ConsumeXFire().
    bool _leftFirePending;
    bool _rightFirePending;

    // --- Retract (held) ---
    // Read directly from action; IsPressed() is stateless and FixedUpdate-safe.
    public bool LeftRetractHeld =>
        leftRetractAction != null && leftRetractAction.action.IsPressed();

    public bool RightRetractHeld =>
        rightRetractAction != null && rightRetractAction.action.IsPressed();

    /// <summary>Returns true and clears the pending flag. Call once per FixedUpdate.</summary>
    public bool ConsumeLeftFire()
    {
        if (!_leftFirePending) return false;
        _leftFirePending = false;
        return true;
    }

    public bool ConsumeRightFire()
    {
        if (!_rightFirePending) return false;
        _rightFirePending = false;
        return true;
    }

    // -----------------------------------------------------------------------

    void OnEnable()
    {
        Enable(leftFireAction);
        Enable(rightFireAction);
        Enable(leftRetractAction);
        Enable(rightRetractAction);

        if (leftFireAction != null)  leftFireAction.action.performed  += OnLeftFire;
        if (rightFireAction != null) rightFireAction.action.performed += OnRightFire;
    }

    void OnDisable()
    {
        if (leftFireAction != null)  leftFireAction.action.performed  -= OnLeftFire;
        if (rightFireAction != null) rightFireAction.action.performed -= OnRightFire;

        Disable(leftFireAction);
        Disable(rightFireAction);
        Disable(leftRetractAction);
        Disable(rightRetractAction);
    }

    void OnLeftFire(InputAction.CallbackContext _)  => _leftFirePending  = true;
    void OnRightFire(InputAction.CallbackContext _) => _rightFirePending = true;

    static void Enable(InputActionReference r)
    {
        if (r != null) r.action.Enable();
    }

    static void Disable(InputActionReference r)
    {
        if (r != null) r.action.Disable();
    }
}
