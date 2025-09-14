using Game.Player.Attack;
using System.Collections.Generic;
using UnityEngine;

public abstract class PlayerSkillModuleBase : MonoBehaviour
{
    protected PlayerSkill playerSkill;
    public int Level { get; protected set; } = 1;
    
    public virtual void UpdateSkill() 
    {
        Level++;
    }

    public virtual void Init(PlayerSkill _skill) 
    {
        playerSkill = _skill;
    }
}

// ---------------------------
// �÷��̾� �ɷ�ġ ��ȭ �迭
// ---------------------------
public interface IPlayerUpgrader
{
    /// <summary>���ݷ� ��� �Ǵ� ����ġ ��ȯ</summary>
    public void Apply();
}

// ---------------------------
// �÷��̾� źȯ ���׷��̵� �迭 - �� �ݻ�, �� ����, �ݵ� ȭ��, Ʈ��ŷ ����
// ---------------------------

public interface IBulletUpgrader
{
    public void Apply();
}

// ---------------------------
// �÷��̾� źȯ �� ���� �迭
// ---------------------------

public interface IShootContributor
{
    public void AddBullet(List<ShotInstruction> _bulletList, ShotInstruction _default);
}

// ---------------------------
// �÷��̾� ���� �迭
// ---------------------------

public interface IOrbGenerator
{
    // ���� �Ŵ����� �ΰ� �ű⿡ ����ϴ� �ý������� ����
    public void GenerateOrb();
}

// ---------------------------
// �÷��̾� ���� ���� �迭
// ---------------------------

public interface IPickupActivatedBombardment
{
    public void GenerateItem();
}

// ---------------------------
// �� �迭
// ---------------------------

