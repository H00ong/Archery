using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Players
{
    [CreateAssetMenu(fileName = "CharacterIdentity", menuName = "Player/Character Identity", order = 1)]
    public class CharacterIdentity : ScriptableObject
    {
        [Header("캐릭터 식별")]
        [RegistryKey("characterNames")] public string characterName;
        public int index = 0;    // 캐릭터 선택 UI에서의 순서(index) - LobbyCharacterManager에서 사용
        public Sprite characterIcon;

        [Header("Addressable 프리팹")]
        public AssetReferenceGameObject characterPrefab;
        public AssetReferenceGameObject projectilePrefab;
        public AssetReferenceGameObject lobbyCharacterDummy;

        [Header("기본 능력치 (Lv1)")]
        public CharacterBaseStatData baseStat;

        [Header("레벨 성장")]
        public int maxLevel = 10;
        public CharacterLevelStatGrowth levelStatGrowth;
        public int[] levelUpCosts = { 100, 200, 350, 550, 800, 1100, 1500, 2000, 2800 };

        [Header("구매")]
        public int purchasePrice = 1000;

        /// <summary>
        /// 지정 레벨의 스탯을 계산한다. Level N = BaseStat + Growth * (N - 1)
        /// </summary>
        public CharacterBaseStatData GetStatsAtLevel(int level)
        {
            int lv = Mathf.Clamp(level, 1, maxLevel);
            int growth = lv - 1;

            return new CharacterBaseStatData
            {
                maxHP = baseStat.maxHP + levelStatGrowth.maxHP * growth,
                attackPower = baseStat.attackPower + levelStatGrowth.attackPower * growth,
                moveSpeed = baseStat.moveSpeed + levelStatGrowth.moveSpeed * growth,
                armor = baseStat.armor + levelStatGrowth.armor * growth,
                magicResistance = baseStat.magicResistance + levelStatGrowth.magicResistance * growth,
                attackSpeed = baseStat.attackSpeed + levelStatGrowth.attackSpeed * growth,
                projectileSpeed = baseStat.projectileSpeed + levelStatGrowth.projectileSpeed * growth,
                attackEffectType = baseStat.attackEffectType,
            };
        }

        /// <summary>
        /// 현재 레벨에서 다음 레벨로 올리는 비용. 최대 레벨이면 -1 반환.
        /// </summary>
        public int GetLevelUpCost(int currentLevel)
        {
            int idx = currentLevel - 1; // Lv1→Lv2 = index 0
            if (idx < 0 || idx >= levelUpCosts.Length || currentLevel >= maxLevel)
                return -1;
            return levelUpCosts[idx];
        }
    }
}
