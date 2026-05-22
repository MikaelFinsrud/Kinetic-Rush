using System.Linq;
using UnityEngine;

public class RestartLevelManager : MonoBehaviour
{
    public static RestartLevelManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("RestartLevelManager already exists!.");
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }
    }

    public void RestartLevel()
    {
        MonoBehaviour[] allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

        IResettable[] resettables = allMonoBehaviours.OfType<IResettable>().ToArray();

        foreach (IResettable resettable in resettables)
        {
            resettable.RestoreInitialState();
        }
    }
}
