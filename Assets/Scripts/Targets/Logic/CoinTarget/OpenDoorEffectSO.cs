using UnityEngine;

[CreateAssetMenu(menuName = "SOs/Coin Targets/Effects/Open Door")]
public class OpenDoorEffectSO : CoinHitEffectSO
{
    [SerializeField] private GameObject doorObject;

    public override bool CanApply(in CoinHitContext ctx)
    {
        return doorObject != null && doorObject.TryGetComponent(out IDoor _);
    }

    public override void Apply(in CoinHitContext ctx)
    {
        if (doorObject.TryGetComponent(out IDoor door))
            door.Open();
    }
}
