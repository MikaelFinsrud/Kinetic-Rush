using UnityEngine;

[CreateAssetMenu(menuName = "SOs/Pickup Effects/Restore Energy")]
public class RestoreEnergyEffectSO : PickupEffectSO
{
    [SerializeField] private bool restoreFull = true;
    [SerializeField] private float amount = 25f;

    public override bool CanApply(in PickupContext ctx)
    {
        return ctx.Picker.TryGetComponent(out PlayerEnergy _);
    }

    public override void Apply(in PickupContext ctx)
    {
        if (!ctx.Picker.TryGetComponent(out PlayerEnergy energy)) return;
        if (restoreFull) energy.RestoreFull();
        else energy.Add(amount);
    }
}
