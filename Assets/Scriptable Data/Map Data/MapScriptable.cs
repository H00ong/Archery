using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "MapConfig", menuName = "MapData/MapConfig")]
public class MapScriptable : ScriptableObject
{
    [RegistryKey("mapIds")] public string mapId;
    public List<AssetReferenceGameObject> mapList;
    public AssetReferenceGameObject bossMap;

    [Header("Lobby")]
    public AssetReferenceGameObject lobbyMapDummy;
    
    [Header("Enemy (EnemyIdentity 기반)")]
    public List<EnemyIdentity> enemyIdentityList;
    public List<EnemyIdentity> bossIdentityList;
}
