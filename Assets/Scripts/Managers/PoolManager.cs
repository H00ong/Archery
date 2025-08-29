using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class PoolManager : MonoBehaviour
{
    [SerializeField] Transform Inactive;
    // ���� Ǯ ���� (�����մ� 1 �ڵ鸸 ����)
    class Pool
    {
        public AsyncOperationHandle<GameObject> PrefabHandle; // LoadAssetAsync �ڵ�(������ ����)
        public GameObject Prefab;                              // PrefabHandle.Result
        public Transform Root;                                 // ��Ȱ�� ��Ʈ (�׻� SetActive(false))
        public readonly HashSet<GameObject> All = new();       // ���� ���� ��ü �ν��Ͻ�
        public readonly Queue<GameObject> Inactive = new();    // ���� ��⿭
    }

    // Ű: Addressables GUID (AssetReference.AssetGUID)
    private readonly Dictionary<string, Pool> _pools = new();
    // �ν��Ͻ� �� �Ҽ� Ǯ GUID ������
    private readonly Dictionary<GameObject, string> _instanceToKey = new();

    [Serializable]
    class PoolTag : MonoBehaviour { public string key; }

    // ---------- Public API ----------

    /// <summary>
    /// ������ �ε� + count�� ����(��Ȱ�� ��Ʈ �Ʒ� ����)
    /// parent�� "���� ����"���� ������ �ϰ�, ����/������ �׻� ��Ȱ�� ��Ʈ �Ʒ����� �����մϴ�.
    /// </summary>

    public IEnumerator Prewarm(AssetReferenceGameObject aref, int count = 5)
    {
        string key = aref.AssetGUID;
        yield return EnsurePoolLoaded(key, aref); // StartCoroutine �����ص� ��
        var pool = _pools[key];                   // ���⼭ �����ϰ� ����
        pool.Root = Inactive;

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(pool.Prefab, pool.Root, false);
            go.SetActive(false);
            EnsureTag(go, key);
            _instanceToKey[go] = key;
            pool.All.Add(go);
            pool.Inactive.Enqueue(go);
            // �ʿ� �� ������ �л�:
            // yield return null;
        }
    }

    /// <summary>
    /// ��� �Ҵ� �õ�(��� X). ���� �� instance ��ȯ.
    /// ���� �������� �θ� �̵��� �����ϼ���(Ȱ��ȭ�� ȣ���� ��å�� ����).
    /// ���� �ÿ��� �׳� ��ü ������ ���� ���� �����Ѵ�. �� prewarm���� �������� �� ���� ���� �뵵
    /// </summary>
    public bool TryGetObject(AssetReferenceGameObject aref, out GameObject instance, Transform parentOverride = null)
    {
        instance = null;
        if (!_pools.TryGetValue(aref.AssetGUID, out var pool))
            return false;

        while (pool.Inactive.Count > 0 && instance == null)
        {
            if (parentOverride != null) pool.Root = parentOverride;
            var go = pool.Inactive.Dequeue();
            if (go == null) continue; // �̹� �ı��� ��� ���
            instance = go;

            if (parentOverride != null)
                instance.transform.SetParent(pool.Root);

            return true;
        }
        return false;
    }

    /// <summary>
    /// ������ �����ؼ� �Ѱ���(��� O). onReady�� "��Ȱ�� ����"�� �ݹ��մϴ�.
    /// extraPrewarmCount��ŭ �߰� ������ ��׶���� �����մϴ�.
    /// </summary>
    public IEnumerator GetObject(
        AssetReferenceGameObject aref,
        Action<GameObject> onReady,
        Transform parentOverride = null,
        int extraPrewarmCount = 4
    )
    {
        string key = aref.AssetGUID;
        yield return EnsurePoolLoaded(key, aref);
        var pool = _pools[key];                   

        // 1) ��Ȱ�� ť �켱 ����
        while (pool.Inactive.Count > 0)
        {
            var go = pool.Inactive.Dequeue();
            if (go == null) continue;

            if (parentOverride != null)
                go.transform.SetParent(parentOverride, false);

            // ������ �׻� ��Ȱ����. �Ѵ� Ÿ�̹��� ȣ���� å��.
            go.SetActive(false);
            onReady?.Invoke(go);
            yield break;
        }

        // 2) ������ 1�� �����ؼ� ��� ����(��Ȱ�� ����)
        if(parentOverride != null) pool.Root = parentOverride;
        var instance = Instantiate(pool.Prefab, pool.Root, false);
        instance.SetActive(false);
        EnsureTag(instance, key);
        _instanceToKey[instance] = key;
        pool.All.Add(instance);

        onReady?.Invoke(instance);

        // 3) ��׶��� ����(��Ȱ�� ��Ʈ �Ʒ� ����)
        if (extraPrewarmCount > 0)
            StartCoroutine(CreateAndEnqueue(pool, key, extraPrewarmCount));
    }

    /// <summary>
    /// �ݳ�: ��Ȱ�� ��Ʈ�� ���� + ť ����.
    /// </summary>
    public void ReturnObject(GameObject instance)
    {
        if (instance == null) return;

        if (!_instanceToKey.TryGetValue(instance, out var key) || !_pools.TryGetValue(key, out var pool))
        {
            // Ǯ �Ҽ��� �ƴϸ� ��������� ���⸸.
            instance.SetActive(false);
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(pool.Root, false);
        pool.Inactive.Enqueue(instance);
    }

    /// <summary>
    /// ��� Ǯ ����: �ν��Ͻ� Destroy, ������ �ڵ� Release, ��Ʈ �ı�.
    /// (LoadAssetAsync ��θ� ���)
    /// </summary>
    public void ClearAllPools()
    {
        foreach (var kv in _pools)
        {
            var pool = kv.Value;

            // �ν��Ͻ� �ı�
            foreach (var go in pool.All)
                if (go != null) Destroy(go);
            pool.All.Clear();
            pool.Inactive.Clear();

            // ������ �ڵ� Release (�� 1ȸ)
            if (pool.PrefabHandle.IsValid())
                Addressables.Release(pool.PrefabHandle);
        }
        _pools.Clear();
        _instanceToKey.Clear();
    }

    // ---------- Internals ----------

    // ������ �ڵ�� ��Ȱ�� ��Ʈ�� ����
    private IEnumerator EnsurePoolLoaded(string key, AssetReferenceGameObject aref)
    {
        if (!_pools.TryGetValue(key, out var pool))
        {
            pool = new Pool();
            _pools.Add(key, pool);
        }

        // ������ �ڵ� �غ�(1ȸ �ε�)
        if (!pool.PrefabHandle.IsValid())
        {
            pool.PrefabHandle = aref.LoadAssetAsync<GameObject>(); // or Addressables.LoadAssetAsync<GameObject>(key)
            yield return pool.PrefabHandle;

            if (pool.PrefabHandle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"Addressables LoadAssetAsync failed: {key}");

            pool.Prefab = pool.PrefabHandle.Result;
            pool.Root   = Inactive;
        }

        yield return pool;
    }

    // �񵿱� ����: count�� ���� �� ��Ȱ�� ��Ʈ�� ����
    private IEnumerator CreateAndEnqueue(Pool pool, string key, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(pool.Prefab, pool.Root, false);
            go.SetActive(false);
            EnsureTag(go, key);
            _instanceToKey[go] = key;
            pool.All.Add(go);
            pool.Inactive.Enqueue(go);

            // �ʿ� �� ������ �л�
            // yield return null;
        }
        yield break;
    }

    private static void EnsureTag(GameObject go, string key)
    {
        var tag = go.GetComponent<PoolTag>();
        if (tag == null) tag = go.AddComponent<PoolTag>();
        tag.key = key;
    }
}
