public interface IPickupReceiver
{
    bool CanReceive(in PickupContext ctx);
    void OnReceived(PickupDefinitionSO definition, in PickupContext ctx);
}