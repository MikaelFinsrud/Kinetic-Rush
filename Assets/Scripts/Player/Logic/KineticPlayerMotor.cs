using System;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using static PushPullTarget;
using static UnityEngine.InputManagerEntry;

/// <summary>
/// Rigidbody-based Quake/CS style controller:
/// - Ground + air acceleration
/// - World-space velocity (looking around does NOT rotate your velocity)
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class KineticPlayerMotor : MonoBehaviour, IResettable
{
    public static KineticPlayerMotor Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform cameraRoot; // assign PlayerCameraRoot in inspector

    [Header("Movement")]
    [SerializeField] private float maxGroundSpeed = 10f;
    [SerializeField] private float groundAcceleration = 60f;
    [SerializeField] private float groundFriction = 8f;
    [SerializeField, Range(0f, 89f)]
    private float maxWalkableSlopeAngle = 50f;

    [SerializeField] private float maxAirSpeed = 10f;
    [SerializeField] private float airFriction = 8f;
    [SerializeField] private float airAcceleration = 20f;
    [Tooltip("Extra turning power while in air (0 = none, 1-5 = CS-style feel)")]
    [SerializeField] private float airControl = 2f;

    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.6f;
    [SerializeField] private float gravityMultiplier = 2f;
    [SerializeField] private float linearGravityDrag = 0.15f; // try 0.05–0.4
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

    [Header("Crouch / Slide Input")]
    [SerializeField] private KeyCode slideKey = KeyCode.LeftAlt;

    [Header("Slide")]
    [SerializeField] private float minSlideSpeed = 8f;
    [Tooltip("Instant speed added along slide direction when slide starts.")]
    [SerializeField] private float slideBoost = 2f;
    [Tooltip("If your current speed is above this, slide gives no extra boost.")]
    [SerializeField] private float slideBoostSpeedThreshold = 16f;
    [Tooltip("How quickly slide slows down. Much lower than ground friction.")]
    [SerializeField] private float slideFriction = 1f;
    [Tooltip("Maximum duration of a slide in seconds.")]
    [SerializeField] private float maxSlideTime = 0.8f;
    private float _slideTimer;
    [SerializeField] private float slideBufferTime = 0.1f;
    private float _slideBufferTimer;
    [SerializeField] private float slideCooldownTime = 0.2f;
    private float _slideCooldownTimer;
    [SerializeField, Range(0f, 89f)]
    private float infiniteDownhillMinAngle = 12f;
    [SerializeField, Range(-1f, 1f)]
    private float downhillAlignmentDot = 0.2f; // 0.0 = any downhill, 0.5 = must be clearly downhill
    [SerializeField, Range(0f, 5f)]
    private float downhillSlideGravityMultiplier;


    [Header("Crouch")]
    [Tooltip("Capsule height multiplier when crouched/ sliding.")]
    [SerializeField] private float crouchHeightMultiplier = 0.5f;
    [Tooltip("Ground move speed multiplier while crouched (not sliding).")]
    [SerializeField] private float crouchSpeedMultiplier = 0.5f;
    [Tooltip("Ground acceleration multiplier while crouched (not sliding).")]
    [SerializeField] private float crouchAccelerationMultiplier = 0.7f;

    [Header("Crouch / Slide Camera")]
    [Tooltip("How far to move the camera down when crouched (local Y). Negative value.")]
    [SerializeField] private float crouchCameraHeightOffset = -0.6f;
    [Tooltip("How far to move the camera down when sliding (local Y). Negative value.")]
    [SerializeField] private float slideCameraHeightOffset = -0.8f;
    [Tooltip("How fast the camera moves between standing and crouched height.")]
    [SerializeField] private float cameraCrouchLerpSpeed = 12f;

    // internal camera height state
    private float _standingCamLocalY;
    private float _crouchedCamLocalY;
    private float _slideCamLocalY;
    private float _targetCamLocalY;

    // State
    private bool _isCrouching;
    private bool _isSliding;
    private Vector3 _slideDirection;

    // input state
    private bool _slidePressed;
    private bool _slideHeld;
    private bool _slideReleased;

    // standing / crouched collider data
    private float _standingHeight;
    private Vector3 _standingCenter;
    private float _crouchedHeight;
    private Vector3 _crouchedCenter;

    // Input (we use legacy Input for now; easy to swap to the new Input System later)
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpQueued;

    // Look state
    private float _yaw;
    private float _pitch;

    // Ground state
    private bool _isGrounded;
    private bool _isOnSteepSlope;
    private Vector3 _groundNormal = Vector3.up;

    private Rigidbody _rb;
    private CapsuleCollider _capsule;

    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("KineticPlayerMotor already exists!.");
            Destroy(this.gameObject);
            return;
        }
        Instance = this;


        _rb = GetComponentInParent<Rigidbody>();
        _capsule = GetComponentInParent<CapsuleCollider>();

        _rb.useGravity = false;      // we apply gravity manually
        _rb.freezeRotation = true;   // we rotate via script

        _yaw = transform.eulerAngles.y;

        _standingHeight = _capsule.height;
        _standingCenter = _capsule.center;

        _crouchedHeight = _standingHeight * crouchHeightMultiplier;

        // shift center down so feet stay at the same place
        _crouchedCenter = _standingCenter;
        _crouchedCenter.y -= (_standingHeight - _crouchedHeight) * 0.5f;

        if (cameraRoot != null)
        {
            _standingCamLocalY = cameraRoot.localPosition.y;
            _crouchedCamLocalY = _standingCamLocalY + crouchCameraHeightOffset;
            _slideCamLocalY = _standingCamLocalY + slideCameraHeightOffset;
            _targetCamLocalY = _standingCamLocalY;
        }
    }

    private void Start()
    {
        CaptureInitialState();
        LockCursor();
    }

    private void OnEnable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnJumpActionPerformed += () => _jumpQueued = true;
            InputManager.Instance.OnSlideActionStarted += () => _slidePressed = true;
            InputManager.Instance.OnSlideActionStopped += () => _slidePressed = false;
        } 
    }

    private void OnDisable()
    {
        UnlockCursor();
    }

    private void Update()
    {
        ReadInput();
        HandleLook();
    }

    private void FixedUpdate()
    {
        CheckGround();
        ApplyMovement();
        ClearInput();
        //Debug.Log($"Velocity: {_rb.linearVelocity.magnitude:F2}");
    }

    private void LateUpdate()
    {
        UpdateCameraHeight();
    }

    private void ReadInput()
    {
        // For now: old Input Manager. Later you can pipe in the new Input System here.
        _moveInput = InputManager.Instance?.GetMovementVectorNormalized() ?? Vector2.zero;

        _lookInput = InputManager.Instance?.GetLookInputVector() ?? Vector2.zero;

        _slideHeld = InputManager.Instance?.IsSlidingHeld() ?? false;

        if (_slidePressed)
        {
            _slideBufferTimer = slideBufferTime;
        }
    }

    private void ClearInput()
    {
        _slidePressed = false;
        _slideReleased = false;
        _jumpQueued = false;
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

    private bool IsWalkable(Vector3 normal)
    {
        // Equivalent to Vector3.Angle(normal, Vector3.up) <= maxWalkableSlopeAngle
        float minDot = Mathf.Cos(maxWalkableSlopeAngle * Mathf.Deg2Rad);
        return Vector3.Dot(normal, Vector3.up) >= minDot;
    }

    private void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        if (Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            _groundNormal = hit.normal;
            _isGrounded = IsWalkable(_groundNormal);
            _isOnSteepSlope = !_isGrounded;
        }
        else
        {
            _isGrounded = false;
            _isOnSteepSlope = false;
            _groundNormal = Vector3.up;
        }
    }

    private void ConstrainVelocityToGroundPlane(ref Vector3 velocity)
    {
        // Remove any component into/out of the surface normal.
        // This stops the solver from repeatedly "correcting" you uphill.
        velocity = Vector3.ProjectOnPlane(velocity, _groundNormal);
    }


    private void UpdateSlideCooldownTimer()
    {
        if (_slideCooldownTimer > 0f)
        {
            _slideCooldownTimer -= Time.fixedDeltaTime;
            if (_slideCooldownTimer < 0f) _slideCooldownTimer = 0f;
        }
    }

    private void ApplyMovement()
    {
        Vector3 velocity = _rb.linearVelocity;

        UpdateSlideCooldownTimer();

        UpdateCoyoteTimer(velocity);

        HandleSlideCrouch(ref velocity);

        if (_isGrounded)
        {
            if (_isSliding)
            {
                SlideMove(ref velocity); // no normal ground friction
            }
            else
            {
                ApplyGroundFriction(ref velocity);
                GroundMove(ref velocity);
            }
        }
        else
        {
            ApplyAirFriction(ref velocity);

            if (!_isOnSteepSlope)
            {
                AirMove(ref velocity);
            }

            ApplyExtraGravity(ref velocity);
        }

        if (_jumpQueued)
        {
            bool canJumpFromGround = _isGrounded;
            bool canJumpFromCoyote = !_isGrounded && _coyoteTimer > 0f && velocity.y <= 0f;

            if (canJumpFromGround || canJumpFromCoyote)
            {
                bool slideJump = _isSliding;
                Jump(ref velocity, slideJump);

                if (_isSliding)
                {
                    StopSlide();
                }
            }
        }

        // Slide auto-stop conditions (after movement/jump)
        if (_isSliding)
        {
            bool infiniteDownhill = IsInfiniteDownhillSlide(velocity);

            _slideTimer -= Time.fixedDeltaTime;

            Vector3 horiz = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float speed = horiz.magnitude;

            // slideHeld: from your input (Alt) – pass into ApplyMovement/FixedUpdate as we discussed
            bool timeUp = !infiniteDownhill && _slideTimer <= 0f;
            bool releasedKey = !_slideHeld;
            bool leftGround = !_isGrounded;
            bool speedTooLow = speed < minSlideSpeed * 0.5f;

            if (timeUp || releasedKey || leftGround || speedTooLow)
            {
                StopSlide();
            }
        }

        if (_isGrounded && !_jumpQueued) // don't fight the jump impulse
        {
            ConstrainVelocityToGroundPlane(ref velocity);
        }

        _rb.linearVelocity = velocity;
    }

    private void TryStandUp()
    {
        // Check if there is room to stand
        float radius = _capsule.radius;
        float height = _standingHeight;

        Vector3 center = transform.TransformPoint(_standingCenter);
        Vector3 up = transform.up;

        float halfHeight = height * 0.5f;
        Vector3 top = center + up * (halfHeight - radius);
        Vector3 bottom = center - up * (halfHeight - radius);

        bool blocked = Physics.CheckCapsule(
            top,
            bottom,
            radius * 0.95f,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        if (!blocked)
        {
            _isCrouching = false;
            _capsule.height = _standingHeight;
            _capsule.center = _standingCenter;

            SetCrouchedCamera(false, false);
        }
        else
        {
            _isCrouching = true; // stay crouched
            _capsule.height = _crouchedHeight;
            _capsule.center = _crouchedCenter;

            SetCrouchedCamera(true, false);
        }
    }

    private bool IsInfiniteDownhillSlide(Vector3 velocity)
    {
        if (!_isGrounded || !_isSliding) return false;

        // slope angle in degrees
        float upDot = Mathf.Clamp(Vector3.Dot(_groundNormal, Vector3.up), -1f, 1f);
        float slopeAngle = Mathf.Acos(upDot) * Mathf.Rad2Deg;
        if (slopeAngle < infiniteDownhillMinAngle) return false;

        // downhill direction along the surface (gravity projected onto the surface)
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
        float downhillMag = downhill.magnitude;
        if (downhillMag < 0.0001f) return false;
        downhill /= downhillMag;

        // are we actually moving downhill along the surface?
        Vector3 alongSurfaceVel = Vector3.ProjectOnPlane(velocity, _groundNormal);
        float speed = alongSurfaceVel.magnitude;
        if (speed < 0.1f) return false;

        Vector3 dir = alongSurfaceVel / speed;
        return Vector3.Dot(dir, downhill) > downhillAlignmentDot;
    }


    private void SlideMove(ref Vector3 velocity)
    {
        // Only affect horizontal
        Vector3 tangent = Vector3.ProjectOnPlane(velocity, _groundNormal);
        float speed = tangent.magnitude;
        Vector3 g = Physics.gravity * gravityMultiplier;
        Vector3 gParallel = Vector3.ProjectOnPlane(g, _groundNormal); // lies along the surface

        if (speed > 0.0001f)
        {
            // Keep sliding mostly along stored slide direction
            Vector3 dir = _slideDirection.sqrMagnitude > 0.0001f
                ? _slideDirection
                : tangent.normalized;

            dir = Vector3.ProjectOnPlane(dir, _groundNormal);
            if (dir.sqrMagnitude < 0.000001f) dir = tangent / speed;
            dir.Normalize();

            // Apply small slide friction (much lower than groundFriction)
            float drop = slideFriction * Time.fixedDeltaTime;
            float newSpeed = Mathf.Max(speed - drop, 0f);
            tangent = dir * newSpeed;
        }

        // Combine surface motion + a bit of into-ground “stick”
        velocity = tangent + _groundNormal;

        // Add downhill acceleration (adds speed downhill naturally)
        velocity += gParallel * downhillSlideGravityMultiplier * Time.fixedDeltaTime;
    }

    private void HandleSlideCrouch(ref Vector3 velocity)
    {
        if (_slideBufferTimer > 0f)
        {
            _slideBufferTimer -= Time.fixedDeltaTime;
            if (_slideBufferTimer < 0f)
                _slideBufferTimer = 0f;
        }

        bool hasBufferedSlide = _slideBufferTimer > 0f;
        bool slideOnCooldown = _slideCooldownTimer > 0f;

        // Start slide / crouch on key press
        if (_slidePressed)
        {
            Debug.Log("Start sliding");
            if (_isGrounded)
            {
                Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
                float speed = horizontal.magnitude;

                if (!slideOnCooldown && speed >= minSlideSpeed)
                {
                    StartSlide(ref velocity, horizontal, speed);
                    _slideBufferTimer = 0f; // consume buffer
                }
                else
                {
                    StartCrouch(false);
                    _slideBufferTimer = 0f;
                }
            }
            else
            {

            }
        }
        else if (hasBufferedSlide && _isGrounded && !_isSliding && !slideOnCooldown)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float speed = horizontal.magnitude;

            if (speed >= minSlideSpeed)
            {
                StartSlide(ref velocity, horizontal, speed);
                _slideBufferTimer = 0f; // consume buffer
            }
        }

        // Try to stand when releasing key (if not sliding)
        if (_slideReleased && !_isSliding)
        {
            TryStandUp();
            _slideBufferTimer = 0f;
        }
    }

    private void StartCrouch(bool slide)
    {
        if (_isCrouching) return;

        _isCrouching = true;
        _capsule.height = _crouchedHeight;
        _capsule.center = _crouchedCenter;

        SetCrouchedCamera(!slide, slide);
    }

    private void StartSlide(ref Vector3 velocity, Vector3 horizontalVelocity, float speed)
    {
        if (_isSliding) return;

        _isSliding = true;
        _slideTimer = maxSlideTime;

        //Adjust capsule height
        if (!_isCrouching) 
        {
            StartCrouch(true);
        }
        else
        {
            SetCrouchedCamera(false, true);
        }

        // Slide direction from current movement, fallback to input
        if (horizontalVelocity.sqrMagnitude > 0.0001f)
        {
            _slideDirection = horizontalVelocity.normalized;
        }
        else
        {
            Vector3 wishDir = GetWishDirectionOnPlane(_groundNormal);
            _slideDirection = wishDir.sqrMagnitude > 0.0001f
                ? wishDir
                : transform.forward;
        }

        // Decide target speed
        float targetSpeed = speed;

        // Only give a boost if we are below the threshold
        if (speed < slideBoostSpeedThreshold)
        {
            targetSpeed = Mathf.Max(speed + slideBoost, minSlideSpeed);
            targetSpeed = Mathf.Min(targetSpeed, slideBoostSpeedThreshold);
        }
        // else: already fast enough, keep current speed (no extra boost)

        Vector3 newHorizontal = _slideDirection * targetSpeed;
        velocity = new Vector3(newHorizontal.x, velocity.y, newHorizontal.z);
    }

    private void StopSlide()
    {
        _isSliding = false;
        _slideCooldownTimer = slideCooldownTime;
        _slideTimer = 0f;

        TryStandUp();
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

        float maxSpeed = maxGroundSpeed;
        float accel = groundAcceleration;

        if (_isCrouching && !_isSliding)
        {
            maxSpeed *= crouchSpeedMultiplier;
            accel *= crouchAccelerationMultiplier;
        }

        Accelerate(ref velocity, wishDir, maxSpeed, groundAcceleration);
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
            // Split velocity into vertical and horizontal
            Vector3 vertical = Vector3.Project(velocity, Vector3.up);
            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up); // XZ only

            float hSpeed = horizontal.magnitude;
            if (hSpeed > 0.1f)
            {
                Vector3 hDir = horizontal.normalized;
                float dot = Vector3.Dot(hDir, wishDir);               // alignment in plane
                float controlAmount = airControl * dot * Time.fixedDeltaTime;

                if (controlAmount > 0f)
                {
                    // Bend horizontal direction only, preserve horizontal speed
                    Vector3 newHDir = (hDir + wishDir * controlAmount).normalized;
                    horizontal = newHDir * hSpeed;
                }
            }

            // Recombine horizontal + vertical
            velocity = horizontal + vertical;
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

    private void ApplyAirFriction(ref Vector3 velocity)
    {
        Vector3 lateral = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float speed = lateral.magnitude;
        if (speed <= 0.0001f)
            return;

        float drop = speed / 10 * airFriction * Time.fixedDeltaTime;
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
        {
            return;
        }

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

    private void SetCrouchedCamera(bool crouched, bool slide)
    {
        if (cameraRoot == null) return;

        if (crouched)
        {
            _targetCamLocalY = _crouchedCamLocalY;
        }
        else if (slide)
        {
            _targetCamLocalY = _slideCamLocalY;
        }
        else
        {
            _targetCamLocalY = _standingCamLocalY;
        }  
    }

    private void UpdateCameraHeight()
    {
        if (cameraRoot == null) return;

        Vector3 localPos = cameraRoot.localPosition;
        float currentY = localPos.y;
        float targetY = _targetCamLocalY;

        if (Mathf.Approximately(currentY, targetY))
            return;

        float t = Time.deltaTime * cameraCrouchLerpSpeed;
        float newY = Mathf.Lerp(currentY, targetY, t);

        localPos.y = newY;
        cameraRoot.localPosition = localPos;
    }





    private State _initial;

    [System.Serializable]
    private struct State
    {
        public float _coyoteTimer;
        public float _slideTimer;
        public float _slideBufferTimer;
        public float _slideCooldownTimer;

        // internal camera height state
        public float _standingCamLocalY;
        public float _crouchedCamLocalY;
        public float _slideCamLocalY;
        public float _targetCamLocalY;

        // State
        public bool _isCrouching;
        public bool _isSliding;
        public Vector3 _slideDirection;

        // input state
        public bool _slidePressed;
        public bool _slideHeld;
        public bool _slideReleased;

        // standing / crouched collider data
        public float _standingHeight;
        public Vector3 _standingCenter;
        public float _crouchedHeight;
        public Vector3 _crouchedCenter;

        // Input (we use legacy Input for now; easy to swap to the new Input System later)
        public Vector2 _moveInput;
        public Vector2 _lookInput;
        public bool _jumpQueued;

        // Look state
        public float _yaw;
        public float _pitch;

        // Ground state
        public bool _isGrounded;
        public Vector3 _groundNormal;
    }

    public void CaptureInitialState()
    {
        _initial = new State
        {
            _coyoteTimer = _coyoteTimer,
            _slideTimer = _slideTimer,
            _slideBufferTimer = _slideBufferTimer,
            _slideCooldownTimer = _slideCooldownTimer,
            _standingCamLocalY = _standingCamLocalY,
            _crouchedCamLocalY = _crouchedCamLocalY,
            _slideCamLocalY = _slideCamLocalY,
            _targetCamLocalY = _targetCamLocalY,
            _isCrouching = _isCrouching,
            _isSliding = _isSliding,
            _slideDirection = _slideDirection,
            _slidePressed = _slidePressed,
            _slideHeld = _slideHeld,
            _slideReleased = _slideReleased,
            _standingHeight = _standingHeight,
            _standingCenter = _standingCenter,
            _crouchedHeight = _crouchedHeight,
            _crouchedCenter = _crouchedCenter,
            _moveInput = _moveInput,
            _lookInput = _lookInput,
            _jumpQueued = _jumpQueued,
            _yaw = _yaw,
            _pitch = _pitch,
            _isGrounded = _isGrounded,
            _groundNormal = _groundNormal
        };
    }

    public void RestoreInitialState()
    {
        LockCursor();

        _coyoteTimer = _initial._coyoteTimer;
        _slideTimer = _initial._slideTimer;
        _slideBufferTimer = _initial._slideBufferTimer;
        _slideCooldownTimer = _initial._slideCooldownTimer;
        _standingCamLocalY = _initial._standingCamLocalY;
        _crouchedCamLocalY = _initial._crouchedCamLocalY;
        _slideCamLocalY = _initial._slideCamLocalY;
        _targetCamLocalY = _initial._targetCamLocalY;
        _isCrouching = _initial._isCrouching;
        _isSliding = _initial._isSliding;
        _slideDirection = _initial._slideDirection;
        _slidePressed = _initial._slidePressed;
        _slideHeld = _initial._slideHeld;
        _slideReleased = _initial._slideReleased;
        _standingHeight = _initial._standingHeight;
        _standingCenter = _initial._standingCenter;
        _crouchedHeight = _initial._crouchedHeight;
        _crouchedCenter = _initial._crouchedCenter;
        _moveInput = _initial._moveInput;
        _lookInput = _initial._lookInput;
        _jumpQueued = _initial._jumpQueued;
        _yaw = _initial._yaw;
        _pitch = _initial._pitch;
        _isGrounded = _initial._isGrounded;
        _groundNormal = _initial._groundNormal;
    }
}
