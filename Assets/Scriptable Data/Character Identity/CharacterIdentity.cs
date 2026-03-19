using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Players
{
    [CreateAssetMenu(fileName = "CharacterIdentity", menuName = "Player/Character Identity", order = 1)]
    public class CharacterIdentity : ScriptableObject
    {
        [Header("캐릭터 식별")]
        [RegistryKey("characterNames")] public string characterName;

        [Header("Addressable 프리팹")]
        public AssetReferenceGameObject characterPrefab;
        public AssetReferenceGameObject projectilePrefab;

        [Header("기본 능력치")]
        public CharacterBaseStatData baseStat;
    }
}
