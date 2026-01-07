using UnityEngine;

public sealed class CoinShooter : MonoBehaviour, IResettable
{
    [Header("Refs")]
    private Camera _cam;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private Coin _coinPrefab;
    [SerializeField] private Collider[] _playerColliders;
    [SerializeField] private Rigidbody _playerRigidbody;

    [Header("Shoot Input")]
    [SerializeField] private KeyCode shootKey = KeyCode.E;

    [Header("Tuning")]
    [SerializeField] private float _shootSpeed = 35f;
    [SerializeField] private float _shootCooldown = 0.15f;
    [SerializeField] private float _coinSpinRadPerSec = 20f;
    [SerializeField] private float yInherit = 0.5f;

    private float _nextShotTime;
    private bool _shotQueued = false;
    private Coin _currentCoin;

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        if (Input.GetKeyDown(shootKey))
        {
            _shotQueued = true;
        }
    }

    private void FixedUpdate()
    {
        if (_shotQueued)
        {
            Shoot();
            _shotQueued = false;
        }
    }

    // Call this from your input layer (new Input System action, etc.)
    public void Shoot()
    {
        if (Time.time < _nextShotTime) return;

        if (_currentCoin != null)
        {
            Destroy(_currentCoin.gameObject); // Prevent more than one coin at a time
        }

        _nextShotTime = Time.time + _shootCooldown;

        var coin = Instantiate(_coinPrefab, _muzzle.position, Quaternion.identity);
        coin.transform.forward = _muzzle.forward;
        _currentCoin = coin;

        Vector3 inherited = Vector3.zero;
        if (_playerRigidbody != null) 
        { 
            inherited = _playerRigidbody.GetPointVelocity(_muzzle.position); 
            inherited = new Vector3(inherited.x, inherited.y * yInherit, inherited.z);
        }

        coin.Shooter = gameObject;
        coin.Launch(inherited + (_muzzle.transform.forward * _shootSpeed), _coinSpinRadPerSec, _playerRigidbody);
    }

    public void ReduceCooldown(float amount)
    {
        _nextShotTime -= amount;
    }

    public void CaptureInitialState()
    {
    }

    public void RestoreInitialState()
    {
        _nextShotTime = 0f;
        _shotQueued = false;
        _currentCoin = null;
    }
}
