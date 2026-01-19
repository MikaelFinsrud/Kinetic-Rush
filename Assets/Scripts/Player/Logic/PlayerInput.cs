using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    public void Jump(InputAction.CallbackContext context)
    {
        Debug.Log("Player Jumped " + context.phase);
    }
}
