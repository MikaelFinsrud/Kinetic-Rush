using System;
using UnityEngine;

[CreateAssetMenu(menuName = "SOs/Pickups/Effects/Reach Checkpoint")]
public class CheckpointEffectSO : PickupEffectSO
{
    public static event Action OnCheckpointReached;
    public override bool CanApply(in PickupContext ctx)
    {
        return ctx.Picker.TryGetComponent(out PlayerPickupReceiver _);
    }

    public override void Apply(in PickupContext ctx)
    {
        OnCheckpointReached.Invoke();
    }
}
