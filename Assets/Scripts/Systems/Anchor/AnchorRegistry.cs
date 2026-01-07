using System.Collections.Generic;
using UnityEngine;

namespace Systems.Anchor
{
    public sealed class AnchorRegistry : MonoBehaviour
    {
        public static AnchorRegistry Instance;
        [SerializeField] private int capacity = 100;
#if UNITY_EDITOR
        public int ActiveCount => _active.Count;
#endif

        private readonly Stack<SkillAnchor> _pool = new();
        private readonly HashSet<SkillAnchor> _active = new();

        private void Awake()
        {
            for (var i = 0; i < capacity; i++)
            {
                var go = new GameObject($"Anchor_{i}");
                go.SetActive(false);

                var anchor = go.AddComponent<SkillAnchor>();
                _pool.Push(anchor);
            }

            Instance ??= this;
        }

        public SkillAnchor Rent(Transform owner, Vector2 position)
        {
            if (_pool.Count == 0)
            {
                Debug.LogWarning("Anchor pool exhausted");
                return null;
            }

            var anchor = _pool.Pop();
            _active.Add(anchor);

            anchor.transform.position = position;
            anchor.owner = owner;
            anchor.active = true;

            anchor.gameObject.SetActive(true);
            return anchor;
        }

        public void Return(SkillAnchor anchor)
        {
            if (!_active.Remove(anchor))
                return;

            anchor.active = false;
            anchor.owner = null;

            anchor.gameObject.SetActive(false);
            _pool.Push(anchor);
        }
    }

}