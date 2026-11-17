using System.Collections.Generic;
using Moves;
using Systems.Data;
using UnityEngine;

namespace PlayerScripts.Skills
{
    public partial class TargetResolver
    {
        private TargetResolveResult ResolveRelative(TargetRequest request)
        {
            AttackLog.Write(request.Trace, "TargetQuery", $"mode={request.Mode} relations={request.Targeting.Relations} version={request.Targeting.Version} queryLayers={TargetPolicy.CandidateLayers}");
            if (request.Mode == TargetMode.TowardsSelf)
            {
                var self = TargetPolicy.EntityOf(request.Caster);
                var allowed = request.Targeting.Allows(request.Caster, self);
                return new TargetResolveResult(allowed ? SkillTarget.ForEntity(self) : default, request.CasterPos, allowed);
            }
            // 좌표는 관계를 갖지 않습니다. None이어도 이동·연출 좌표는 구할 수 있습니다.
            if (request.Mode == TargetMode.TowardsCursor)
                return cursorResolver ? ResolveTowardsCursor(request) : new TargetResolveResult(default, request.CasterPos, false);
            if (request.Mode == TargetMode.TowardsEntity && Camera.main)
            {
                var point = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
                return ResolveRelativeAt(request, point);
            }
            AttackLog.Write(request.Trace, "TargetRejected", "reason=UnsupportedModeOrMissingCamera");
            return new TargetResolveResult(default, request.CasterPos, false);
        }

        /// <summary>커서/외부 조준 좌표에서 모든 후보를 검사합니다. 첫 거부 Collider가 뒤의 적을 가리지 않습니다.</summary>
        public TargetResolveResult ResolveRelativeAt(TargetRequest request, Vector2 point)
        {
            if (!request.Caster || !request.Targeting.IsValid || !EntityAttackSelection.ValidRange(request.MinRange, request.MaxRange))
                return new TargetResolveResult(default, request.CasterPos, false);
            var colliders = Physics2D.OverlapPointAll(point, TargetPolicy.CandidateLayers);
            var seen = new HashSet<Entity>();
            Entity best = null;
            var nearest = float.PositiveInfinity;
            foreach (var collider in colliders)
            {
                var entity = TargetPolicy.EntityOf(collider);
                if (!entity || !seen.Add(entity) || !request.Targeting.Allows(request.Caster, entity)) continue;
                var range = Vector2.Distance(request.CasterPos.AsVector2, entity.transform.position);
                if (range < request.MinRange || range > request.MaxRange) continue;
                var distance = ((Vector2)entity.transform.position - point).sqrMagnitude;
                if (distance < nearest || (distance == nearest && (!best || entity.GetInstanceID() < best.GetInstanceID())))
                { best = entity; nearest = distance; }
            }
            AttackLog.Write(request.Trace, best ? "TargetResolved" : "TargetRejected",
                $"reason={(best ? "Allowed" : "NoEligibleEntity")} target={AttackLog.Identity(best)} colliders={colliders.Length} relations={request.Targeting.Relations}");
            return best ? new TargetResolveResult(SkillTarget.ForEntity(best), new FixedVector2(best.transform.position), true) :
                new TargetResolveResult(default, request.CasterPos, false);
        }
    }
}
