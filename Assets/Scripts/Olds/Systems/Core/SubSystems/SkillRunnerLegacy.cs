// SkillRunner.cs — Intent 전용 Runner. 액터당 1개만 존재해야 합니다.
// - Runner는 IntentOrchestrator로 Root Intent만 전달합니다.
// - FollowUp은 CastScope.AddIntent를 통해 Orchestrator가 직접 수집합니다.
// - Busy/Cooldown 상태는 Orchestrator에서 관리합니다.
// 잠재적 문제: 현재 Owner Transform을 Awake 시점에 고정하며, 런타임에 변경되면 Intent가 잘못된 위치를 참조할 수 있습니다.

/*using Intents;
using SkillInterfaces;
using Unity.VisualScripting;
using UnityEngine;

public sealed class SkillRunner : MonoBehaviour, ISkillRunner
{
	[Header("Actor")]
        [SerializeField] ushort actorIdValue = 1;
        [SerializeField] Camera boundCamera;

	[Header("Queueing")]
	[SerializeField] bool respectBusyCooldown = true;
	[SerializeField] int defaultPriority;

	[Header("Debug")]
	[SerializeField] bool verbose;

        int _rootSequence;
        IntentOrchestrator _orchestrator;
        EntityId _actorId;

	void Awake()
	{
                _orchestrator = IntentOrchestrator.Instance;
                if (_orchestrator == null)
                {
                        Debug.LogError("IntentOrchestrator 인스턴스를 찾을 수 없습니다. Runner가 작동하지 않습니다.");
                }
                _actorId = new EntityId(actorIdValue);
                boundCamera ??= Camera.main;
	}

	void OnEnable()
	{
                _orchestrator ??= IntentOrchestrator.Instance;
                if (_orchestrator == null)
                {
                        Debug.LogWarning("Orchestrator 부재: Intent를 큐잉할 수 없습니다.");
                }
                if (!_actorId.IsValid)
                {
                        _actorId = new EntityId(actorIdValue);
                }
        }

        public bool IsBusy => _orchestrator != null && _orchestrator.IsActorBusy(_actorId);

        public bool IsOnCooldown => _orchestrator != null && _orchestrator.IsActorOnCooldown(_actorId);

	public void EnqueueRootIntent(ISkillMechanism mech, ISkillParam param, TargetRequest request, int priorityLevel = 0)
	{
		if (mech == null || param == null)
		{
			Debug.LogWarning("EnqueueRootIntent: 메커니즘 또는 파라미터가 null입니다.");
			return;
		}
		_orchestrator ??= IntentOrchestrator.Instance;
		if (_orchestrator == null)
		{
			Debug.LogError("Orchestrator가 없어 Intent를 큐잉할 수 없습니다.");
			return;
		}

		if (!mech.ParamType.IsInstanceOfType(param))
		{
			Debug.LogError($"ParamType mismatch: {mech.ParamType.Name} 필요, {param.GetType().Name} 제공");
			return;
		}

                var intent = CastIntent.Root(
                        _actorId,
                        ++_rootSequence,
                        mech,
                        param,
                        request,
                        respectBusyCooldown,
                        priorityLevel == 0 ? defaultPriority : priorityLevel,
                        transform,
                        boundCamera,
                        BattleCore.Ticker.TickCount); // Intent 생성 시점의 틱 값을 함께 보관합니다.

		// Guard/Dedup 기본값 설정. 실제 프로젝트에서는 Skill 고유 키로 치환 필요.
                intent.DedupKey ??= $"root:{_actorId.Value}:{intent.RootCastId}";
                intent.GuardKey ??= $"guard:{_actorId.Value}:{intent.RootCastId}";

		_orchestrator.Enqueue(intent);
		if (verbose)
		{
			Debug.Log($"[Runner] Root Intent enqueue: {intent}");
		}
	}
}*/