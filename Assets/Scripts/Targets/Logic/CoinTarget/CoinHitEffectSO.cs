using UnityEngine;

public abstract class CoinHitEffectSO : ScriptableObject
{
    public abstract bool CanApply(in CoinHitContext ctx);
    public abstract void Apply(in CoinHitContext ctx);
}
