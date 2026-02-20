using Game.Player;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "OrbData", menuName = "OrbData")]
public class OrbScriptable : ScriptableObject
{
    public EffectType effectType;
    public AssetReferenceGameObject orb;
}
