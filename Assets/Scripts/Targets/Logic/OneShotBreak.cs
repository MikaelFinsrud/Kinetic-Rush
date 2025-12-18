using UnityEngine;

public class OneShotBreak : MonoBehaviour, IBreakable
{
    public void Break(Vector3 hitPoint, Vector3 hitNormal)
    {
        gameObject.SetActive(false);
    }
}
