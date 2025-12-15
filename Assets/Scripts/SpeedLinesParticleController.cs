using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpeedLinesParticleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParticleSystem speedLines;

    [Tooltip("Optional. If set, velocity is read from this Rigidbody.")]
    [SerializeField] private Rigidbody rigidbodySource;

    [Tooltip("Optional. If set, velocity is read from this CharacterController.")]
    [SerializeField] private CharacterController characterControllerSource;

    [Tooltip("Optional. Any component implementing IVelocityProvider (Velocity property).")]
    [SerializeField] private MonoBehaviour customVelocityProvider;

    [Header("Speed Sampling")]
    [SerializeField] private bool useHorizontalSpeed = true;

    [Tooltip("Minimum speed required before emission starts.")]
    [Min(0f)]
    [SerializeField] private float minSpeedToEnable = 8f;

    [Tooltip("Extra buffer to prevent flicker near the threshold. Set 0 to disable.")]
    [Min(0f)]
    [SerializeField] private float hysteresis = 1.5f;

    [Tooltip("Speed at which scaling reaches 100% (normalized=1).")]
    [Min(0.01f)]
    [SerializeField] private float speedForMax = 25f;

    [Header("Emission Scaling")]
    [Tooltip("Emission rate at normalized speed 0 (when active). Usually 0.")]
    [Min(0f)]
    [SerializeField] private float minRateOverTime = 0f;

    [Tooltip("Emission rate at normalized speed 1.")]
    [Min(0f)]
    [SerializeField] private float maxRateOverTime = 80f;

    [Tooltip("Maps normalized speed (0..1) to emission intensity (0..1).")]
    [SerializeField]
    private AnimationCurve speedToEmissionCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 0.6f),
            new Keyframe(1f, 1f)
        );

    [Header("Particle Speed Scaling")]
    [Tooltip("Particle start speed at normalized speed 0 (when active).")]
    [Min(0f)]
    [SerializeField] private float minParticleStartSpeed = 2f;

    [Tooltip("Particle start speed at normalized speed 1.")]
    [Min(0f)]
    [SerializeField] private float maxParticleStartSpeed = 12f;

    [SerializeField] private float minSpeedRadius = 20f;
    [SerializeField] private float maxSpeedRadius = 17f;

    [Tooltip("Maps normalized speed (0..1) to particle speed intensity (0..1).")]
    [SerializeField]
    private AnimationCurve speedToParticleSpeedCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 0.7f),
            new Keyframe(1f, 1f)
        );

    [Header("Smoothing")]
    [Tooltip("How quickly values follow the target. Higher = snappier.")]
    [Min(0f)]
    [SerializeField] private float followSharpness = 12f;

    [Tooltip("Also apply scaling to all child particle systems.")]
    [SerializeField] private bool withChildren = true;

    private bool _isActive;
    private float _currentRate;
    private float _currentStartSpeed;
    private float _currentRadius;

    private void Reset()
    {
        speedLines = GetComponentInChildren<ParticleSystem>();
        rigidbodySource = GetComponentInParent<Rigidbody>();
        characterControllerSource = GetComponentInParent<CharacterController>();
    }

    private void Awake()
    {
        if (speedLines == null)
        {
            speedLines = GetComponentInChildren<ParticleSystem>();
        }

        if (speedLines == null)
        {
            enabled = false;
            Debug.LogError($"{nameof(SpeedLinesParticleController)}: No ParticleSystem assigned/found.", this);
            return;
        }

        // Start from "off"
        _currentRate = 0f;
        _currentStartSpeed = minParticleStartSpeed;
        _currentRadius = minSpeedRadius;

        ApplyToParticleSystem(_currentRate, _currentStartSpeed, _currentRadius);

        speedLines.Play(withChildren);

        // Make sure it isn't blasting in edit/awake
        SetEmitting(false);
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
        float speed = GetSpeed();

        // Activation with hysteresis
        float enableAt = minSpeedToEnable;
        float disableAt = Mathf.Max(0f, minSpeedToEnable - hysteresis);

        if (!_isActive && speed >= enableAt)
        {
            _isActive = true;
            SetEmitting(true);
        }
        else if (_isActive && speed <= disableAt)
        {
            _isActive = false;
            SetEmitting(false); // lets current particles die naturally
        }

        float targetRate = 0f;
        float targetStartSpeed = minParticleStartSpeed;
        float targetRadius = minSpeedRadius;

        if (_isActive)
        {
            float t = Mathf.Clamp01(speed / speedForMax);

            float e01 = Mathf.Clamp01(speedToEmissionCurve.Evaluate(t));
            targetRate = Mathf.Lerp(minRateOverTime, maxRateOverTime, e01);

            float s01 = Mathf.Clamp01(speedToParticleSpeedCurve.Evaluate(t));
            targetStartSpeed = Mathf.Lerp(minParticleStartSpeed, maxParticleStartSpeed, s01);

            targetRadius = Mathf.Lerp(minSpeedRadius, maxSpeedRadius, s01);
        }

        // Exponential smoothing (stable, framerate independent)
        float a = followSharpness <= 0f ? 1f : (1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        _currentRate = Mathf.Lerp(_currentRate, targetRate, a);
        _currentStartSpeed = Mathf.Lerp(_currentStartSpeed, targetStartSpeed, a);
        _currentRadius = Mathf.Lerp(_currentRadius, targetRadius, a);

        ApplyToParticleSystem(_currentRate, _currentStartSpeed, _currentRadius);
    }

    private float GetSpeed()
    {
        Vector3 v = GetVelocity();
        if (useHorizontalSpeed) v.y = 0f;
        return v.magnitude;
    }

    private Vector3 GetVelocity()
    {
        if (rigidbodySource != null)
        {
#if UNITY_6000_0_OR_NEWER
            return rigidbodySource.linearVelocity;
#else
            return rigidbodySource.velocity;
#endif
        }

        if (characterControllerSource != null)
            return characterControllerSource.velocity;

        if (customVelocityProvider is IVelocityProvider provider)
            return provider.Velocity;

        return Vector3.zero;
    }

    private void SetEmitting(bool emitting)
    {
        var emission = speedLines.emission;
        emission.enabled = emitting;   // stops spawning immediately, no restart delay
    }

    private void ApplyToParticleSystem(float rateOverTime, float startSpeed, float radius)
    {
        // Emission rate
        var emission = speedLines.emission;
        emission.rateOverTime = rateOverTime;

        // Particle speed
        var main = speedLines.main;
        main.startSpeed = startSpeed;

        var shape = speedLines.shape;
        shape.radius = radius;
    }
}
