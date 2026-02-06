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
        return EnemyTagUtil.ToEffectType(targetTag);
    }

    private void OnValidate()
    {
        // Shoot 태그를 항상 포함, 다른 플래그는 유지
        targetTag |= EnemyTag.Shoot;
        firePointType = EnemyPointType.NormalShootingMuzzle;
    }
}
