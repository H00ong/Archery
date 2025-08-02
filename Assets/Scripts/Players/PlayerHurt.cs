using UnityEngine;

public class PlayerHurt : MonoBehaviour
{
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
            GameManager.Instance.PlayerManager.ChangePlayerState(PlayerState.Dead);
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

            Enemy enemy = collision.gameObject.GetComponentInParent<Enemy>();
            damage = enemy.GetAttackDamage();

            GetHit(damage);
        }
    }

}
