using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SOs/Pickups/Pickup Definition")]
public class PickupDefinitionSO : ScriptableObject
{
    public string displayName;
    public AudioClip sfx;
    public float sfxVolume = 1f;
    public GameObject vfxPrefab;

    public List<PickupEffectSO> effects = new();
    public bool consumeOnSuccess = true;
}
