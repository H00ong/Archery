using Game.Player;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "BarrelScriptable", menuName = "BarrelData")]
public class BarrelScriptable : ScriptableObject
{
    public EffectType type;
    public AssetReferenceGameObject barrelPrefab;
    public AssetReferenceGameObject meteorPrefab;
}
