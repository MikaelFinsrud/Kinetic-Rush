using UnityEngine;

public class CoinTarget : MonoBehaviour, IResettable
{
    [SerializeField] private CoinTargetDefinitionSO definition;

    private bool _used;

    private GameObject currentVFX;

    public bool TryHit(in CoinHitContext ctx)
    {
        if (_used || definition == null) return false;

        // Gatekeeper
        if (TryGetComponent(out ICoinHitReceiver receiver) && !receiver.CanBeHit(definition, in ctx)) { return false; }

        // Check applicability
        if (definition.requireAllEffectsApplicable)
        {
            for (int i = 0; i < definition.effects.Count; i++)
            {
                if (definition.effects[i] && !definition.effects[i].CanApply(in ctx))
                {
                    return false;
                }
            }
        }

        // Apply
        for (int i = 0; i < definition.effects.Count; i++)
        {
            if (definition.effects[i] && definition.effects[i].CanApply(in ctx))
            {
                definition.effects[i].Apply(in ctx);
            }
        }

        // Feedback (optional)
        if (definition.vfxPrefab) { currentVFX = Instantiate(definition.vfxPrefab, ctx.HitPoint, Quaternion.LookRotation(ctx.HitNormal)); }
        if (definition.sfx) { AudioSource.PlayClipAtPoint(definition.sfx, ctx.HitPoint, definition.sfxVolume); }

        // Hook
        if (receiver != null) { receiver.OnHit(definition, in ctx); }

        if (definition.disableAfterHit)
        {
            _used = true;
            gameObject.SetActive(false);
        }

        return true;
    }

    public void CaptureInitialState()
    {

    }

    public void RestoreInitialState()
    {
        _used = false;
        gameObject.SetActive(true);

        if (currentVFX != null)
        {
            Destroy(currentVFX);
        }
    }
}
