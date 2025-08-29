using UnityEngine;

public class PlayerHurt : MonoBehaviour
{
    [SerializeField] PlayerManager playerManager;
    PlayerHeatlh playerHealth;
    string enemyTag = "Enemy";

    private void Start()
    {
        playerHealth = GetComponent<PlayerHeatlh>();

        playerHealth.InitializeHealth();
    }

    public void GetHit(float _damage)
    {
        if (PlayerManager.IsPlayerDead)
            return;

        // ������ ���� ����

        playerHealth.TakeDamage(_damage);

        if (playerHealth.IsDead()) 
        {
            playerManager.ChangePlayerState(PlayerState.Dead);
        }
        else
        {
            // �ǰ� ȿ��
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (PlayerManager.IsPlayerDead)
            return;

        // �浹�� ������Ʈ�� ���� �������� Ȯ��

        if (Utils.CompareTag(collision, enemyTag)) 
        {
            float damage = 0f;

            EnemyController enemy = collision.gameObject.GetComponentInParent<EnemyController>();
            damage = enemy.GetAtk();

            GetHit(damage);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (playerManager == null) playerManager = FindAnyObjectByType<PlayerManager>();
    }
#endif
}
