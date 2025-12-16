using System.Linq;
using UnityEngine;

public class RestartLevelManager : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }
    }

    private void RestartLevel()
    {
        MonoBehaviour[] allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

        IResettable[] resettables = allMonoBehaviours.OfType<IResettable>().ToArray();

        foreach (IResettable resettable in resettables)
        {
            resettable.RestoreInitialState();
        }
    }
}
