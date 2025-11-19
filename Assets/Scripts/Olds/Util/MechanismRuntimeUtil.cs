using Systems.Data;
using UnityEngine;

namespace Olds.Util
{
	/// <summary>
	///     Intent 파이프라인과 메커니즘 사이의 런타임 헬퍼입니다. FollowUp 생성과 캐스트 체인 전달을 표준화합니다.
	/// </summary>
	public static class MechanismRuntimeUtil
	{
		/// <summary>
		///     IntentOrchestrator가 60 FPS 기준으로 동작한다고 가정한 틱 전환 상수입니다.
		///     <para>필요 시 <see cref="ConvertSecondsToTiming"/> 부분을 교체해 프로젝트의 실제 틱레이트에 맞추십시오.</para>
		/// </summary>
		const float TickRate = 60f;

		/// <summary>
		///     FollowUpProvider에서 정의한 후속 Intent를 전역 큐에 등록합니다.
		/// </summary>
		/// <param name="provider">FollowUp 정의를 제공하는 파라미터입니다.</param>
		/// <param name="hook">후속 실행을 트리거한 훅입니다.</param>
		/// <param name="prevTarget">연속 타격에 사용할 우선 대상입니다. 없으면 CastScope의 현재 타깃을 참조합니다.</param>
		/// <param name="templateIdPrefix">FollowUp TemplateId에 붙일 접두사입니다.</param>
		public static void QueueFollowUps(IFollowUpProvider provider, AbilityHook hook, Transform prevTarget = null, string templateIdPrefix = null)
		{
			/*if (provider == null) return;
		if (!CastScope.TryGetContext(out var parent, out _))
		{
			Debug.LogWarning("CastScope 컨텍스트 없이 FollowUp을 생성할 수 없습니다.");
			return;
		}

		var fallbackTarget = prevTarget != null ? prevTarget : CastScope.CurrentTarget;
		foreach (var (order, delaySeconds, respectBusyCooldown) in provider.BuildFollowUps(hook, fallbackTarget))
		{
			if (order.Mech == null || order.Param == null) continue;

			var (timing, delayTicks) = ConvertSecondsToTiming(delaySeconds);
			var request = ResolveTargetRequest(order.TargetOverride, parent.TargetRequest, fallbackTarget);
			var templateId = BuildTemplateId(templateIdPrefix, hook, order.Mech);

			var followUp = CastIntent.FollowUp(
				parent,
				hook.ToString(),
				templateId,
				order.Mech,
				order.Param,
				timing,
				delayTicks,
				respectBusyCooldown,
				request,
				guardKey: null,
				dedupKey: null,
				priority: parent.PriorityLevel);

			CastScope.AddIntent(followUp);
		}*/
		}

		/// <summary>
		///     Switch 메커니즘 등에서 임의의 CastOrder를 FollowUp으로 등록할 때 사용합니다.
		/// </summary>
		public static void QueueCastOrder(CastOrder order, AbilityHook hook, float delaySeconds, bool respectBusyCooldown, string templateIdPrefix = null, Transform prevTarget = null)
		{/*
		if (order.Mech == null || order.Param == null) return;
		if (!CastScope.TryGetContext(out var parent, out _))
		{
			Debug.LogWarning("CastScope 컨텍스트 없이 CastOrder를 큐잉할 수 없습니다.");
			return;
		}

		var (timing, delayTicks) = ConvertSecondsToTiming(delaySeconds);
		var request = ResolveTargetRequest(order.TargetOverride, parent.TargetRequest, prevTarget ?? CastScope.CurrentTarget);
		var templateId = BuildTemplateId(templateIdPrefix, hook, order.Mech);

		var followUp = CastIntent.FollowUp(
			parent,
			hook.ToString(),
			templateId,
			order.Mech,
			order.Param,
			timing,
			delayTicks,
			respectBusyCooldown,
			request,
			guardKey: null,
			dedupKey: null,
			priority: parent.PriorityLevel);

		CastScope.AddIntent(followUp);*/
		}
		/*
	static (IntentTiming timing, int delayTicks) ConvertSecondsToTiming(float delaySeconds)
	{
		if (delaySeconds <= 0f)
		{
			return (IntentTiming.Immediate, 0);
		}

		int ticks = Mathf.Max(1, Mathf.RoundToInt(delaySeconds * TickRate));
		return (IntentTiming.Delayed, ticks);
	}*/
		/*static TargetRequest ResolveTargetRequest(Transform overrideTarget, TargetRequest parentRequest, Transform fallbackTarget)
	{
		if (overrideTarget != null)
		{
			return TargetRequest.SameActor(overrideTarget);
		}
		if (fallbackTarget != null)
		{
			return TargetRequest.SameActor(fallbackTarget);
		}
		return parentRequest;
	}*/

		static string BuildTemplateId(string prefix, AbilityHook hook, ISkillMechanism mech)
		{
			var mechName = mech != null ? mech.GetType().Name : "<null>";
			if (string.IsNullOrEmpty(prefix))
			{
				return $"{hook}:{mechName}";
			}
			return $"{prefix}:{hook}:{mechName}";
		}

		// 향후 디버그를 위해 FollowUp 생성 이벤트를 로깅하려면 여기서 UnityEvent 또는 CustomLogger를 연결하십시오.
	}
}
