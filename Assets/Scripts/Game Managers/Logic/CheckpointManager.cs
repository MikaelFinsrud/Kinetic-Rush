using System;
using UnityEngine;
using Object = UnityEngine.Object;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }
    public static event Action OnAllCheckpointsReached;

    [SerializeField] private PickupDefinitionSO checkpointPickupDefinition;

    private int checkpointsReached = 0;
    private int totalCheckpoints = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("CheckpointManager already exists!.");
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        Pickup[] allPickups = Object.FindObjectsByType<Pickup>();

        foreach (Pickup pickup in allPickups)
        {
            if (pickup.GetSODefinition() == checkpointPickupDefinition)
            {
                totalCheckpoints++;
            }
        }

        Debug.Log("Total checkpoints: " + totalCheckpoints);
    }

    private void OnEnable()
    {
        CheckpointEffectSO.OnCheckpointReached += HandleCheckpointReached;
    }

    private void OnDisable()
    {
        CheckpointEffectSO.OnCheckpointReached -= HandleCheckpointReached;
    }

    private void HandleCheckpointReached()
    {
        checkpointsReached++;
        // Handle checkpoint logic here, e.g., save game state, update UI, etc.
        Debug.Log("Checkpoint reached! Now reached " + checkpointsReached + "checkpoints.");

        if (checkpointsReached >= totalCheckpoints)
        {
            Debug.Log("All checkpoints reached!");
            OnAllCheckpointsReached?.Invoke();
        }
    }
}
