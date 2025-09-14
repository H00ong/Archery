using Game.Player.Attack;
using System.Collections.Generic;
using UnityEngine;

public class DiagonalShot : PlayerSkillModuleBase, IShootContributor
{
    [SerializeField, Min(0f)] private float interval = 2f; // �¿� �ִ� ������
    [SerializeField, Min(0f)] private float angle = 40f;     // �� �� �Ѿ��� �ִ� ȸ�� ����(��)

    private int GetBulletCount(int lvl) => Mathf.Max(2 * lvl, 2); // �׻� ¦��(�߾� ����)

    // normalized vector3�� �´ٰ� ����
    public void AddBullet(List<ShotInstruction> bulletList, ShotInstruction _default)
    {
        if (bulletList == null) return;

        int count = GetBulletCount(Level);               // ��: level=1 �� 2��, 2 �� 4�� ��
        int pairs = count / 2;                           // �¿� �� ����
        if (pairs <= 0) return;

        // �뷮 ����
        int need = bulletList.Count + count;
        if (bulletList.Capacity < need) bulletList.Capacity = need;

        Vector3 origin = _default.shootingPos;
        Vector3 dir0 = _default.shootingDir;
        Vector3 up     = Vector3.up;          // ���� �� �� ���� ȸ��
        Vector3 right  = Vector3.Cross(up, dir0);

        // �ε����� [-pairs..-1, 1..pairs]�� ���� �� �߾�(0) ����
        for (int i = 0; i < count; i++)
        {
            int k = i - (i < pairs ? pairs : pairs - 1); // [-pairs..-1, 1..pairs]
            float t = k / (float)pairs;                  // ����ȭ ������: [-1..-1/pairs]��[1/pairs..1]

            float offset = t * interval;                 // �¿� ��ġ: [-interval..interval]
            Vector3 pos = origin + right * offset;

            // ������ ����ȭ�Ͽ� �ش��� ��angle �ǵ���
            Quaternion rot = Quaternion.AngleAxis(angle * t, up);
            Vector3 dir = rot * dir0;

            bulletList.Add(new ShotInstruction
            {
                shootingDir = dir,   // �ݵ�� ȸ�� ����� ���� ���
                shootingPos = pos
            });
        }
    }
}

