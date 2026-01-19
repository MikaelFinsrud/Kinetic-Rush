using UnityEngine;

[CreateAssetMenu(menuName = "SOs/Levels/Level Definition")]
public class LevelDefinitionSO : ScriptableObject 
{
    public string levelId;
    public float targetTimeSeconds;
}
