using System;
using UnityEngine;

public interface IEnergy
{
    float Current { get; }
    float Max { get; }
    bool CanRegen { get; }

    event Action<float, float> OnEnergyChanged; // current, max

    bool TrySpendInstant(float amount);
    float SpendContinuous(float costPerSecond, float deltaTime); // returns [0..1] fraction paid

    void RestoreFull();
    void Add(float amount);
    void BlockRegen(float durationSeconds);
}

public sealed class PlayerEnergy : MonoBehaviour, IEnergy
{
    public static PlayerEnergy Instance { get; private set; }

    [SerializeField] private EnergySettingsSO _settings;
    [SerializeField] private float _startEnergy = 100f;

    public float Current => _current;
    public float Max => _settings != null ? _settings.maxEnergy : 0f;
    public bool CanRegen => Time.time >= _regenBlockedUntil;

    public event Action<float, float> OnEnergyChanged;

    private float _current;
    private float _regenResumeAt;
    private float _regenBlockedUntil;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("PlayerEnergy already exists!.");
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        if (_settings == null)
        {
            Debug.LogError($"{nameof(PlayerEnergy)} missing EnergySettings.", this);
            enabled = false;
            return;
        }

        _current = Mathf.Clamp(_startEnergy, 0f, _settings.maxEnergy);
        RaiseChanged();
    }

    private void Update()
    {
        if (!CanRegen) return;
        if (Time.time < _regenResumeAt) return;

        float before = _current;
        _current = Mathf.MoveTowards(_current, _settings.maxEnergy, _settings.regenPerSecond * Time.deltaTime);

        if (!Mathf.Approximately(before, _current))
            RaiseChanged();
    }

    public bool TrySpendInstant(float amount)
    {
        if (amount <= 0f) return true;
        if (_current < amount) return false;

        _current -= amount;
        AfterSpend();
        RaiseChanged();
        return true;
    }

    /// <summary>
    /// Tries to spend costPerSecond * deltaTime. Returns fraction [0..1] that was actually paid.
    /// You can multiply your continuous force by this fraction for smooth “energy starvation”.
    /// </summary>
    public float SpendContinuous(float costPerSecond, float deltaTime)
    {
        if (costPerSecond <= 0f || deltaTime <= 0f) return 1f;

        float required = costPerSecond * deltaTime;
        if (required <= 0f) return 1f;

        float paid = Mathf.Min(_current, required);
        _current -= paid;

        if (paid > 0f)
        {
            AfterSpend();
            RaiseChanged();
        }

        return required <= 0f ? 1f : (paid / required);
    }

    public void RestoreFull()
    {
        _current = _settings.maxEnergy;
        RaiseChanged();
    }

    public void Add(float amount)
    {
        if (amount <= 0f) return;
        float before = _current;
        _current = Mathf.Min(_settings.maxEnergy, _current + amount);
        if (!Mathf.Approximately(before, _current))
            RaiseChanged();
    }

    public void BlockRegen(float durationSeconds)
    {
        if (durationSeconds <= 0f) return;
        _regenBlockedUntil = Mathf.Max(_regenBlockedUntil, Time.time + durationSeconds);
    }

    private void AfterSpend()
    {
        _regenResumeAt = Time.time + _settings.regenDelayAfterSpend;
    }

    private void RaiseChanged()
    {
        OnEnergyChanged?.Invoke(_current, _settings.maxEnergy);
    }
}
