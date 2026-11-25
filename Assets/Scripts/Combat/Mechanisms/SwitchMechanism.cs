using global::NoWebGame.Combat.Stacks.Definitions;
using global::NoWebGame.Moves;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Diagnostics.Runtime;




using UnityEngine;

namespace NoWebGame.Combat.Mechanisms
{
    /// <summary>
    /// - CastContext에 포함된 Variable은 최대 하나 (Exclusive 정책)
    /// -> 추후 Public인 모든 Variable을 넘길 필요도 있으나 이 경우에는 Variable 제거를 고려하기 어려움
    /// - Variable "종류"를 기준으로 순차 분기한다.
    /// - 첫 번째로 일치하는 FollowUp을 실행한다.
    /// - 어떤 것도 일치하지 않으면 defaultFollowUp을 실행한다.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.Mechanisms", "Assembly-CSharp", "SwitchMechanism")]
    [System.Serializable]
    public class SwitchMechanism : NewMechanism, INewMechanism
    {
        // 부정형 저장: 옛 SerializeReference에 필드가 없으면 false이므로 기존 소비 동작을 유지합니다.
        [SerializeField, Tooltip("켜면 Variable을 소비하지 않습니다. 꺼짐(기본값)은 Consume=true입니다.")]
        private bool preserveVariable;
        public bool Consume { get => !preserveVariable; set => preserveVariable = !value; }
        [Tooltip("우선순위 순으로 검사되는 Variable 분기 목록")]
        public SwitchCase[] cases;

        [Tooltip("어떤 Variable에도 해당하지 않을 때 실행될 기본 FollowUp")]
        [SerializeReference] public MechanismData defaultFollowUp;

        public void Prepare(CastContext ctx)
        {
            // 현재는 의도적으로 비워 둔다.
            // Hold / Preview / UI 표시가 필요해질 경우 이 지점에서 확장.
        }

        protected override void ExecuteCore(CastContext ctx)
        {
            if (ctx.Mech is not SwitchMechanism mech)
                return;

            if (ctx.SwitchSelection != null)
            {
                SkillUtils.ActivateChain(new[] { ctx.SwitchSelection.Branch }, ctx, trigger: "SwitchSelected");
                return;
            }
            // 직접 실행은 큐 수락 경계를 거치지 않습니다. 소비 증표 없는 유료 분기를 무료 실행하지 않습니다.
            if (Consume && ctx.Var.Variable && (ctx.Var.Consumption == null || !ctx.Var.Consumption.Consumed))
            { AttackLog.Write(ctx.Trace, "ExecutionRejected", "reason=SwitchRequiresAcceptedConsumption"); return; }

            MechanismData selected = default;
            var chosen = false;
            var ctxVar = ctx.Var;

            if (ctxVar.Variable is not null && mech.cases != null)
            {
                foreach (var swCase in mech.cases)
                {
                    if (swCase.variable is null) continue;
                    if (!ctxVar.Variable.ID.Equals(swCase.variable.ID)) continue;
                    selected = swCase.followUp;
                    chosen = true;
                    break;
                }
            }

            if (!chosen)
            {
                selected = mech.defaultFollowUp;
            }

            SkillUtils.ActivateChain(new[] { selected }, ctx, trigger: "new[] { selected }");
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.Mechanisms", "Assembly-CSharp", "SwitchCase")]
    [System.Serializable]
    public class SwitchCase
    {
        [Tooltip("이 Variable이면 해당 FollowUp 실행")]
        public VariableDefinition variable;

        [Tooltip("조건을 만족했을 때 실행할 FollowUp")]
        [SerializeReference] public MechanismData followUp;
    }
}
