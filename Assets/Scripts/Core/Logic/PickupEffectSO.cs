using UnityEngine;

public abstract class PickupEffectSO : ScriptableObject
{
    public abstract bool CanApply(in PickupContext ctx);
    public abstract void Apply(in PickupContext ctx);
}
