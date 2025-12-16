using System;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody))]
public class PlayerPushPull : MonoBehaviour, IResettable
{
    public static PlayerPushPull Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private PlayerEnergy energy;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private LayerMask environmentMask;   // ground, walls, etc.

    [Header("General")]
    [SerializeField] private float maxRange = 30f;
    [SerializeField] private float anchorCheckDistance = 0.1f;

    [Header("Impulse (on button down)")]
    [SerializeField] private float baseImpulseStrength = 25f;
    [Min(0f)][SerializeField] private float _bigImpulseCost = 20f;

    [Header("Continuous Force (while holding)")]
    [SerializeField] private float continuousAccelStrength = 20f;
    [Min(0f)][SerializeField] private float _continuousCostPerSecond = 12f;

    [Header("Push/Pull Tuning")]
    [SerializeField] private float upwardPushPullMultiplier = 1.5f; // 1 = normal, <1 = weaker up, >1 = stronger up
    [SerializeField] private float pushPullSoftCapSpeed = 20f;      // speed along the force dir where we start heavily diminishing
    [SerializeField] private float minPushPullMultiplier = 0.15f;   // never scale below this


    private Camera _playerCamera;

    // Input buffer flags (Update -> FixedUpdate).
    private bool _pushHeld;
    private bool _pullHeld;
    private bool _pushPressedThisFrame;
    private bool _pullPressedThisFrame;
    private bool _isPushing;
    private bool _isPulling;

    // Cached hit target each physics step.
    private PushPullTarget _currentTarget;
    private PushPullTarget _lastTarget;

    private const int MaxHits = 32;
    private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
    private RaycastHit _currentHit;
    private Vector3 _currentTargetOrigin;


