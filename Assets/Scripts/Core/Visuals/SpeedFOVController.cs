using UnityEngine;
using Unity.Cinemachine;

public interface IVelocityProvider
{
    Vector3 Velocity { get; }
}

[DisallowMultipleComponent]
public sealed class SpeedFOVController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera targetCamera;

    [Tooltip("Optional. If set, velocity is read from this Rigidbody.")]
    [SerializeField] private Rigidbody rigidbodySource;

    [Tooltip("Optional. If set, velocity is read from this CharacterController.")]
    [SerializeField] private CharacterController characterControllerSource;

    [Tooltip("Optional. Any component implementing IVelocityProvider (Velocity property).")]
    [SerializeField] private MonoBehaviour customVelocityProvider;

    [Header("FOV Settings")]
    [SerializeField] private bool useHorizontalSpeed = true;

    [Tooltip("If 0, we'll grab the camera's current FOV on Awake.")]
    [SerializeField] private float baseFov = 0f;

    [Min(0f)]
    [SerializeField] private float maxExtraFov = 18f;

    [Min(0.01f)]
    [SerializeField] private float speedForMaxFov = 20f;

    [Tooltip("Minimum speed required before FOV scaling starts.")]
    [Min(0f)]
    [SerializeField] private float minSpeedToEnable = 3f;

    [Tooltip("Extra buffer to prevent flicker near the threshold. Set 0 to disable.")]
    [Min(0f)]
    [SerializeField] private float hysteresis = 0.5f;

    [Tooltip("Maps normalized speed (0..1) to FOV intensity (0..1).")]
    [SerializeField]
    private AnimationCurve speedToFovCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 0.65f),
            new Keyframe(0.5f, 0.88f),
            new Keyframe(1f, 1f)
        );

    [Header("Smoothing")]
    [Tooltip("How quickly FOV follows the target. Lower = snappier, higher = smoother.")]
    [Min(0f)]
    [SerializeField] private float smoothTime = 0.12f;

    private float _currentFov;
    private float _fovVelocity;
    private bool _isActive;

    private void Reset()
    {
        targetCamera = FindFirstObjectByType<CinemachineCamera>();
        rigidbodySource = FindFirstObjectByType<Rigidbody>();
        characterControllerSource = GetComponentInParent<CharacterController>();
    }

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = FindFirstObjectByType<CinemachineCamera>();

        if (targetCamera == null)
        {
            enabled = false;
            Debug.LogError($"{nameof(SpeedFOVController)}: No Camera found/assigned.", this);
            return;
        }

        if (baseFov <= 0f)
            baseFov = targetCamera.Lens.FieldOfView;

        _currentFov = targetCamera.Lens.FieldOfView;
    }

    private void Start()
    {
        if (PlayerPushPull.Instance != null)
        {
            rigidbodySource = PlayerPushPull.Instance.GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        Vector3 v = GetVelocity();
        if (useHorizontalSpeed)
            v.y = 0f;

        float speed = v.magnitude;

        // Activation with hysteresis to avoid flicker
        float enableAt = minSpeedToEnable;
        float disableAt = Mathf.Max(0f, minSpeedToEnable - hysteresis);

        if (!_isActive && speed >= enableAt) _isActive = true;
        else if (_isActive && speed <= disableAt) _isActive = false;

        float targetFov = baseFov;

        if (_isActive)
        {
            float t = Mathf.Clamp01(speed / speedForMaxFov);
            float curve01 = Mathf.Clamp01(speedToFovCurve.Evaluate(t));
            targetFov = baseFov + (maxExtraFov * curve01);
        }

        _currentFov = smoothTime <= 0f
            ? targetFov
            : Mathf.SmoothDamp(_currentFov, targetFov, ref _fovVelocity, smoothTime);

        targetCamera.Lens.FieldOfView = _currentFov;
        Debug.Log(_currentFov);
    }

    private Vector3 GetVelocity()
    {
        if (rigidbodySource != null)
        {
            return rigidbodySource.linearVelocity;
        }

        if (characterControllerSource != null)
            return characterControllerSource.velocity;

        if (customVelocityProvider is IVelocityProvider provider)
            return provider.Velocity;

        return Vector3.zero;
    }
}
