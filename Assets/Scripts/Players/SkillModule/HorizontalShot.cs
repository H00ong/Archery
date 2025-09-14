using Game.Player.Attack;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditorInternal.ReorderableList;

public class HorizontalShot : PlayerSkillModuleBase, IShootContributor
{
    [SerializeField, Min(0f)] private float interval = 1f;

    // ������ �߻� ���� ���� (����)
    // level 1 -> 2��, level 2 -> 3��, �� �̻��� ���� ���� �� ��åȭ
    private int GetBulletCount(int lvl)
    {
        return lvl + 1;
    }
    
    // normalized vector �� �´ٰ� ����
    public void AddBullet(List<ShotInstruction> bulletList, ShotInstruction _default)
    {
        if (bulletList == null) return;

        int count = GetBulletCount(Level);
        if (count <= 0) return;

        // ����Ʈ �뷮 ����(���� ����: GC/���Ҵ� ���̱�)
        int need = bulletList.Count + count;
        if (bulletList.Capacity < need) bulletList.Capacity = need;

        Vector3 origin = _default.shootingPos;
        Vector3 dir = _default.shootingDir;
        Vector3 up = Vector3.up;          // ���� �� �� ���� ȸ��
        Vector3 right = Vector3.Cross(up, dir);

        // �߽� ����: i = 0..count-1, half = (count-1)/2
        // offset = (i - half) * interval  => ¦���� ��0.5, ��1.5�� / Ȧ���� 0, ��1, ��2��
        float half = (count - 1) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float offset = (i - half) * interval;
            Vector3 pos = origin + right * offset;

            bulletList.Add(new ShotInstruction
            {
                shootingDir = _default.shootingDir,
                shootingPos = pos
            });
        }
    }
}

