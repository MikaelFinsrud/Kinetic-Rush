using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public event Action OnJumpActionPerformed;

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
    }

    private void JumpPerformed(InputAction.CallbackContext context)
    {
        OnJumpActionPerformed?.Invoke();
    }

    public Vector2 GetMovementVectorNormalized()
    {
        Vector2 inputVector = playerInputActions.Player.Movement.ReadValue<Vector2>();

        inputVector = inputVector.normalized;

        return inputVector;
    }
}
