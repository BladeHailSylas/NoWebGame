using System.Collections;
using UnityEngine;
using SkillInterfaces;
using Intents;

[CreateAssetMenu(menuName = "Mechanics/Switch Controller")]
public class SwitchSkillMechanism : SkillMechanismBase<SwitchControllerParams>
{
	public override IEnumerator Execute(Transform owner, Camera cam, SwitchControllerParams p)
	{
		/*if (p == null)
		{
			Debug.LogWarning("SwitchSkillMechanism: 파라미터가 null입니다.");
			yield break;
		}

		if (!CastScope.TryGetContext(out _, out _))
		{
			Debug.LogWarning("SwitchSkillMechanism: CastScope 컨텍스트가 없어 동작을 중단합니다.");
			yield break;
		}

		var prevTarget = CastScope.CurrentTarget;
		if (p.TrySelect(owner, cam, prevTarget, out var order, out var reference))
		{
			var followTarget = order.TargetOverride != null ? order.TargetOverride : prevTarget;
			MechanismRuntimeUtil.QueueCastOrder(order, AbilityHook.OnCastEnd, reference.delay, reference.respectBusyCooldown, "Switch", followTarget);
			//필요 시 switch 단계별로 UI를 갱신하려면 이 지점에서 이벤트를 브로드캐스트하십시오.
		}
		else
		{
			Debug.LogWarning("SwitchSkillMechanism: 실행할 스텝을 찾지 못했습니다.");
		}*/
	
		yield break;
	}
}
