using System;
using System.Linq;
using PlayerScripts.Skills;
using Systems.Data;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(fileName = "SwitchMechanism", menuName = "Skills/Mechanisms/Switch")]
    public class SwitchMechanism : ScriptableObject, INewMechanism
    {

        public void Prepare(CastContext ctx)
        {
            if (ctx.Params is not SwitchParams param) return;
            foreach (var item in param.switchFollowUps)
            {
                //Implement Cast Preparation
            }
        }
        public void Execute(CastContext ctx)
        {
            if (ctx.Params is not SwitchParams param || !ctx.Var.Variable.ID.Equals(param.variable.ID))
                return;
            var variable = ctx.Var;
            var conditions = param.points.OrderByDescending(x => x).ToArray();
            var followUps = param.switchFollowUps;

// followUps가 더 적을 수 있으므로 offset 계산
            int cCount = conditions.Length;
            int fCount = followUps.Length;
            int offset = cCount - fCount;

// 선택된 follow-up을 여기에 저장
            MechanismRef selected = default;
            var chosen = false;
// 조건 검사 (내림차순)
            for (int i = 0; i < cCount; i++) 
            {
                if (variable.Amount >= conditions[i])
                {
                    // follow-up index 계산
                    int fIdx = i - offset;

                    // followUp 부족 시 default(맨 앞) 사용
                    if (fIdx < 0)
                        fIdx = 0;

                    // followUp 배열 범위 초과 방지
                    if (fIdx >= fCount)
                        fIdx = fCount - 1;
                    chosen = true;
                    selected = followUps[fIdx];
                    break;
                }
            }

// 조건을 하나도 만족시키지 못한 경우 (variable.Amount < 모든 conditions)
            if (!chosen && fCount > 0)
            {
                chosen = true;
                selected = followUps[0]; // 디폴트 분기
            }
        
// 이제 selected를 실행
            if (!chosen || selected.mechanism is not INewMechanism mech) return;
            
            var ctxTarget = !selected.requireRetarget ? ctx.Target : null;
            SkillCommand cmd = new(ctx.Caster, ctx.Mode, new FixedVector2(ctx.Caster.position),
                mech, selected.@params, ctx.Damage, ctxTarget);
            CommandCollector.Instance.EnqueueCommand(cmd);
        }
    }
    [Serializable]
    public class SwitchParams : NewParams
    {
        public VariableDefinition variable;
        public int[] points;
        public MechanismRef[] switchFollowUps;
    }
}
