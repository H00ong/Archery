using System.Collections.Generic;
using Map;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "MapData", menuName = "MapData")]
public abstract class MapScriptable : ScriptableObject
{
    public MapType mapType;
    public List<AssetReferenceGameObject> mapList;
    
    [Header("Enemy (EnemyIdentity 기반)")]
    public List<EnemyIdentity> enemyIdentityList;
    public List<EnemyIdentity> bossIdentityList;
    
    [Header("Legacy (기존 AssetRef 방식 - 호환용)")]
    public List<AssetReferenceGameObject> enemyList;
    public List<AssetReferenceGameObject> bossList;
    public AssetReferenceGameObject bossMap;
}
