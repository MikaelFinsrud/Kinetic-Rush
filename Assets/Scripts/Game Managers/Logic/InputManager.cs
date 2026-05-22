using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public event Action OnJumpActionPerformed;
    public event Action OnSlideActionStarted;
    public event Action OnSlideActionStopped;

    private PlayerInputActions playerInputActions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("InputManager already exists!.");
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();

        playerInputActions.Player.Jump.performed += JumpPerformed;
        playerInputActions.Player.Slide.performed += SlidePerformed;
        playerInputActions.Player.Slide.canceled += SlideCanceled;
    }

    private void JumpPerformed(InputAction.CallbackContext context)
    {
        OnJumpActionPerformed?.Invoke();
    }

    private void SlidePerformed(InputAction.CallbackContext context)
    {
        OnSlideActionStarted?.Invoke();
    }

    private void SlideCanceled(InputAction.CallbackContext context)
    {
        OnSlideActionStopped?.Invoke();
    }

    public Vector2 GetMovementVectorNormalized()
    {
        Vector2 inputVector = playerInputActions.Player.Movement.ReadValue<Vector2>();

        inputVector = inputVector.normalized;

        return inputVector;
    }

    public Vector2 GetLookInputVector()
    {
        Vector2 inputVector = new Vector2(
            Input.GetAxis("Mouse X"),
            Input.GetAxis("Mouse Y")
        );

        return inputVector;
    }

    public bool IsSlidingHeld()
    {
        return playerInputActions.Player.Slide.IsPressed();
    }
}
