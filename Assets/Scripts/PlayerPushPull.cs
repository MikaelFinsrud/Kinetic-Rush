using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerPushPull : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private LayerMask environmentMask;   // ground, walls, etc.

    [Header("General")]
    [SerializeField] private float maxRange = 30f;
    [SerializeField] private float anchorCheckRadius = 0.3f;
    [SerializeField] private float anchorCheckDistance = 0.1f;

    [Header("Impulse (on button down)")]
    [SerializeField] private float baseImpulseStrength = 25f;

    [Header("Continuous Force (while holding)")]
    [SerializeField] private float continuousAccelStrength = 20f;

    [Header("Push/Pull Diminishing Returns")]
   
    [Header("Push/Pull Tuning")]
    [SerializeField] private float upwardPushPullMultiplier = 1.5f; // 1 = normal, <1 = weaker up, >1 = stronger up
    [SerializeField] private float pushPullSoftCapSpeed = 20f;      // speed along the force dir where we start heavily diminishing
    [SerializeField] private float minPushPullMultiplier = 0.15f;   // never scale below this


    // Input buffer flags (Update -> FixedUpdate).
    bool _pushHeld;
    bool _pullHeld;
    bool _pushPressedThisFrame;
    bool _pullPressedThisFrame;

    // Cached hit target each physics step.
    private PushPullTarget _currentTarget;
    private RaycastHit _currentHit;
    private Vector3 _currentTargetOrigin;

    private void Reset()
    {
        playerRb = GetComponent<Rigidbody>();
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
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
        HandlePushPull();

        // Clear one-frame input flags.
        _pushPressedThisFrame = false;
        _pullPressedThisFrame = false;
    }

    private void UpdateTargetFromCamera()
    {
        _currentTarget = null;
        _currentHit = default;

        if (playerCamera == null)
            return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out var hit, maxRange, interactableMask))
        {
            _currentHit = hit;
            _currentTarget = hit.collider.GetComponentInParent<PushPullTarget>();
            // Find a collider to get a reasonable origin (center of the object).

            if (_currentTarget != null)
            {
                Collider col = _currentTarget.GetComponentInChildren<Collider>();

                if (col != null)
                {
                    _currentTargetOrigin = col.bounds.center;
                }
            }
            
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
        Vector3 toTarget = (_currentTargetOrigin - playerRb.worldCenterOfMass);
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Vector3 dir = toTarget.normalized;

        if (_currentTarget.CanReceiveImpulse)
        {
            // Impulse on button down.
            if (_pushPressedThisFrame)
            {
                ApplyForce(_currentTarget, dir, isPull: false, baseImpulseStrength, ForceMode.Impulse);
                _currentTarget.RegisterImpulse();
            }
            if (_pullPressedThisFrame)
            {
                ApplyForce(_currentTarget, dir, isPull: true, baseImpulseStrength, ForceMode.Impulse);
                _currentTarget.RegisterImpulse();
            }
        }

        // Continuous small force while held.
        if (_pushHeld)
        {
            ApplyForce(_currentTarget, dir, isPull: false, continuousAccelStrength, ForceMode.Acceleration);
            _currentTarget.RegisterImpulse();
        }
        if (_pullHeld)
        {
            ApplyForce(_currentTarget, dir, isPull: true, continuousAccelStrength, ForceMode.Acceleration);
            _currentTarget.RegisterImpulse();
        }
    }

    private void ApplyForce(PushPullTarget target, Vector3 dirPlayerToTarget, bool isPull, float forceStrength, ForceMode forceMode)
    {

        Vector3 playerDir = isPull ? dirPlayerToTarget : -dirPlayerToTarget;
        Vector3 targetDir = isPull ? -dirPlayerToTarget : dirPlayerToTarget;

        Vector3 playerImpulse = playerDir * forceStrength;

        playerImpulse = ApplyPerAxisDiminishing(playerImpulse);

        if (playerImpulse.y > 0f)
        {
            playerImpulse.y *= upwardPushPullMultiplier;
        }

        float mPlayer = playerRb.mass;
        float mTarget = target.InteractionMass;

        // Special rule: free coin -> only coin moves.
        if (target.Kind == PushPullTarget.TargetKind.Coin && !target.IsAnchored && target.Body != null)
        {
            target.Body.AddForce(targetDir * forceStrength, forceMode);
            
            return;
        }

        bool anchoredForThisInteraction = target.IsAnchored || float.IsPositiveInfinity(mTarget) || IsTargetBlocked(target, targetDir);

        // Anchored targets: only move the player.
        if (anchoredForThisInteraction)
        {
            playerRb.AddForce(playerImpulse, forceMode);
            return;
        }

        // Mass-based split (heavier side moves less).
        float kPlayer = mTarget / (mPlayer + mTarget);
        float kTarget = mPlayer / (mPlayer + mTarget);

        playerImpulse = playerImpulse * kPlayer;
        Vector3 targetImpulse = targetDir * forceStrength * kTarget;

        playerRb.AddForce(playerImpulse, forceMode);

        if (target.Body != null)
            target.Body.AddForce(targetImpulse, forceMode);
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
        if (playerCamera == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(playerCamera.transform.position,
                        playerCamera.transform.position + playerCamera.transform.forward * maxRange);
    }
#endif
}
