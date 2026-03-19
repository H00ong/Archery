using Enemy;
using UnityEngine;

[CreateAssetMenu(fileName = "BaseModule", menuName = "Enemy/ModuleData/BaseModule")]
public class BaseModuleData : ScriptableObject
{
    [Header("Identification")]
    [RegistryKey("enemyNames")] public string targetName;    
    public EnemyTag targetTag;
}
