using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerPushPull : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask interactableMask;

    [Header("General")]
    [SerializeField] private float maxRange = 30f;

    [Header("Impulse (on button down)")]
    [SerializeField] private float baseImpulseStrength = 25f;

    [Header("Continuous Force (while holding)")]
    [SerializeField] private float continuousAccelStrength = 20f;

    // Input buffer flags (Update -> FixedUpdate).
    bool _pushHeld;
    bool _pullHeld;
    bool _pushPressedThisFrame;
    bool _pullPressedThisFrame;

    // Cached hit target each physics step.
    PushPullTarget _currentTarget;
    RaycastHit _currentHit;

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

        if (Physics.Raycast(ray, out var hit, maxRange, interactableMask, QueryTriggerInteraction.Ignore))
        {
            _currentHit = hit;
            _currentTarget = hit.collider.GetComponentInParent<PushPullTarget>();
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
        Vector3 toTarget = (_currentHit.point - playerRb.worldCenterOfMass);
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Vector3 dir = toTarget.normalized;

        // Impulse on button down.
        if (_pushPressedThisFrame)
        {
            ApplyImpulse(_currentTarget, dir, isPull: false);
        }
        if (_pullPressedThisFrame)
        {
            ApplyImpulse(_currentTarget, dir, isPull: true);
        }

        // Continuous small force while held.
        if (_pushHeld)
        {
            ApplyContinuousForce(_currentTarget, dir, isPull: false);
        }
        if (_pullHeld)
        {
            ApplyContinuousForce(_currentTarget, dir, isPull: true);
        }
    }

    private void ApplyImpulse(PushPullTarget target, Vector3 dirPlayerToTarget, bool isPull)
    {
        Vector3 playerDir = isPull ? dirPlayerToTarget : -dirPlayerToTarget;
        Vector3 targetDir = isPull ? -dirPlayerToTarget : dirPlayerToTarget;

        float mPlayer = playerRb.mass;
        float mTarget = target.InteractionMass;

        // Special rule: free coin -> only coin moves.
        if (target.Kind == PushPullTarget.TargetKind.Coin && !target.IsAnchored)
        {
            if (target.Body != null)
            {
                target.Body.AddForce(targetDir * baseImpulseStrength, ForceMode.Impulse);
            }
            return;
        }

        // Anchored targets: only move the player.
        if (float.IsPositiveInfinity(mTarget) || target.IsAnchored)
        {
            playerRb.AddForce(playerDir * baseImpulseStrength, ForceMode.Impulse);
            return;
        }

        // Mass-based split (heavier side moves less).
        float kPlayer = mTarget / (mPlayer + mTarget);
        float kTarget = mPlayer / (mPlayer + mTarget);

        Vector3 playerImpulse = playerDir * baseImpulseStrength * kPlayer;
        Vector3 targetImpulse = targetDir * baseImpulseStrength * kTarget;

        playerRb.AddForce(playerImpulse, ForceMode.Impulse);

        if (target.Body != null)
            target.Body.AddForce(targetImpulse, ForceMode.Impulse);
    }

    private void ApplyContinuousForce(PushPullTarget target, Vector3 dirPlayerToTarget, bool isPull)
    {
        Vector3 playerDir = isPull ? dirPlayerToTarget : -dirPlayerToTarget;
        Vector3 targetDir = isPull ? -dirPlayerToTarget : dirPlayerToTarget;

        float mPlayer = playerRb.mass;
        float mTarget = target.InteractionMass;

        // Free coin: only coin moves.
        if (target.Kind == PushPullTarget.TargetKind.Coin && !target.IsAnchored)
        {
            if (target.Body != null)
            {
                target.Body.AddForce(targetDir * continuousAccelStrength, ForceMode.Acceleration);
            }
            return;
        }

        // Anchored: only move player.
        if (float.IsPositiveInfinity(mTarget) || target.IsAnchored)
        {
            playerRb.AddForce(playerDir * continuousAccelStrength, ForceMode.Acceleration);
            return;
        }

        float kPlayer = mTarget / (mPlayer + mTarget);
        float kTarget = mPlayer / (mPlayer + mTarget);

        Vector3 playerAccel = playerDir * continuousAccelStrength * kPlayer;
        Vector3 targetAccel = targetDir * continuousAccelStrength * kTarget;

        playerRb.AddForce(playerAccel, ForceMode.Acceleration);

        if (target.Body != null)
            target.Body.AddForce(targetAccel, ForceMode.Acceleration);
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
