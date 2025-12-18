public interface IPickupReceiver
{
    bool CanReceive(in PickupContext ctx);
}