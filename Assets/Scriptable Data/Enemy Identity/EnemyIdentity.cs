using System.Collections.Generic;
using Enemy;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "EnemyIdentity", menuName = "Enemy/Identity", order = 1)]
public class EnemyIdentity : ScriptableObject
{
    public AssetReferenceGameObject Prefab;
    public EnemyTag Tag;
    public Material ObjectMat;
    public Material AccessoryMat;
    
    [Header("Modules")]
    public BaseModuleData moveModule;
    public List<BaseModuleData> attackModules;
}
