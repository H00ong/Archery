using Game.Player;
using UnityEngine;

public class PlayerHurt : MonoBehaviour
{
    [SerializeField] PlayerManager playerManager;
    [SerializeField] PlayerHeatlh playerHealth;

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

        var hitRoot = collision.collider.attachedRigidbody ? collision.collider.attachedRigidbody.gameObject
                                              : collision.collider.gameObject;

        if(hitRoot.TryGetComponent<EnemyController>(out var enemy))
        {
            float atk = enemy.GetAtk();

            GetHit(atk);
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
