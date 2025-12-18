using UnityEngine;

public sealed class PlayerPickupReceiver : MonoBehaviour, IPickupReceiver
{
    [SerializeField] private bool canPickup = true;

    public bool CanReceive(in PickupContext ctx)
    {
        // Examples of rules you might enforce:
        // - player is alive
        // - not in a cutscene/menu
        // - inventory not full
        // - energy already full => reject energy orb
        return canPickup;
    }

    public void OnReceived(PickupDefinitionSO definition, in PickupContext ctx)
    {
        // Player-side reactions:
        // - update HUD
        // - stats/analytics
        // - haptics
        // (You can also play SFX here instead of in the Pickup.)
        // Debug.Log($"Picked up: {definition.displayName}");
    }
}
