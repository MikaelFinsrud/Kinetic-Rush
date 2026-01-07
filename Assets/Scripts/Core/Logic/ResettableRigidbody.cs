using UnityEngine;

public sealed class ResettableRigidbody : MonoBehaviour, IResettable
{
    [SerializeField] private Rigidbody _rb;

    private State _initial;

    [System.Serializable]
    private struct State
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LocalScale;

        public Vector3 Velocity;
        public Vector3 AngularVelocity;

        public bool IsKinematic;
        public RigidbodyConstraints Constraints;
        public RigidbodyInterpolation Interpolation;
        public CollisionDetectionMode CollisionDetectionMode;
        // Add more only if you actually change it at runtime.
    }

    private void Reset()
    {
        if (_rb == null) _rb = GetComponentInParent<Rigidbody>();
    }

    private void Start()
    {
        CaptureInitialState();
    }

    public void CaptureInitialState()
    {
        var t = transform;

        _initial = new State
        {
            Position = t.position,
            Rotation = t.rotation,
            LocalScale = t.localScale,

            Velocity = _rb ? _rb.linearVelocity : default,
            AngularVelocity = _rb ? _rb.angularVelocity : default,

            IsKinematic = _rb && _rb.isKinematic,
            Constraints = _rb ? _rb.constraints : default,
            Interpolation = _rb ? _rb.interpolation : default,
            CollisionDetectionMode = _rb ? _rb.collisionDetectionMode : default,
        };
    }

    public void RestoreInitialState()
    {
        var t = transform;

        // Teleport first
        t.position = _initial.Position;
        t.rotation = _initial.Rotation;
        t.localScale = _initial.LocalScale;

        if (!_rb) return;

        // Restore rb “mode” first, then velocities
        _rb.isKinematic = _initial.IsKinematic;
        _rb.constraints = _initial.Constraints;
        _rb.interpolation = _initial.Interpolation;
        _rb.collisionDetectionMode = _initial.CollisionDetectionMode;

        _rb.linearVelocity = _initial.Velocity;
        _rb.angularVelocity = _initial.AngularVelocity;

        _rb.Sleep();
    }
}
