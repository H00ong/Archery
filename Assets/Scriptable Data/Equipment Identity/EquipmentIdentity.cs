using System.Collections.Generic;
using Stat;
using UnityEngine;

namespace Players
{
    [CreateAssetMenu(fileName = "EquipmentIdentity", menuName = "Player/Equipment Identity", order = 2)]
    public class EquipmentIdentity : ScriptableObject
    {
        [Header("장비 식별")]
        public string equipmentName;
        public EquipmentType equipmentType;
        public Sprite equipmentIcon;

        [Header("기본 능력치 (Lv1)")]
        public EquipmentBaseStatData baseStat;

        [Header("레벨 성장")]
        public int maxLevel = 10;
        public EquipmentLevelStatGrowth levelStatGrowth;
        public int[] levelUpCosts = { 100, 200, 350, 550, 800, 1100, 1500, 2000, 2800 };

        [Header("이펙트 데이터 (Lv1 기준)")]
        public EquipmentEffectConfig[] effectConfigs;

        [Header("이펙트 레벨 성장")]
        public EquipmentEffectGrowth[] effectGrowths;

        [Header("구매")]
        public int purchasePrice = 500;

        /// <summary>
        /// 지정 레벨의 스탯을 계산한다. Level N = BaseStat + Growth * (N - 1)
        /// </summary>
        public EquipmentBaseStatData GetStatsAtLevel(int level)
        {
            int lv = Mathf.Clamp(level, 1, maxLevel);
            int growth = lv - 1;

            return new EquipmentBaseStatData
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
        /// 지정 레벨에 해당하는 EffectData 맵을 반환한다.
        /// </summary>
        public Dictionary<EffectType, EffectData> GetEffectDataAtLevel(int level)
        {
            int lv = Mathf.Clamp(level, 1, maxLevel);
            int growth = lv - 1;

            var result = new Dictionary<EffectType, EffectData>();

            if (effectConfigs == null) return result;

            Dictionary<EffectType, EquipmentEffectGrowth> growthMap = null;
            if (effectGrowths != null && effectGrowths.Length > 0)
            {
                growthMap = new Dictionary<EffectType, EquipmentEffectGrowth>();
                foreach (var g in effectGrowths)
                    growthMap[g.effectType] = g;
            }

            foreach (var cfg in effectConfigs)
            {
                if (cfg.baseEffect == null) continue;

                float dur = cfg.baseEffect.duration;
                float val = cfg.baseEffect.value;
                float dot = cfg.baseEffect.dotDamage;
                float tick = cfg.baseEffect.tickInterval;

                if (growthMap != null && growthMap.TryGetValue(cfg.effectType, out var g))
                {
                    dur += g.durationGrowth * growth;
                    val += g.valueGrowth * growth;
                    dot += g.dotDamageGrowth * growth;
                    tick += g.tickIntervalGrowth * growth;
                }

                result[cfg.effectType] = new EffectData(dur, val, dot, tick);
            }

            return result;
        }

        public int GetLevelUpCost(int currentLevel)
        {
            int idx = currentLevel - 1;
            if (idx < 0 || idx >= levelUpCosts.Length || currentLevel >= maxLevel)
                return -1;
            return levelUpCosts[idx];
        }
    }
}
