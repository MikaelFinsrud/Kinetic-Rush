using System;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Rigidbody-based Quake/CS style controller:
/// - Ground + air acceleration
/// - World-space velocity (looking around does NOT rotate your velocity)
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class KineticPlayerMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraRoot; // assign PlayerCameraRoot in inspector

    [Header("Movement")]
    [SerializeField] private float maxGroundSpeed = 10f;
    [SerializeField] private float groundAcceleration = 60f;
    [SerializeField] private float groundFriction = 8f;

    [SerializeField] private float maxAirSpeed = 10f;
    [SerializeField] private float airAcceleration = 20f;
    [Tooltip("Extra turning power while in air (0 = none, 1-5 = CS-style feel)")]
    [SerializeField] private float airControl = 2f;

    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.6f;
    [SerializeField] private float gravityMultiplier = 2f;
    [Tooltip("Fraction of horizontal speed kept on a normal standing/running jump (0 = none, 1 = keep all).")]
    [SerializeField, Range(0f, 1f)] private float normalJumpHorizontalRetention = 0.25f;
    [Tooltip("Maximum horizontal speed allowed on a normal jump after applying retention.")]
    [SerializeField] private float normalJumpMaxHorizontalSpeed = 6f;

    [Header("Jump Grace (Coyote Time)")]
    [SerializeField] private float coyoteTime = 0.1f; // 0.1s is a good start
    private float _coyoteTimer;

    [Header("Grounding")]
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private float groundCheckDistance = 0.6f;
    [SerializeField] private LayerMask groundLayers;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxPitch = 89f;


    private Rigidbody _rb;
    private CapsuleCollider _capsule;

    // Input (we use legacy Input for now; easy to swap to the new Input System later)
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpQueued;

    // Look state
    private float _yaw;
    private float _pitch;

    // Ground state
    private bool _isGrounded;
    private Vector3 _groundNormal = Vector3.up;


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();

        _rb.useGravity = false;      // we apply gravity manually
        _rb.freezeRotation = true;   // we rotate via script

        _yaw = transform.eulerAngles.y;
    }

    private void Start()
    {
        LockCursor();
    }

    private void OnDisable()
    {
        UnlockCursor();
    }

    private void Update()
    {
        ReadInput();
        HandleLook();
        HandleJumpQueue();
    }

    private void FixedUpdate()
    {
        CheckGround();
        ApplyMovement();
    }

    private void ReadInput()
    {
        // For now: old Input Manager. Later you can pipe in the new Input System here.
        _moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        _lookInput = new Vector2(
            Input.GetAxis("Mouse X"),
            Input.GetAxis("Mouse Y")
        );
    }

    private void HandleLook()
    {
        _yaw += _lookInput.x * mouseSensitivity;
        _pitch -= _lookInput.y * mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -maxPitch, maxPitch);

        // Rotate body only around Y (yaw)
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        // Rotate camera root for pitch
        if (cameraRoot != null)
        {
            cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    private void HandleJumpQueue()
    {
        if (Input.GetButtonDown("Jump"))
        {
            _jumpQueued = true;
        }
    }

    private void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        if (Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            _isGrounded = true;
            _groundNormal = hit.normal;
            Debug.Log("Grounded on normal: " + _groundNormal);
        }
        else
        {
            _isGrounded = false;
            _groundNormal = Vector3.up;
            Debug.Log("Not grounded!");
        }
    }

    private void ApplyMovement()
    {
        Vector3 velocity = _rb.linearVelocity;

        UpdateCoyoteTimer(velocity);

        if (_isGrounded)
        {
            ApplyGroundFriction(ref velocity);
            GroundMove(ref velocity);
        }
        else
        {
            AirMove(ref velocity);
            ApplyExtraGravity(ref velocity);
        }

        if (_jumpQueued)
        {
            bool canJumpFromGround = _isGrounded;
            bool canJumpFromCoyote = !_isGrounded && _coyoteTimer > 0f && velocity.y <= 0f;

            if (canJumpFromGround || canJumpFromCoyote)
            {
                // normal jump for now (not a slide-jump)
                Jump(ref velocity, false);
            }
        }

        _jumpQueued = false;
        _rb.linearVelocity = velocity;
    }

    private void UpdateCoyoteTimer(Vector3 velocity)
    {
        if (_isGrounded && velocity.y <= 0f)
        {
            // As long as we are on the ground (and not going up),
            // keep resetting the grace window.
            _coyoteTimer = coyoteTime;
        }
        else
        {
            // Count down when we’re in the air
            _coyoteTimer -= Time.fixedDeltaTime;
            if (_coyoteTimer < 0f)
            {
                _coyoteTimer = 0f;
            }
        }
    }

    #region Ground & Air Move

    private void GroundMove(ref Vector3 velocity)
    {
        Vector3 wishDir = GetWishDirectionOnPlane(_groundNormal);
        if (wishDir.sqrMagnitude < 0.0001f)
            return;

        Accelerate(ref velocity, wishDir, maxGroundSpeed, groundAcceleration);
    }

    private void AirMove(ref Vector3 velocity)
    {
        Vector3 wishDir = GetWishDirectionOnPlane(Vector3.up);
        if (wishDir.sqrMagnitude < 0.0001f)
            return;

        // Standard air acceleration
        Accelerate(ref velocity, wishDir, maxAirSpeed, airAcceleration);

        // Simple "air control" � allows redirecting more when already moving
        if (airControl > 0f)
        {
            float speed = velocity.magnitude;
            if (speed > 0.1f)
            {
                float dot = Vector3.Dot(velocity.normalized, wishDir);
                float controlAmount = airControl * dot * Time.fixedDeltaTime;
                if (controlAmount > 0f)
                {
                    Vector3 newDir = (velocity.normalized + wishDir * controlAmount).normalized;
                    velocity = newDir * speed;
                }
            }
        }
    }

    private void ApplyGroundFriction(ref Vector3 velocity)
    {
        Vector3 lateral = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float speed = lateral.magnitude;
        if (speed <= 0.0001f)
            return;

        float drop = speed * groundFriction * Time.fixedDeltaTime;
        float newSpeed = Mathf.Max(speed - drop, 0f);
        lateral *= newSpeed / speed;

        // keep vertical velocity
        velocity = new Vector3(lateral.x, velocity.y, lateral.z);
    }

    private void Accelerate(ref Vector3 velocity, Vector3 wishDir, float maxSpeed, float accel)
    {
        float currentSpeed = Vector3.Dot(velocity, wishDir);
        float addSpeed = maxSpeed - currentSpeed;
        if (addSpeed <= 0f)
            return;

        float accelSpeed = accel * Time.fixedDeltaTime * maxSpeed;
        if (accelSpeed > addSpeed)
            accelSpeed = addSpeed;

        velocity += wishDir * accelSpeed;
    }

    private Vector3 GetWishDirectionOnPlane(Vector3 planeNormal)
    {
        if (_moveInput.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        // Input direction in camera space (ignoring pitch)
        Vector3 camForward = cameraRoot != null
            ? Vector3.ProjectOnPlane(cameraRoot.forward, Vector3.up).normalized
            : transform.forward;
        Vector3 camRight = cameraRoot != null
            ? Vector3.ProjectOnPlane(cameraRoot.right, Vector3.up).normalized
            : transform.right;

        Vector3 wishDir = camForward * _moveInput.y + camRight * _moveInput.x;
        wishDir = Vector3.ProjectOnPlane(wishDir, planeNormal).normalized;

        return wishDir;
    }

    #endregion

    #region Jump & Gravity

    private void Jump(ref Vector3 velocity, bool preserveHorizontalMomentum)
    {
        // Remove any downward velocity before jump
        if (velocity.y < 0f)
            velocity.y = 0f;

        float gravity = Physics.gravity.magnitude * gravityMultiplier;
        float jumpVel = Mathf.Sqrt(2f * gravity * jumpHeight);

        // We'll rebuild velocity, so cache horizontal first
        Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float horizontalSpeed = horizontal.magnitude;

        // --- Horizontal handling ---
        if (!preserveHorizontalMomentum)
        {
            // Normal jump: bleed off most horizontal speed
            if (horizontalSpeed > 0.0001f)
            {
                float targetSpeed = horizontalSpeed * normalJumpHorizontalRetention;

                // Optional clamp so you can't keep crazy sprint/slide speed by jumping
                targetSpeed = Mathf.Min(targetSpeed, normalJumpMaxHorizontalSpeed);

                horizontal = horizontal.normalized * targetSpeed;
            }
            // If speed is already tiny, we just leave it as-is (effectively zero)
        }
        // else:
        //  Slide-jump case (later): keep full horizontal vector,
        //  maybe even boost it slightly.

        // --- Rebuild final velocity ---
        velocity = horizontal;
        velocity.y += jumpVel;

        // We just jumped: no longer grounded and no coyote grace
        _isGrounded = false;
        _coyoteTimer = 0f;
    }

    private void ApplyExtraGravity(ref Vector3 velocity)
    {
        Vector3 gravity = Physics.gravity * gravityMultiplier;
        velocity += gravity * Time.fixedDeltaTime;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // Visualize ground check
        Gizmos.color = Color.green;
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 end = origin + Vector3.down * groundCheckDistance;
        Gizmos.DrawWireSphere(end, groundCheckRadius);
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked; // centers + locks
        Cursor.visible = false;                   // hide cursor
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;   // free
        Cursor.visible = true;                    // show cursor
    }
}
