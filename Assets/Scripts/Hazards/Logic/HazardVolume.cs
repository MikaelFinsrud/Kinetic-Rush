using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class HazardVolume : MonoBehaviour
{
    [SerializeField] private DeathCause _cause = DeathCause.HazardVolume;

    private void Reset()
    {
        // Make it a trigger by default
        var col = GetComponentInParent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<IDeathReceiver>(out var death) || death.IsDead)
        {
            return;
        }

        Vector3 point = other.ClosestPoint(transform.position);
        Vector3 normal = (other.transform.position - transform.position).normalized;

        var info = new KillInfo(_cause, point, normal, this);
        death.Kill(in info);
    }
}
