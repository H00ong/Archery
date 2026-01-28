using System.Collections.Generic;
using Enemy;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "ShootData", menuName = "Enemy/ModuleData/ShootData")]
public class ShootData : AttackModuleData
{
    [Header("Assets (Addressable)")]
    public AssetReferenceGameObject projectilePrefab;

    [Header("Firing Settings")]
    public bool playerTargeting;
    public EnemyPointType firePointType;
    
    public List<Transform> GetShootingPoint(EnemyController controller)
    {
        return controller.enemyReference != null ? controller.enemyReference.GetPoints(EnemyPointType.NormalShootingMuzzle) : null;
    }

    public EffectType GetEffectType()
    {
        EnemyTag attributeTags = targetTag & EnemyTag.AttributeMask;
        EffectType result = EffectType.Normal;

        if (Utils.HasFlag(attributeTags, EnemyTag.Fire))
            result |= EffectType.Fire;
        if (Utils.HasFlag(attributeTags, EnemyTag.Ice))
            result |= EffectType.Ice;
        if (Utils.HasFlag(attributeTags, EnemyTag.Poison))
            result |= EffectType.Poison;
        if (Utils.HasFlag(attributeTags, EnemyTag.Lightning))
            result |= EffectType.Lightning;
        if (Utils.HasFlag(attributeTags, EnemyTag.Magma))
            result |= EffectType.Magma;

        return result;
    }

    private void OnValidate()
    {
        // Shoot 태그를 항상 포함, 다른 플래그는 유지
        targetTag |= EnemyTag.Shoot;
        firePointType = EnemyPointType.NormalShootingMuzzle;
    }
}
