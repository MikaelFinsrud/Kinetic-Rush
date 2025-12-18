using UnityEngine;

public class OneShotBreak : MonoBehaviour, IBreakable, ICoinHitReceiver, IResettable
{
    public void Break(Vector3 hitPoint, Vector3 hitNormal)
    {
        gameObject.SetActive(false);
    }

    public bool CanBeHit(CoinTargetDefinitionSO definition, in CoinHitContext ctx)
    {
        return true;
    }

    public void CaptureInitialState()
    {

    }

    public void OnHit(CoinTargetDefinitionSO definition, in CoinHitContext ctx)
    {

    }

    public void RestoreInitialState()
    {
        gameObject.SetActive(true);
    }
}
