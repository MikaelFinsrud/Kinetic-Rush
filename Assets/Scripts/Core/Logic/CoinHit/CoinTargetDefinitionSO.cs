using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SOs/Coin Targets/Coin Target Definition")]
public class CoinTargetDefinitionSO : ScriptableObject
{
    public string displayName;
    public List<CoinHitEffectSO> effects = new();
    public bool requireAllEffectsApplicable = true;

    // Optional feedback
    public AudioClip sfx;
    public float sfxVolume = 1f;
    public GameObject vfxPrefab;

    // Optional: single-use targets
    public bool disableAfterHit = false;
}
