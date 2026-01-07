using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public class PushPullTarget : MonoBehaviour, IResettable
{
    public enum TargetKind
    {
        GenericAlwaysAnchored,
        GenericLoose,
        Coin
    }

    [Header("Push/Pull Settings")]
    [SerializeField] private TargetKind kind = TargetKind.GenericAlwaysAnchored;
    [SerializeField] private float impulseCooldown = 0.25f;
    // "Interaction mass" used for how much this influences vs is influenced by the player.
    // If zero/negative, we fall back to Rigidbody.mass.
    [SerializeField] private float interactionMass = 50f;
    // When anchored, we treat this as effectively infinite mass (world-attached).

    private float _nextPushImpulseTime;
    private float _nextPullImpulseTime;

    public TargetKind Kind => kind;
    public bool IsAnchored { get; private set; }

    public Rigidbody Body { get; private set; }

    public event Action OnHighlighted;
    public event Action OnUnHighlighted;
    public event Action OnPushed;
    public event Action OnPulled;


    public float InteractionMass
    {
        get
        {
            if (IsAnchored)
                return float.PositiveInfinity;

            if (interactionMass > 0f)
                return interactionMass;

            if (Body != null)
                return Body.mass;

            return 50f;
        }
    }

    public bool CanReceivePushImpulse => Time.time >= _nextPushImpulseTime;
    public bool CanReceivePullImpulse => Time.time >= _nextPullImpulseTime;

    public void RegisterImpulse(bool isPush)
    {
        if (isPush)
        {
            _nextPushImpulseTime = Time.time + impulseCooldown;

            OnPushed?.Invoke();
        }
        else
        {
            _nextPullImpulseTime = Time.time + impulseCooldown;

            OnPulled?.Invoke();
        }
    }

    public void Target()
    {
        OnHighlighted?.Invoke();
    }

    public void Untarget()
    {
        OnUnHighlighted?.Invoke();
        SetAnchored(false);
    }

    private void Awake()
    {
        Body = GetComponentInParent<Rigidbody>();
        IsAnchored = kind == TargetKind.GenericAlwaysAnchored;
    }

    private void Start()
    {
        CaptureInitialState();
    }

    /// <summary>
    /// Call this from your coin logic when it hits / sticks to a surface.
    /// </summary>
    public void SetAnchored(bool value)
    {
        if (kind == TargetKind.GenericAlwaysAnchored) { return; }

        IsAnchored = value;

        if (value && Body != null)
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.constraints = RigidbodyConstraints.FreezePosition;

        }
        else if (!value && Body != null)
        {
            Body.constraints = RigidbodyConstraints.None;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsAnchored ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
#endif






    private State _initial;

    [System.Serializable]
    private struct State
    {
        public float interactionMass;
        public float _nextPushImpulseTime;
        public float _nextPullImpulseTime;
        public TargetKind Kind;
        public bool IsAnchored;
    }

    public void CaptureInitialState()
    {
        _initial = new State
        {
            interactionMass = interactionMass,
            _nextPushImpulseTime = 0f,
            _nextPullImpulseTime = 0f,
            Kind = kind,
            IsAnchored = IsAnchored,
        };
    }

    public void RestoreInitialState()
    {
        interactionMass = _initial.interactionMass;
        _nextPushImpulseTime = _initial._nextPushImpulseTime;
        _nextPullImpulseTime = _initial._nextPullImpulseTime;
        kind = _initial.Kind;
        IsAnchored = _initial.IsAnchored;
    }
}
