using UnityEngine;

public readonly struct CoinHitContext
{
    public readonly GameObject Shooter;        // player
    public readonly GameObject Coin;           // coin projectile
    public readonly GameObject Target;         // target hit by the coin
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitNormal;
    public readonly float ImpactSpeed;
    public readonly RaycastHit? RayHit;        // optional

    public CoinHitContext(GameObject shooter, GameObject coin, GameObject target, Vector3 hitPoint, Vector3 hitNormal, float impactSpeed, RaycastHit? rayHit = null)
    {
        Shooter = shooter;
        Coin = coin;
        Target = target;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        ImpactSpeed = impactSpeed;
        RayHit = rayHit;
    }
}
