using UnityEngine;

[DisallowMultipleComponent]
public sealed class Coin : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody _rb;

    [Header("Tuning")]
    [SerializeField] private float gravityMultiplier = 2f;

    private void Awake()
    {
        GetComponent<AudioSource>().pitch = Random.Range(0.75f, 0.9f);
    }

    private void Reset()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        Vector3 velocity = _rb.linearVelocity;
        ApplyExtraGravity(ref velocity);
        _rb.linearVelocity = velocity;
    }

    private void OnCollisionEnter(Collision c)
    {
        // Take first contact (you can average normals if you want)
        AlignFlatToSurfaceNormal(c.GetContact(0).normal);
    }

    public void Launch(Vector3 velocity, float spinRadPerSec, Rigidbody launchParent)
    {
        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.linearVelocity = velocity;
        _rb.maxAngularVelocity = 70f;
        _rb.angularVelocity = transform.right * spinRadPerSec;
    }

    public void AlignFlatToSurfaceNormal(Vector3 surfaceNormal)
    {
        // coinFaceNormal is the axis that points out of the coin's flat face.
        Vector3 coinFaceNormal = transform.up; // change to transform.up / transform.right if needed

        Quaternion delta = Quaternion.FromToRotation(coinFaceNormal, -surfaceNormal);

        _rb.angularVelocity = Vector3.zero;
        transform.rotation = delta * transform.rotation;
    }

    private void ApplyExtraGravity(ref Vector3 velocity)
    {
        Vector3 gravity = Physics.gravity * gravityMultiplier;
        velocity += gravity * Time.fixedDeltaTime;
    }
}
