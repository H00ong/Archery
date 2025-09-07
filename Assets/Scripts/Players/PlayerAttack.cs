using Game.Player;
using Game.Player.Attack;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Player.Attack
{
    public struct ShotInstruction
    {
        Vector3 shootingPos;
        Vector3 shootingDir;
    }
}

public class PlayerAttack : MonoBehaviour
{
    [Header("Debug Mode")]
    [SerializeField] bool debugMode;
    [Space]
    [Header("Required Components")]
    [SerializeField] PlayerSkill skill;
    [Header("Required Objects")]
    [SerializeField] AssetReferenceGameObject playerProjectile;
    [SerializeField] Transform projectileParent;
    [SerializeField] Transform shootingPos;
    [Space]
    [Header("Player Attack Info")]
    [SerializeField] float defaultAttackSpeed; // animation speed
    [SerializeField] float defaultProjectileSpeed;
    [SerializeField] float defaltProjectileLifetime = 10f; // Time after which the projectile will be destroyed if not used
    [SerializeField] int defaultPlayerAtk = 1;

    public static EnemyController currentTarget;
    public static float playerAttackSpeed { get; set; }
    public static int playerAtk; // Default player damage, can be modified later
    public static float projectileSpeed;

    public void Init() 
    {
        if (!debugMode) 
        {
            // data ��������
        }

        playerAtk = defaultPlayerAtk;
        playerAttackSpeed = defaultAttackSpeed;
        projectileSpeed = defaultProjectileSpeed;
    }

    public void Attack()
    {
        EnemyController target = GetEenemyTarget();

        if (target != null)
        {
            currentTarget = target;
            transform.rotation = Quaternion.LookRotation(target.transform.position - transform.position);
        }
    }

    private EnemyController GetEenemyTarget()
    {
        EnemyController target = null;
        float minDistance = float.MaxValue;

        if (EnemyManager.enemies.Count <= 0)
            return null;

        foreach (EnemyController enemy in EnemyManager.enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance < minDistance)
            {
                minDistance = distance;
                target = enemy;
            }
        }

        return target;
    }

    #region Animation Events
    public void Shoot()
    {
        StartCoroutine(ShootingCoroutine());
    }

    IEnumerator ShootingCoroutine() 
    {
        GameObject go = null;

        yield return PoolManager.Instance.GetObject(playerProjectile, inst => go = inst, projectileParent);

        if (go == null) yield break;

        Projectile_Player newProjectile = go.GetComponent<Projectile_Player>();

        if (newProjectile == null) yield break;

        Vector3 projectileDir = Utils.GetDirectionVector(currentTarget.transform.position, shootingPos.position);
        newProjectile.SetupProjectile(shootingPos.position, projectileDir, projectileSpeed, playerAtk, defaltProjectileLifetime, _isFlying: false);
    }

    private void BuildShotPlan() 
    {
        var list = new List<ShotInstruction>();

        // ����
        if (PlayerSkill.acquiredSkillModule.TryGetValue(PlayerSkillId.HorizontalShot, out PlayerSkillModuleBase horizontal) )
        {
            
        }

        //if ()
        //{
           
        //}

    }
    #endregion



#if UNITY_EDITOR
    private void OnValidate()
    {
        if (skill == null) skill = GetComponent<PlayerSkill>();
    }
#endif
}
