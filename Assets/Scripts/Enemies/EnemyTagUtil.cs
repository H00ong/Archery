using System.Collections.Generic;
using System;
using UnityEngine;
using Game.Enemies.Enum;

namespace Game.Enemies
{
    public static class EnemyTagUtil
    {
        public static EnemyTag ParseTagsToMask(IEnumerable<string> tags)
        {
            if (tags == null) return EnemyTag.None;
            EnemyTag mask = EnemyTag.None;
            foreach (string tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag)) continue; // �� ���ڳ� ���鹮���� ��츦 ���� ����ó��
                var norm = tag.Trim();
                if (System.Enum.TryParse(norm, ignoreCase: true, out EnemyTag t))
                    mask |= t;
                else
                    Debug.LogWarning($"Unknown tag: {tag}");
            }

            return mask;
        }

        public static EnemyTag ParseTagsToMask(string tag)
        {
            return ParseTagsToMask(new[] { tag });
        }

        public static bool Has(EnemyTag mask, EnemyTag t) => (mask & t) != 0;
    }

    public struct EnemyKey : IEquatable<EnemyKey>
    {
        public EnemyName Name;
        public EnemyTag Tag;

        public bool Equals(EnemyKey other) => Name == other.Name && Tag == other.Tag;
        public override int GetHashCode() => ((int)Name * 397) ^ (int)Tag; // �ؽ����̺��� �ؽ� ���� �ʿ��ϹǷ� �̸� ���ؼ� 397�̶�� ������ �Ҽ��� ���
    };

    
}