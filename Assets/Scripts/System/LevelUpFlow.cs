using UnityEngine;

public class LevelUpFlow : MonoBehaviour
{
    [SerializeField] SkillChoicePopupView _skillChoicePopup;
    [SerializeField] Transform _popupContainer;
    [SerializeField] PlayerSkill _playerSkill; // ���� ���� ������Ʈ ����

    public static event System.Action OnLevelUp; // ������ �ñ׳�
    public static event System.Action OnSkillChosen; // ��ų ���� �ñ׳�

    private void Start()
    {
        if (_playerSkill == null)
            _playerSkill = FindAnyObjectByType<PlayerSkill>();

        OnLevelUp += ShowSkillChoicePopup;
    }

    public void ShowSkillChoicePopup()
    {
        var popup = Instantiate(_skillChoicePopup, _popupContainer);
        var presenter = new SkillChoicePresenter(
            skillPopup : popup, _playerSkill, onCompleted: OnSkillChosen);

        presenter.Show();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            OnLevelUp?.Invoke();
    }
}