using System;
using System.Collections;
using UnityEngine;

public class PushPullTargetVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PushPullTarget pushPullTarget;

    [Header("Emission Settings")]
    [SerializeField] private UnityEngine.Color baseEmissionColorHighlighted;
    [SerializeField] private UnityEngine.Color baseEmissionColorPushed;
    [SerializeField] private UnityEngine.Color baseEmissionColorPulled;

    [SerializeField] private float defaultIntensity = 0f;             // starting intensity
    [SerializeField] private float highlightedIntensity = 2f;         // intensity when highlighted
    [SerializeField] private float pushPulledIntensity = 1.4f;         // intensity when pushed and pulled


    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private Coroutine _currentRoutine;

    static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private float _currentIntensity;
    private bool _isHighlighted;

    private void Awake()
    {
        _renderer = GetComponentInParent<Renderer>();
        _mpb = new MaterialPropertyBlock();

        _currentIntensity = defaultIntensity;
        ApplyEmission(_currentIntensity, baseEmissionColorHighlighted);
    }

    private void Start()
    {
        if (pushPullTarget == null)
        {
            Debug.LogError("PushPullTargetVisuals: PushPullTarget reference is missing.");
            return;
        }
        pushPullTarget.OnHighlighted += HandleHighlighted;
        pushPullTarget.OnUnHighlighted += HandleUnHighlighted;
        pushPullTarget.OnPushed += HandlePushed;
        pushPullTarget.OnPulled += HandlePulled;
    }

    private void HandlePulled()
    {
        LerpEmissionTo(pushPulledIntensity, 0.2f, baseEmissionColorPulled);
    }

    private void HandlePushed()
    {
        LerpEmissionTo(pushPulledIntensity, 0.2f, baseEmissionColorPushed);
    }

    private void HandleUnHighlighted()
    {
        _isHighlighted = false;
        LerpEmissionTo(0f, 0.2f, baseEmissionColorHighlighted);
    }

    private void HandleHighlighted()
    {
        _isHighlighted = true;
        LerpEmissionTo(highlightedIntensity, 0.2f, baseEmissionColorHighlighted);
    }

    /// <summary>
    /// Public API: smoothly go to a new emission intensity over time.
    /// </summary>
    public void LerpEmissionTo(float targetIntensity, float duration, UnityEngine.Color color)
    {
        if (_currentRoutine != null)
            StopCoroutine(_currentRoutine);

        _currentRoutine = StartCoroutine(LerpEmissionRoutine(targetIntensity, duration, color));
    }

    IEnumerator LerpEmissionRoutine(float targetIntensity, float duration, UnityEngine.Color color)
    {
        float startIntensity = _currentIntensity;
        float time = 0f;

        // Handle 0 duration edge case
        if (duration <= 0f)
        {
            _currentIntensity = targetIntensity;
            ApplyEmission(_currentIntensity, color);
            yield break;
        }

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            _currentIntensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            ApplyEmission(_currentIntensity, color);

            yield return null;
        }

        if (_isHighlighted)
        {
            _currentIntensity = highlightedIntensity;
            ApplyEmission(_currentIntensity, baseEmissionColorHighlighted);
        }
        else
        {
            _currentIntensity = 0f;
            ApplyEmission(_currentIntensity, baseEmissionColorHighlighted);
        }

        _currentRoutine = null;
    }

    void ApplyEmission(float intensity, UnityEngine.Color color)
    {
        _renderer.GetPropertyBlock(_mpb);

        // Multiply base color by intensity (HDR-friendly if you go above 1)
        UnityEngine.Color emissive = color * intensity;

        _mpb.SetColor(EmissionColorID, emissive);
        _renderer.SetPropertyBlock(_mpb);
    }
}


