using System.Collections.Generic;
using PlayerScripts.Skills;
using Systems.Anchor;
using Systems.Data;
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

            if (distance > param.maxRange || distance < param.minRange)
            {
                //Debug.Log($"[HitscanMechanism] Target out of range ({distance:F2}) — skipping.");
                return;
            }

            var hit = Physics2D.Raycast(origin, direction, param.maxRange, param.layerMask);
            if (param.debugDraw)
            {
                var c = hit ? Color.red : Color.yellow;
                Debug.DrawRay(origin, direction * param.maxRange, c, 0.5f);
            }

            if (hit.collider is not null)
            {
                OnHit(ctx);
            }
            OnFinished(ctx);
        }
        protected virtual void OnHit(CastContext ctx)
        {
            if (ctx.Params is not HitscanParams param) return;
            // Placeholder for EffectSkill or damage pipeline.
            foreach (var followup in param.onHit)
            {
                //Debug.Log($"I got {ctx.Damage.Attack}");
                if (followup.mechanism is not INewMechanism mech) continue;
                //Debug.Log($"[HitscanMechanism] Now enqueuing {mech.GetType().Name}");
                var ctxTarget = !followup.requireRetarget ? ctx.Target : null;
                SkillCommand cmd = new(ctx.Caster, ctx.Mode, new FixedVector2(ctx.Caster.position),
                    mech, followup.@params, ctx.Damage, ctxTarget);
                CommandCollector.Instance.EnqueueCommand(cmd);
                //Debug.Log($"You are casting {mech.GetType().Name} as a followup to {hitTarget.name}");
            }

            //Debug.Log($"[HitscanMechanism] OnHit triggered on {ctx.Target.name}");
        }

        protected virtual void OnFinished(CastContext ctx)
        {
            if (ctx.Params is not HitscanParams param) return;
            if (param.onExpire.Count == 0)
            {
                if (!ctx.Target.TryGetComponent<SkillAnchor>(out var anchor)) return;
                AnchorRegistry.Instance.Return(anchor);
            }

            foreach (var followup in param.onExpire)
            {
                if (followup.mechanism is not INewMechanism mech) continue;
                var ctxTarget = !followup.requireRetarget ? ctx.Target : null;
                SkillCommand cmd = new(ctx.Caster, ctx.Mode, new FixedVector2(ctx.Caster.position),
                    mech, followup.@params, ctx.Damage, ctxTarget);
                CommandCollector.Instance.EnqueueCommand(cmd);
            }
        }
    }

    [System.Serializable]
    public class HitscanParams : INewParams
    {
        [Header("Hitscan Settings")]
        // Range limits for this mechanism (world units).
        public float minRange;
        public float maxRange = 10f;
        [Header("Entity Settings")]
        public LayerMask layerMask = 1 << 8; // Default "Foe" layer
        public GameObject hitEffectPrefab;   // Placeholder — not used yet
        [Header("Ticker")] 
        [SerializeField] private short cooldownTicks;
        public short CooldownTicks => cooldownTicks;
        public float MinRange => minRange / 1000;
        public float MaxRange => maxRange / 1000;
        [Header("FollowUp")] 
        public List<MechanismRef> onHit;
        public List<MechanismRef> onExpire;
        [Header("Debug")]
        public bool debugDraw = true;
    }
}
