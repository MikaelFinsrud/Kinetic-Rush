using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [SerializeField] private Coin coin;
    [SerializeField] private PushPullTarget _pushPullTarget;

    private bool isPulling = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        coin.OnImpact += HandleImpact;
        coin.OnTrigger += HandleTrigger;
        _pushPullTarget.OnPulled += HandlePulled;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void HandleImpact() 
    {
        isPulling = false;
    }

    private void HandlePulled()
    {
        isPulling = true;
    }

    private void HandleTrigger(Collider c)
    {
        CoinShooter coinShooter = c.GetComponent<CoinShooter>();

        if (coinShooter != null && isPulling)
        {
            
        }
    }
}
