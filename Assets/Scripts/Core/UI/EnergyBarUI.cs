using System;
using UnityEngine;
using UnityEngine.UI;

public class EnergyBarUI : MonoBehaviour
{
    [SerializeField] private Image _energyBar;

    private void Start()
    {
        if (PlayerEnergy.Instance != null)
        {
            PlayerEnergy.Instance.OnEnergyChanged += HandleEnergyChanged;
        }
    }

    private void HandleEnergyChanged(float arg1, float arg2) //arg1: current, arg2: max
    {
        _energyBar.fillAmount = arg1 / arg2;
    }
}
