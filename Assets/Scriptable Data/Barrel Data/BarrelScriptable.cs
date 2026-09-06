using Game.Player;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Attack: 습득 시 무작위 적에게 메테오 공격 (Ice/Lightning/Fire 등, meteorPrefab 사용)
/// Shield: 습득 시 플레이어에게 보호막 부여 (shieldAmount 사용)
/// Heal:   습득 시 플레이어 체력 회복 (healAmount 사용)
/// </summary>
public enum BarrelKind
{
    Attack,
    Shield,
    Heal,
}

[CreateAssetMenu(fileName = "BarrelScriptable", menuName = "BarrelData")]
public class BarrelScriptable : ScriptableObject
{
    public EffectType type;
    public BarrelKind kind = BarrelKind.Attack;
    public AssetReferenceGameObject barrelPrefab;

    [Header("Attack 전용 (kind == Attack)")]
    public AssetReferenceGameObject meteorPrefab;

    [Header("Shield/Heal 전용 (kind == Shield / Heal)")]
    public int shieldAmount = 50;
    public int healAmount = 30;
}
