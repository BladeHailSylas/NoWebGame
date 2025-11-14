using System.Collections.Generic;
using SkillInterfaces;
using UnityEngine;

[CreateAssetMenu(fileName = "LaserMechanism", menuName = "Skills/Mechanisms/Laser")]
public class LaserMechanism : ScriptableObject, INewMechanism
{
    public void Execute(CastContext ctx)
    {
        if (ctx.Params is not LaserParams param) return;
        Vector2 origin = ctx.Caster.position;
        Vector2 target = ctx.Target.position;
        var dist = target - origin;
        var direction = dist.normalized;
        var distance = dist.magnitude * param.rangeMultiplier;

        // 감지: 적과 장애물 모두 탐색
        var hits = Physics2D.RaycastAll(origin, direction, distance, LayerMask.GetMask("Foe", "Walls&Obstacles"));
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            var layer = hit.collider.gameObject.layer;

            // 벽을 만나면 중단
            if (layer == LayerMask.NameToLayer("Walls&Obstacles"))
                break;

            // 적 판정
            var entity = hit.collider.GetComponent<Entity>();
            if (entity is null) continue;

            // FollowUp 즉시 발동
            foreach (var followup in param.onHitFollowUps)
            {
                if (followup.mechanism is not INewMechanism mech) continue;
                SkillCommand cmd = new(ctx.Caster, TargetMode.TowardsEntity,
                    new FixedVector2(entity.transform.position), mech, followup.@params, ctx.Damage, entity.transform);
                CommandCollector.Instance.EnqueueCommand(cmd);
            }
        }

        // 시각 효과 (디버그용)
        Debug.DrawRay(origin, direction * distance, Color.blue, 0.1f);
    }
}

[System.Serializable]
public class LaserParams : INewParams
{
    public short CooldownTicks { get; private set; }
    public float rangeMultiplier = 1f;
    public List<MechanismRef> onHitFollowUps;
}