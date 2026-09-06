using UnityEngine;
using Game.Player;
using System;
using Players;
using Players.SkillModule;
using Stat;
using UnityEditor;
using UnityEngine.EventSystems;

/// <summary> 스킬이 선택지에 등장할 조건. </summary>
public enum SkillAvailability
{
    Always,                     // 항상 등장
    RequireAttackEffectMissing, // 캐릭터·장비(Base+Equip)에 conditionEffectType이 없을 때만 등장 (속성 부여 스킬용)
    RequireAttackEffectPresent, // 최종 공격 속성(Base+Equip+Buff)에 conditionEffectType이 있을 때만 등장 (속성 강화 스킬용)
}

[CreateAssetMenu(fileName = "PlayerSkillScriptable", menuName = "PlayerSkill")]
public class SkillDefinition : ScriptableObject
{
    [RegistryKey("skillIds")] public string id;
    public int maxLevel;
    public Sprite icon;
    public MonoScript moduleScript;
    private PlayerSkillModuleBase skillModule;

    [Header("스킬 설명")]
    [TextArea(2, 4)] public string description;

    [Header("등장 조건")]
    public SkillAvailability availability = SkillAvailability.Always;
    public EffectType conditionEffectType = EffectType.Normal;

#if UNITY_EDITOR
    [ContextMenu("에셋 이름을 Id와 동기화")]
    private void SyncAssetNameToId()
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning($"{name}: id가 비어있어 이름을 변경할 수 없습니다.", this);
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(this);
        if (string.IsNullOrEmpty(assetPath)) return;

        string currentName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        if (currentName == id) return;

        string error = AssetDatabase.RenameAsset(assetPath, id);
        if (!string.IsNullOrEmpty(error))
            Debug.LogWarning($"{name}: could not rename skill asset to '{id}'. {error}", this);
    }
#endif

    /// <summary>
    /// 이 스킬이 선택지에 오를 수 있는지 판단한다.
    /// 부여 스킬은 타고난 속성(Base+Equip) 기준, 강화 스킬은 최종 속성(Base+Equip+Buff) 기준으로 판정한다.
    /// </summary>
    public bool IsAvailable(BaseStat stat)
    {
        if (availability == SkillAvailability.Always) return true;
        if (stat == null) return true;

        // 부여 스킬: 스킬로 얻은 버프 속성은 제외하고, 캐릭터·장비가 원래 갖고 있는지만 본다.
        if (availability == SkillAvailability.RequireAttackEffectMissing)
            return !Utils.HasEffectType(stat.InnateAttackEffectType, conditionEffectType);

        // 강화 스킬: 부여 스킬로 얻은 버프 속성까지 포함해 실제로 그 속성으로 공격 중일 때만 등장한다.
        return Utils.HasEffectType(stat.AttackEffectType, conditionEffectType);
    }

    public void InstallModule(GameObject owner, PlayerSkill skill)
    {
        var t = moduleScript.GetClass();
        if (t == null || !typeof(MonoBehaviour).IsAssignableFrom(t))
        {
            Debug.LogError($"{id.ToString()}: script type is invalid!");
            return;
        }

        var comp = owner.GetComponent(t) ?? owner.AddComponent(t);

        if (comp is PlayerSkillModuleBase mod)
        {
            mod.Init(skill);
            skillModule = mod;
        }
    }

    public PlayerSkillModuleBase GetModule() => skillModule;
}
