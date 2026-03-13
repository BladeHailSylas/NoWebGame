using System;
using PlayerScripts.Skills;
using Systems.Anchor;
using Systems.Data;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Moves.Mechanisms
{
    /// <summary>
    /// - CastContext에 포함된 Variable은 최대 하나 (Exclusive 정책)
    /// -> 추후 Public인 모든 Variable을 넘길 필요도 있으나 이 경우에는 Variable 제거를 고려하기 어려움
    /// - Variable "종류"를 기준으로 순차 분기한다.
    /// - 첫 번째로 일치하는 FollowUp을 실행한다.
    /// - 어떤 것도 일치하지 않으면 defaultFollowUp을 실행한다.
    /// </summary>
    [CreateAssetMenu(fileName = "SwitchMechanism", menuName = "Skills/Mechanisms/Switch")]
    public class SwitchMechanism : ScriptableObject, INewMechanism
    {
        public void Prepare(CastContext ctx)
        {
            // 현재는 의도적으로 비워 둔다.
            // Hold / Preview / UI 표시가 필요해질 경우 이 지점에서 확장.
        }

        public void Execute(CastContext ctx)
        {
            if (ctx.Params is not SwitchParams param)
                return;

            MechanismRef selected = default;
            var chosen = false;

            // CastContext에 Variable이 없는 경우도 고려
            var ctxVar = ctx.Var;

            // 1) Variable 분기 검사 (순서 = 우선순위)
            if (ctxVar.Variable is not null && param.cases != null)
            {
                foreach (var swCase in param.cases)
                {
                    if (swCase.variable is null) continue;
                    // 핵심: "이 Variable이 A인가?"
                    if (!ctxVar.Variable.ID.Equals(swCase.variable.ID)) continue;
                    selected = swCase.followUp;
                    chosen = true;
                    break;
                }
            }

            // 2) 어떤 case도 매칭되지 않았으면 default
            if (!chosen)
            {
                selected = param.defaultFollowUp;
            }

            // 3) 실행 가능하지 않으면 종료
            if (selected.mechanism is not INewMechanism mech)
            {
                if(ctx.Target.TryGetComponent<SkillAnchor>(out var anchor))
                    AnchorRegistry.Instance.Return(anchor);
                return;
            }

            // 4) FollowUp 실행
            var target = selected.requireRetarget ? null : ctx.Target;

            SkillCommand cmd = new(
                ctx.Caster,
                ctx.Mode,
                new FixedVector2(ctx.Caster.position),
                mech,
                selected.@params,
                ctx.Damage,
                target
            );

            CommandCollector.Instance.EnqueueCommand(cmd);
        }
    }

    // =========================
    // Params & Case Definitions
    // =========================

    [Serializable]
    public class SwitchParams : NewParams
    {
        [Tooltip("우선순위 순으로 검사되는 Variable 분기 목록")]
        public SwitchCase[] cases;

        [Tooltip("어떤 Variable에도 해당하지 않을 때 실행될 기본 FollowUp")]
        public MechanismRef defaultFollowUp;
    }

    [Serializable]
    public class SwitchCase
    {
        [Tooltip("이 Variable이면 해당 FollowUp 실행")]
        public VariableDefinition variable;

        [Tooltip("조건을 만족했을 때 실행할 FollowUp")]
        public MechanismRef followUp;
    }
}
