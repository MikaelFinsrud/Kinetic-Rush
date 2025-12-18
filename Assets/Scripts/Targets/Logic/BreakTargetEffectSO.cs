using UnityEngine;

[CreateAssetMenu(menuName = "SOs/Coin Targets/Effects/Break Target")]
public class BreakTargetEffectSO : CoinHitEffectSO
{
    public override bool CanApply(in CoinHitContext ctx)
    {
        return ctx.Target.TryGetComponent(out IBreakable _);
    }

    public override void Apply(in CoinHitContext ctx)
    {
        if (ctx.Target.TryGetComponent(out IBreakable b))
        {
            b.Break(ctx.HitPoint, ctx.HitNormal);
        }
    }
}
