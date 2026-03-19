using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NameRegistry", menuName = "Registry/NameRegistry")]
public class NameRegistry : ScriptableObject
{
    [Header("Enemy Names")]
    public List<string> enemyNames = new();

    [Header("Map IDs")]
    public List<string> mapIds = new();

    [Header("Character Names")]
    public List<string> characterNames = new();

    [Header("Skill IDs")]
    public List<string> skillIds = new();

#if UNITY_EDITOR
    private static NameRegistry _instance;

    public static NameRegistry Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:NameRegistry");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<NameRegistry>(path);

                return _instance;
            }
            
            return null;
        }
    }
#endif
}
