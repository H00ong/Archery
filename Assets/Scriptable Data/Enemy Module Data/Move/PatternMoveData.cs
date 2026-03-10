using System.Collections.Generic;
using Enemy;
using UnityEngine;

[CreateAssetMenu(fileName = "PatternMoveData", menuName = "Enemy/ModuleData/PatternMove")]
public class PatternMoveData : MoveModuleData
{
    private void OnEnable()
    {
        targetTag = EnemyTag.PatternMove;
    }


    public List<Vector3> GetPoints(EnemyController controller)
    {
        return controller.enemyReference != null
            ? controller.enemyReference.GetPatrolPoints()
            : null;
    }
    
    private void OnValidate()
    {
        targetTag = EnemyTag.PatternMove;
    }
}
