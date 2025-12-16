using UnityEngine;
using System;

[DisallowMultipleComponent]
public sealed class PlayerDeathController : MonoBehaviour, IDeathReceiver
{
    public bool IsDead { get; private set; }

    public event Action<KillInfo> Died;

    [Header("Optional refs")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private MonoBehaviour[] _disableOnDeath; // movement, input, etc.

    [Header("Restart")]
    [SerializeField] private float _restartDelaySeconds = 1f; // keep 0 for instant

    private float _restartAtTime = -1f;
    private KillInfo _lastKill;

    public void Kill(in KillInfo info)
    {
        if (IsDead) return;
        IsDead = true;

        _lastKill = info;

        // Disable gameplay scripts (movement controller, input, etc.)
        if (_disableOnDeath != null)
        {
            for (int i = 0; i < _disableOnDeath.Length; i++)
            {
                if (_disableOnDeath[i] != null)
                {
                    _disableOnDeath[i].enabled = false;

                }
            }
        }

        // Stop physics quickly (optional)
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }

        Died?.Invoke(info);

        if (_restartDelaySeconds <= 0f)
        {
            InstantRestart();
        }
        else
        {
            _restartAtTime = Time.unscaledTime + _restartDelaySeconds;
        }
    }

    private void Update()
    {
        if (!IsDead) return;
        if (_restartAtTime > 0f && Time.unscaledTime >= _restartAtTime)
            InstantRestart();
    }

    private void InstantRestart()
    {
        _restartAtTime = -1f;

        if (_disableOnDeath != null)
        {
            for (int i = 0; i < _disableOnDeath.Length; i++)
            {
                if (_disableOnDeath[i] != null)
                {
                    _disableOnDeath[i].enabled = true;

                }
            }
        }

        if (RestartLevelManager.Instance != null)
        {
            RestartLevelManager.Instance.RestartLevel();
        }
    }
}
