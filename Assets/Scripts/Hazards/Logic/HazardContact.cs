using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class ContactHazard : MonoBehaviour
{
    [SerializeField] private DeathCause _cause = DeathCause.Spikes;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.TryGetComponent<IDeathReceiver>(out var death) || death.IsDead)
            return;

        var contact = collision.GetContact(0);
        var info = new KillInfo(_cause, contact.point, contact.normal, this);
        death.Kill(in info);
    }
}
