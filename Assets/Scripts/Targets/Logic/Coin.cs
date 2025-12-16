using Unity.VisualScripting;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Coin : MonoBehaviour, IResettable
{
    [Header("Refs")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private PushPullTarget _pushPullTarget;

    [Header("Tuning")]
    [SerializeField] private float gravityMultiplier = 2f;
    [SerializeField] private float linearGravityDrag;
    [SerializeField] private float impulseBackToPlayerBuffer = 0.2f;

    private float _noImpulseBackToPlayerTime;
    private bool canImpulseBackToPlayer = false;
    public bool isAttached = false;
    

    private void Awake()
    {
        GetComponent<AudioSource>().pitch = Random.Range(0.75f, 0.9f);
    }

    private void Start()
    {
        _pushPullTarget.OnPushed += HandleImpulse;
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

        if (canImpulseBackToPlayer && Time.time <= _noImpulseBackToPlayerTime)
        {
            ImpulseBackToPlayer();
            canImpulseBackToPlayer = false;
        }
    }

    public void Launch(Vector3 velocity, float spinRadPerSec, Rigidbody launchParent)
    {
        transform.parent = null;
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
        Vector3 g = Physics.gravity * gravityMultiplier;
        Vector3 gDir = g.normalized;

        float vAlongG = Vector3.Dot(velocity, gDir); // + = falling

        // Only apply drag when moving along gravity (falling)
        if (vAlongG > 0f)
        {
            // Drag acceleration magnitude (linear + quadratic)
            float dragAcc = (linearGravityDrag * vAlongG);

            // Drag points opposite the fall direction (against gravity direction)
            velocity += (-gDir * dragAcc) * Time.fixedDeltaTime;
        }

        // Gravity always applies
        velocity += g * Time.fixedDeltaTime;
    }

    private void HandleImpulse()
    {
        _noImpulseBackToPlayerTime = Time.time + impulseBackToPlayerBuffer;
        canImpulseBackToPlayer = true;
    }

    private void Detach() 
    {
        transform.parent = null;
        _rb.constraints = RigidbodyConstraints.None;
        isAttached = false;
    }

    public void Attach()
    {
        _rb.constraints = RigidbodyConstraints.FreezePosition;
        isAttached = true;
    }

    private void ImpulseBackToPlayer()
    {
        if (PlayerPushPull.Instance != null)
        {
            PlayerPushPull.Instance.ImpulseBackToPlayer(_pushPullTarget);
        }
    }

    public void CaptureInitialState()
    {
        
    }

    public void RestoreInitialState()
    {
        Destroy(gameObject);
    }
}
