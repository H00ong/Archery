using System.Collections.Generic;
using Map;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "MapData", menuName = "MapData")]
public abstract class MapScriptable : ScriptableObject
{
    public MapType mapType;
    public List<AssetReferenceGameObject> mapList;
    public AssetReferenceGameObject bossMap;
    
    [Header("Enemy (EnemyIdentity 기반)")]
    public List<EnemyIdentity> enemyIdentityList;
    public List<EnemyIdentity> bossIdentityList;
}
