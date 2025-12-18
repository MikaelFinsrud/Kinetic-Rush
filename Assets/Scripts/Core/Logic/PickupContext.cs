using UnityEngine;

public readonly struct PickupContext
{
    public readonly GameObject Picker;
    public readonly Transform PickupTransform;

    public PickupContext(GameObject picker, Transform pickupTransform)
    {
        Picker = picker;
        PickupTransform = pickupTransform;
    }
}