    public event Action<bool> OnPush;
    public event Action<bool> OnPull;
    public event Action OnStopPush;
    public event Action OnStopPull;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("PlayerPushPull already exists!.");
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
        }

        if (energy == null) energy = GetComponentInParent<PlayerEnergy>();
        if (energy == null) Debug.LogError("Missing PlayerEnergy reference.", this);
    }

    private void Reset()
    {
        playerRb = GetComponent<Rigidbody>();
        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
        }
    }

    private void Start()
    {
        CaptureInitialState();
    }

    private void Update()
    {
        // You can replace this with Input System bindings later.
        _pushHeld = Input.GetMouseButton(0);
        _pullHeld = Input.GetMouseButton(1);

        if (Input.GetMouseButtonDown(0))
            _pushPressedThisFrame = true;

        if (Input.GetMouseButtonDown(1))
            _pullPressedThisFrame = true;
    }

    private void FixedUpdate()
    {
        UpdateTargetFromCamera();
        HandleStopPushPulling();
        HandlePushPull();

        // Clear one-frame input flags.
        _pushPressedThisFrame = false;
        _pullPressedThisFrame = false;
    }

    private void HandleStopPushPulling()
    {
        if (_isPulling && !_pullHeld && !_pullPressedThisFrame)
        {
            StopPulling();
        }

        if (_isPushing && !_pushHeld && !_pushPressedThisFrame)
        {
            StopPushing();
        }

        if ((_isPulling || _isPushing) && _currentTarget == null)
        {
            if (_isPulling)
            {
                StopPulling();
            }
            if (_isPushing)
            {
                StopPushing();
            }
        }
    }

    private void StopPulling()
    {
        _isPulling = false;
        OnStopPull?.Invoke();

        if (_currentTarget != null)
        {
            _currentTarget.SetAnchored(false);
        }
    }

    private void StopPushing()
    {
        _isPushing = false;
        OnStopPush?.Invoke();

        if (_currentTarget != null)
        {
            _currentTarget.SetAnchored(false);
        }
    }

    public bool TryGetBestTarget(out PushPullTarget best)
    {
        best = null;

        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

        int count = Physics.RaycastNonAlloc(
            ray, _hits, maxRange, interactableMask);

        if (count <= 0) return false;

        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider col = _hits[i].collider;
            if (!col) continue;

            PushPullTarget t = col.GetComponent<PushPullTarget>();
            if (!t) continue;

            // Pick a representative point on the collider that’s closest to the aim ray
            Vector3 center = col.bounds.center;

            float depth = Vector3.Dot(center - ray.origin, ray.direction);
            if (depth < 0f) continue; // behind camera

            Vector3 pointOnRay = ray.origin + ray.direction * Mathf.Clamp(depth, 0f, maxRange);

            // Perpendicular distance from aim ray to the target center
            Vector3 toPoint = center - ray.origin;
            float lateralDist = Vector3.Cross(ray.direction, toPoint).magnitude;

            // Small bias toward closer targets so far-away stuff doesn’t “steal” selection
            float distanceAlongRay = Vector3.Dot(toPoint, ray.direction);

            // Optional: don’t select targets through walls
            if (Physics.Raycast(ray.origin, (center - ray.origin).normalized,
                    out RaycastHit blockHit, distanceAlongRay, environmentMask, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            float score = lateralDist + (distanceAlongRay * 0.01f); // tune the 0.01f
            if (score < bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        return best != null;
    }

    private void UpdateTargetFromCamera()
    {
        _lastTarget = _currentTarget;

        _currentTarget = null;
        _currentHit = default;

        if (_playerCamera == null)
            return;

        if (TryGetBestTarget(out PushPullTarget bestTarget))
        {
            _currentTarget = bestTarget;
            Collider col = _currentTarget.GetComponentInChildren<Collider>();
            if (col != null)
            {
                _currentTargetOrigin = col.bounds.center;
            }
        }

        if (_currentTarget != null && _currentTarget != _lastTarget)
        {
            if (_lastTarget != null)
            {
                _lastTarget.Untarget();
            }

            _currentTarget.Target();
        }

        if (_currentTarget == null && _lastTarget != null)
        {
            _lastTarget.Untarget();
        }
    }

    private void HandlePushPull()
    {
        if (_currentTarget == null)
            return;

        bool push = _pushHeld || _pushPressedThisFrame;
        bool pull = _pullHeld || _pullPressedThisFrame;

        if (!push && !pull)
            return;

        // Direction from player to target in world space.
        Vector3 dir = CalculateTargetDir(_currentTargetOrigin);

        if (_currentTarget.CanReceiveImpulse && energy.TrySpendInstant(_bigImpulseCost))
        {
            // Impulse on button down.
            if (_pushPressedThisFrame)
            {
                if (_isPulling)
                {
                    StopPulling();
                }

                OnPush?.Invoke(true);
                _isPushing = true;
                ApplyForce(_currentTarget, dir, isPull: false, baseImpulseStrength, ForceMode.Impulse);
                _currentTarget.RegisterImpulse(true);

                return;
            }
            if (_pullPressedThisFrame)
            {
                if (_isPushing)
                {
                    StopPushing();
                }

                _isPulling = true;
                OnPull?.Invoke(true);
                ApplyForce(_currentTarget, dir, isPull: true, baseImpulseStrength, ForceMode.Impulse);
                _currentTarget.RegisterImpulse(false);

                return;
            }
        }

        // Continuous small force while held.
        if (_pushHeld)
        {
            if (_isPulling)
            {
                StopPulling();
            }

            float fraction = energy.SpendContinuous(_continuousCostPerSecond, Time.fixedDeltaTime);

            if (fraction <= 0f)
            {
                // Not enough energy to continue.
                if (_isPushing)
                {
                    StopPushing();
                }

                return;
            }

            if (!_isPushing)
            {
                _isPushing = true;
                OnPush?.Invoke(false);
            }

            ApplyForce(_currentTarget, dir, isPull: false, continuousAccelStrength * fraction, ForceMode.Acceleration);
            _currentTarget.RegisterImpulse(true);

            return;
        }

        if (_pullHeld)
        {
            if (_isPushing)
            {
                StopPushing();
            }

            float fraction = energy.SpendContinuous(_continuousCostPerSecond, Time.fixedDeltaTime);

            if (fraction <= 0f)
            {
                // Not enough energy to continue.
                if (_isPulling)
                {
                    StopPulling();
                }

                return;
            }

            if (!_isPulling)
            {
                _isPulling = true;
                OnPull?.Invoke(false);
            }

            ApplyForce(_currentTarget, dir, isPull: true, continuousAccelStrength * fraction, ForceMode.Acceleration);
            _currentTarget.RegisterImpulse(false);

            return;
        }
    }

    private void ApplyForce(PushPullTarget target, Vector3 dirPlayerToTarget, bool isPull, float forceStrength, ForceMode forceMode, bool onlyPlayer = false)
    {
        Debug.Log($"ApplyForce: target={target.name}, isPull={isPull}, strength={forceStrength}, mode={forceMode}, onlyPlayer={onlyPlayer}");
        Vector3 playerDir = isPull ? dirPlayerToTarget : -dirPlayerToTarget;
        Vector3 targetDir = isPull ? -dirPlayerToTarget : dirPlayerToTarget;

        Vector3 playerImpulse = playerDir * forceStrength;

        playerImpulse = ApplyPerAxisDiminishing(playerImpulse);

        if (playerImpulse.y > 0f)
        {
            playerImpulse.y *= upwardPushPullMultiplier;
        }

        if (onlyPlayer)
        {
            AddForcePlayer(playerImpulse, forceMode);
            return;
        }

        if (target.Kind != PushPullTarget.TargetKind.GenericAlwaysAnchored && !target.IsAnchored && IsTargetBlocked(target, targetDir))
        {
            target.SetAnchored(true);
        }

        if (target.Kind != PushPullTarget.TargetKind.GenericAlwaysAnchored && target.IsAnchored && !IsTargetBlocked(target, targetDir))
        {
            target.SetAnchored(false);
        }

        // Special rule: free coin -> only coin moves.
        if (target.Kind == PushPullTarget.TargetKind.Coin && !target.IsAnchored && target.Body != null)
        {
            Debug.Log("Add force!");
            Vector3 coinDir = isPull ? targetDir : _playerCamera.transform.forward;
            target.Body.AddForce(coinDir * forceStrength, forceMode);

            return;
        }

        float mPlayer = playerRb.mass;
        float mTarget = target.InteractionMass;

        bool anchoredForThisInteraction = float.IsPositiveInfinity(mTarget);

        // Anchored targets: only move the player.
        if (anchoredForThisInteraction)
        {
            AddForcePlayer(playerImpulse, forceMode);
            return;
        }

        // Mass-based split (heavier side moves less).
        float kPlayer = mTarget / (mPlayer + mTarget);
        float kTarget = mPlayer / (mPlayer + mTarget);

        playerImpulse = playerImpulse * kPlayer;
        Vector3 targetImpulse = targetDir * forceStrength * kTarget;

        AddForcePlayer(playerImpulse, forceMode);

        if (target.Body != null)
        {
            target.Body.AddForce(targetImpulse, forceMode);
            Debug.Log("Add force to target!");
        }
    }

    private Vector3 CalculateTargetDir(Vector3 targetOrigin)
    {
        Vector3 toTarget = (targetOrigin - playerRb.worldCenterOfMass);

        Vector3 dir = toTarget.normalized;

        return dir;
    }

    private void AddForcePlayer(Vector3 playerImpulse, ForceMode forceMode)
    {
        playerRb.AddForce(playerImpulse, forceMode);
    }

    public void ImpulseBackToPlayer(PushPullTarget target)
    {
        Collider col = target.GetComponentInChildren<Collider>();
        Vector3 targetOrigin = Vector3.zero;
        Vector3 dir = Vector3.zero;

        if (col != null)
        {
            targetOrigin = col.bounds.center;
            dir = CalculateTargetDir(targetOrigin);
        }
        else
        {
            return;
        }

        ApplyForce(target, dir, false, baseImpulseStrength, ForceMode.Impulse, true);
    }

    private bool IsTargetBlocked(PushPullTarget target, Vector3 targetDir)
    {
        if (target == null)
            return false;

        Vector3 dir = targetDir.normalized;
        if (dir.sqrMagnitude < 0.0001f)
            return false;

        if (_currentTargetOrigin == null) { return false; }

        // Long ray forward: from target towards environment
        if (!Physics.Raycast(_currentTargetOrigin,
                             dir,
                             out RaycastHit envHit,
                             100f,
                             environmentMask,
                             QueryTriggerInteraction.Ignore))
        {
            // No environment in this direction at all.
            return false;
        }

        // Ray back from environment towards targets, but only a *short* distance
        // Start slightly "inside" the gap towards the target so we don't sit exactly on the environment plane.
        const float skin = 0.02f;
        Vector3 backOrigin = envHit.point + dir * skin;
        float backDistance = anchorCheckDistance + skin;

        if (Physics.Raycast(backOrigin,
                            -dir,
                            out RaycastHit backHit,
                            backDistance,
                            interactableMask,
                            QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        // Either there was a big gap, or we hit something else, or nothing at all.
        return false;
    }

    private float ApplyAxisSoftCap(float forceAxis, float velocityAxis)
    {
        // No force on this axis? Nothing to do.
        if (Mathf.Approximately(forceAxis, 0f))
            return forceAxis;

        // If we're not already moving in the SAME direction on this axis, don't diminish:
        // - v == 0  -> full force (we're starting movement)
        // - opposite sign -> full force (we're braking/turning)
        if (Mathf.Sign(forceAxis) != Mathf.Sign(velocityAxis) || velocityAxis == 0f)
            return forceAxis;

        float speed = Mathf.Abs(velocityAxis);

        // 0..1 where 1 means we've reached or exceeded the soft cap on this axis
        float t = Mathf.Clamp01(speed / pushPullSoftCapSpeed);

        // Smooth curve: gentle reduction at low speed, stronger near cap
        t = t * t;

        float multiplier = Mathf.Lerp(1f, minPushPullMultiplier, t);
        return forceAxis * multiplier;
    }

    private Vector3 ApplyPerAxisDiminishing(Vector3 playerForce)
    {
        Vector3 v = playerRb.linearVelocity;

        playerForce.x = ApplyAxisSoftCap(playerForce.x, v.x);
        playerForce.y = ApplyAxisSoftCap(playerForce.y, v.y);
        playerForce.z = ApplyAxisSoftCap(playerForce.z, v.z);

        return playerForce;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_playerCamera == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(_playerCamera.transform.position,
                        _playerCamera.transform.position + _playerCamera.transform.forward * maxRange);
    }
#endif






    private State _initial;

    [System.Serializable]
    private struct State
    {
        public Camera _playerCamera;

        // Input buffer flags (Update -> FixedUpdate).
        public bool _pushHeld;
        public bool _pullHeld;
        public bool _pushPressedThisFrame;
        public bool _pullPressedThisFrame;
        public bool _isPushing;
        public bool _isPulling;

        // Cached hit target each physics step.
        public PushPullTarget _currentTarget;
        public PushPullTarget _lastTarget;

        public RaycastHit _currentHit;
        public Vector3 _currentTargetOrigin;
    }

    public void CaptureInitialState()
    {
        _initial = new State
        {
            _playerCamera = _playerCamera,
            _pushHeld = _pushHeld,
            _pullHeld = _pullHeld,
            _pushPressedThisFrame = _pushPressedThisFrame,
            _pullPressedThisFrame = _pullPressedThisFrame,
            _isPushing = _isPushing,
            _isPulling = _isPulling,
            _currentTarget = _currentTarget,
            _lastTarget = _lastTarget,
            _currentHit = _currentHit,
            _currentTargetOrigin = _currentTargetOrigin
        };
    }

    public void RestoreInitialState()
    {
        if (_lastTarget != null) { _lastTarget.Untarget(); }
        if (_currentTarget != null) { _currentTarget.Untarget(); }

        _playerCamera = _initial._playerCamera;
        _pushHeld = _initial._pushHeld;
        _pullHeld = _initial._pullHeld;
        _pushPressedThisFrame = _initial._pushPressedThisFrame;
        _pullPressedThisFrame = _initial._pullPressedThisFrame;
        _isPushing = _initial._isPushing;
        _isPulling = _initial._isPulling;
        _currentTarget = _initial._currentTarget;
        _lastTarget = _initial._lastTarget;
        _currentHit = _initial._currentHit;
        _currentTargetOrigin = _initial._currentTargetOrigin;

        OnStopPull?.Invoke();
        OnStopPush?.Invoke();
    }
}
