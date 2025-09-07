using System;
using UnityEngine;

public enum StageState 
{
    Combat,
    Clear,
    Loading,
}

// �ڷ�ƾ���� Ui �ϸ��� ������ loading -> combat / enemy���� �� combat -> clear / clear -> portal ���� clear -> Loading

public class StageManager : MonoBehaviour
{
    [Header("Required Managers")]
    [SerializeField] MapManager mapManager;

    public static int currentStageIndex = 1;
    public static StageState CurrentState { get; private set; } = StageState.Combat;
    
    public static event Action OnStageCleared;
    public static Action OnCombat;

    private void OnEnable()
    {
        Init();
    }

    private void Init()
    {
        OnCombat = UpdateStage;

        InitStage();
    }

    private void OnDisable()
    {
        OnStageCleared = null;
        OnCombat = null;
    }

    public void InitStage() 
    {
        currentStageIndex = 0;

        UpdateStage();
    }

    public void UpdateStage()
    {   
        currentStageIndex++;

        mapManager.GetNewMap();
        mapManager.PositionPlayer();
        mapManager.SpawnEnemy();
    }

    public static void ChangeStageState(StageState _newState) 
    {
        CurrentState = _newState;

        if (CurrentState == StageState.Clear) OnStageCleared?.Invoke();
        if(CurrentState == StageState.Combat) OnCombat?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!mapManager) mapManager = FindAnyObjectByType<MapManager>();
    }
#endif
}
