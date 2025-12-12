using UnityEngine;

[DisallowMultipleComponent]
public sealed class Coin : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody _rb;

    private void Awake()
    {
        GetComponent<AudioSource>().pitch = Random.Range(0.75f, 0.9f);
    }

    private void Reset()
    {
        _rb = GetComponent<Rigidbody>();
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
}
