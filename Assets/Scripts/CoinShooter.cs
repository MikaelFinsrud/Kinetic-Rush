using UnityEngine;

public sealed class CoinShooter : MonoBehaviour
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
        if (_currentCoin != null)
        {
            Destroy(_currentCoin.gameObject); // Prevent more than one coin at a time
        }

        if (Time.time < _nextShotTime) return;
        _nextShotTime = Time.time + _shootCooldown;

        var coin = Instantiate(_coinPrefab, _muzzle.position, Quaternion.identity);
        _currentCoin = coin;

        Vector3 inherited = Vector3.zero;
        if (_playerRigidbody != null) 
        { 
            inherited = _playerRigidbody.GetPointVelocity(_muzzle.position); 
        }

        coin.Launch(inherited + (_muzzle.transform.forward * _shootSpeed), _playerRigidbody);
    }
}
