using UnityEngine;

[DisallowMultipleComponent]
public sealed class Coin : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody _rb;

    private void Reset()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Launch(Vector3 velocity, Rigidbody launchParent)
    {
        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.linearVelocity = velocity;
        _rb.angularVelocity = Vector3.zero;
    }
}
