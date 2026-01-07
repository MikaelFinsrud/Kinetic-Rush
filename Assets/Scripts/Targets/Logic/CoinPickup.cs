using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Coin coin;
    [SerializeField] private PushPullTarget _pushPullTarget;

    [Header("Tuning")]
    [SerializeField] private float cooldownReductionAmount = 5.75f;

    private bool isPulling = false;
    private CoinShooter currentCoinShooter = null;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        coin.OnImpact += HandleImpact;
        coin.OnTrigger += HandleTrigger;
        coin.OnExitTrigger += HandleExitTrigger;
        _pushPullTarget.OnPulled += HandlePulled;
    }

    private void HandleImpact(Collider c) 
    {
        if (!c.GetComponent<CoinShooter>())
        {
            isPulling = false;
        }
    }

    private void HandlePulled()
    {
        isPulling = true;

        if (currentCoinShooter != null)
        {
            currentCoinShooter.ReduceCooldown(cooldownReductionAmount);
        }
    }

    private void HandleTrigger(Collider c)
    {
        CoinShooter coinShooter = c.GetComponentInParent<CoinShooter>();

        if (coinShooter != null)
        {
            currentCoinShooter = coinShooter;

            if (isPulling) 
            {
                currentCoinShooter.ReduceCooldown(cooldownReductionAmount);
            }
        }
    }

    private void HandleExitTrigger(Collider c)
    {
        CoinShooter coinShooter = c.GetComponentInParent<CoinShooter>();

        if (coinShooter != null)
        {
            currentCoinShooter = null;
        }
    }
}
