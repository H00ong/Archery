using UnityEngine;

public class PlayerHurt : MonoBehaviour
{
    [SerializeField] PlayerManager playerManager;
    [SerializeField] PlayerHeatlh playerHealth;
    string enemyTag = "Enemy";

    public void Init()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHeatlh>();
    }


    public void GetHit(float _damage)
    {
        playerHealth.TakeDamage(_damage);

        if (playerHealth.IsDead()) 
        {
            playerManager.ChangePlayerState(PlayerState.Dead);
            return;
        }
        else
        {
            // �ǰ� ȿ��
        }
    }

    public void GetHeal(int _healAmount, out bool valid) 
    {
        playerHealth.Heal(_healAmount, out valid);
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
        if (playerManager == null) playerManager = GetComponent<PlayerManager>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHeatlh>();
    }
#endif
}
