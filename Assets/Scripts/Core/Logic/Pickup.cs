using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Pickup : MonoBehaviour
{
    [SerializeField] private PickupDefinitionSO definition;

    private Collider _col;
    private bool _consumed;

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed || definition == null) return;

        // Find “picker root” (player)
        var picker = other.gameObject;
        var context = new PickupContext(picker, transform);

        // Optional gate: must implement IPickupReceiver
        if (picker.TryGetComponent(out IPickupReceiver receiver))
        {
            if (!receiver.CanReceive(in context)) return;
        }

        // All effects must be applicable (or you can change to “any effect” logic)
        for (int i = 0; i < definition.effects.Count; i++)
        {
            if (definition.effects[i] && !definition.effects[i].CanApply(in context)) { return; }
        }

        // Apply
        for (int i = 0; i < definition.effects.Count; i++) 
        {
            if (definition.effects[i])
            {
                definition.effects[i].Apply(in context);
            }
        }

        OnConsumed();
    }

    private void OnConsumed()
    {
        _consumed = true;

        if (definition.vfxPrefab) Instantiate(definition.vfxPrefab, transform.position, Quaternion.identity);
        if (definition.sfx) AudioSource.PlayClipAtPoint(definition.sfx, transform.position);

        if (definition.consumeOnSuccess)
        {
            // Disable instead of Destroy => instant restart friendly :contentReference[oaicite:5]{index=5}
            gameObject.SetActive(false);
        }
    }

    // For your instant restart system: call this from your global reset
    public void ResetPickup()
    {
        _consumed = false;
        gameObject.SetActive(true);
    }
}
