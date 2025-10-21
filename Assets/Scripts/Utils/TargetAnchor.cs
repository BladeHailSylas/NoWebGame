using UnityEngine;
using System.Collections.Generic;

public static class TargetAnchorPool
{
    static readonly Stack<Transform> Pool = new();
    static Transform _root;
	public static bool IsAnchor(Transform target)
	{
		return Pool.Contains(target);
	}
    public static Transform Acquire(Vector3 pos)
    {
        if (!_root)
        {
            var go = new GameObject("_TargetAnchors");
            Object.DontDestroyOnLoad(go);
            _root = go.transform;
        }

        var t = Pool.Count > 0 ? Pool.Pop() : new GameObject("Anchor").transform;
        t.SetParent(_root, false);
        t.position = pos;
        t.gameObject.SetActive(true);
        //Display(for debug)
        if (!t.GetComponent<TargetAnchorDebug>())
            t.gameObject.AddComponent<TargetAnchorDebug>();
        return t;
    }

    public static void Release(Transform t)
    {
        if (!t || Pool.Contains(t)) return;
        t.gameObject.SetActive(false);
        t.SetParent(_root, false);
        Pool.Push(t);
    }
}