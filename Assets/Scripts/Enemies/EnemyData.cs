using UnityEngine;

[System.Serializable]
public class EnemyData
{

    public string enemyName;
    public float idleTime;
    public float moveTime;
    public float moveSpeed;

    #region MeleeEnemy
    public float attackRange;
    public float attackMoveSpeed; // �̵� �ӵ�
    #endregion

    public float attackSpeed;
    // public float attackDamage;
}

[System.Serializable]
public class EnemyDataList
{
    public EnemyData[] enemies;
}
