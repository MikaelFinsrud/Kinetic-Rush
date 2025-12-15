using UnityEngine;

[CreateAssetMenu(menuName = "KineticRush/Energy Settings")]
public sealed class EnergySettingsSO : ScriptableObject
{
    [Header("Pool")]
    [Min(1f)] public float maxEnergy = 100f;

    [Header("Regen")]
    [Min(0f)] public float regenPerSecond = 18f;

    [Tooltip("Optional: delay after spending before regen starts. Set 0 if you don't want it.")]
    [Min(0f)] public float regenDelayAfterSpend = 0.15f;
}
