using System.Collections.Generic;
using UnityEngine;

namespace Moves.Mechanisms
{
    /// <summary>
    /// Defines how hitscan-based skills behave — instant ray-based hit detection.
    /// </summary>
    [CreateAssetMenu(fileName = "HitscanMechanism", menuName = "Skills/Mechanisms/Hitscan")]
    public class HitscanMechanism : ScriptableObject, INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            if (ctx.Target is null)
            {
                //Debug.LogWarning("[HitscanMechanism] No target provided — skipping.");
                return;
            }

            if (ctx.Params is not HitscanParams param)
            {
                //Debug.LogError("Wrong Parameter");
                return;
            }

            if ((param.layerMask.value & (1 << ctx.Target.gameObject.layer)) == 0)
            {
                //Debug.Log("[HitscanMechanism] Target layer not allowed — skipping.");
                return;
            }

            Vector2 origin = ctx.Caster?.position ?? Vector2.zero;
            var direction = ((Vector2)ctx.Target.position - origin).normalized;
            var distance = Vector2.Distance(origin, ctx.Target.position);

            if (distance > param.MaxRange || distance < param.MinRange)
            {
                //Debug.Log($"[HitscanMechanism] Target out of range ({distance:F2}) — skipping.");
                return;
            }

            var hit = Physics2D.Raycast(origin, direction, param.MaxRange, param.layerMask);
            if (param.debugDraw)
            {
                var c = hit ? Color.red : Color.yellow;
                Debug.DrawRay(origin, direction * param.MaxRange, c, 0.5f);
            }

            if (hit.collider is not null)
            {
                SkillUtils.ActivateFollowUp(param.onHit, ctx, hit.transform);
            }

            SkillUtils.ActivateFollowUp(param.onExpire, ctx);
        }
    }

    [System.Serializable]
    public class HitscanParams : NewParams
    {
        [Header("Entity Settings")]
        public LayerMask layerMask = 1 << 8; // Default "Foe" layer
        public GameObject hitEffectPrefab;   // Placeholder — not used yet
        [Header("FollowUp")] 
        public List<MechanismRef> onHit;
        public List<MechanismRef> onExpire;
        [Header("Debug")]
        public bool debugDraw = true;
    }
}
