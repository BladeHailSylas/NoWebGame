// EventPayloads.cs
// 목적: 타입 기반 EventBus<T>에 맞춘 "가벼운 값 타입 페이로드" 정의 모음
// 사용 예:
//   EventBus.Subscribe<DamageDealt>(OnDamage); // 구독
//   EventBus.Publish(new DamageDealt(meta, attacker, target, raw, final, EDamageType.Normal, isCrit, hitPt, hitNrm)); // 발행
//
// 설계 원칙:
// 1) 값 타입(readonly struct)으로 유지해 가비지 최소화.
// 2) "요청(Request)"과 "사실 알림(Event)"을 명확히 구분.
// 3) Unity 수명 고려: Transform/Object 참조는 연출/편의 목적에 한정하고, 필요 시 Id/Meta를 병행 제공.
// 4) 타입이 곧 채널: EventBus<T> 키는 T 타입이므로, 구독/발행의 결합은 타입 시그니처로 관리.
// 알 수 없는 것:
// 플레이어 캐릭터 선택 시에도 event를 사용해야 하는지 모르겠음
// 스테이터스 
#nullable enable
using SkillInterfaces;
using UnityEngine;

#region ===== 공통 메타데이터 =====

/// <summary>
/// 모든 게임 이벤트에 공통으로 포함될 수 있는 메타 정보.
/// - EventId: 전역 증가 시퀀스(로그/트레이싱 용이)
/// - Time: 이벤트 발생 시각(Time.time 스냅샷)
/// - Source: 발신자(주로 Transform 루트, null 지양)
/// - Channel: 논리 채널("combat", "buff", "ui" 등) - 필터/디버그 용
/// - CorrelationId: 시작~중간~종료 같은 흐름을 묶을 상관 식별자
/// </summary>
public readonly struct GameEventMeta
{
	public readonly int EventId;
	public readonly float Time;
	public readonly Transform Source;
	public readonly string? Channel;
	public readonly int CorrelationId;

	public GameEventMeta(int eventId, float time, Transform source, string? channel, int correlationId)
	{
		EventId = eventId;
		Time = time;
		Source = source;
		Channel = channel;
		CorrelationId = correlationId;
	}

	public override string ToString()
		=> $"#{EventId} t={Time:F3} src={(Source ? Source.name : null)} ch={Channel ?? null} corr={CorrelationId}";
}

/// <summary>
/// GameEventMeta 생성을 표준화하는 간단 팩토리.
/// - 일관된 증가 시퀀스/시간 스냅샷을 부여.
/// - <see cref="Create"/> 하나만 쓰면 메타가 균일해집니다.
/// </summary>
public static class GameEventMetaFactory
{
	private static int _seq;

	/// <summary>표준 메타 생성. channel/correlationId는 필요 시에만 지정.</summary>
	public static GameEventMeta Create(Transform source, string? channel = null, int correlationId = 0)
		=> new(++_seq, Time.time, source, channel, correlationId);
}

#endregion

#region ===== 공통: 스킬 참조 =====

/// <summary>
/// 스킬 참조를 위한 경량 구조.
/// - Asset: ScriptableObject(메카닉/스킬 데이터) 참조. 없을 수 있음.
/// - Id: 숫자형 식별자(네트워킹/세이브/표준화 용).
/// 둘 중 하나만 써도 되고, 둘 다 쓸 수도 있습니다.
/// </summary>
public readonly struct SkillRef
{
	public readonly Object? Asset; // ScriptableObject(또는 기타 UnityEngine.Object)
	public readonly int Id;                    // 외부 시스템과의 호환을 위한 정수형 키(0이면 미사용으로 간주)

	public bool HasAsset => Asset != null;
	public bool HasId => Id != 0;

	public SkillRef(Object? asset, int id = 0)
	{ Asset = asset; Id = id; }

	public override string ToString()
		=> HasAsset ? $"{Asset!.name}(#{Id})" : $"SkillId#{Id}";
}

#endregion

#region ===== 시전(캐스팅) 흐름: 사실 알림(Event) =====

/// <summary>
/// [Event] 시전 시작 알림.
/// - EventBus.Subscribe&lt;CastStarted&gt;(...) 로 구독.
/// - 발행자는 "사실"만 알리고, UI/VFX/사운드는 이를 구독해 반응합니다.
/// </summary>
public readonly struct CastStarted
{
	public readonly GameEventMeta Meta;
	public readonly SkillRef Skill;       // 강타입 참조(또는 Id)
	public readonly ISkillParam Param;    // 시전 시점의 파라미터 스냅샷(읽기 전용 계약)
	public readonly Transform Caster;     // 편의상 별도 보강
	public readonly Transform? TargetOpt; // 대상형 스킬일 때만 존재(없으면 null)

	public CastStarted(GameEventMeta meta, SkillRef skill, ISkillParam param, Transform caster, Transform? targetOpt)
	{ Meta = meta; Skill = skill; Param = param; Caster = caster; TargetOpt = targetOpt; }

	public override string ToString() => $"CastStarted[{Skill}] by {Caster.name}";
}

/// <summary>
/// [Event] 시전 종료 알림.
/// - Interrupted: 취소/피격/CC 등에 의한 중단 여부.
/// </summary>
public readonly struct CastEnded
{
	public readonly GameEventMeta Meta;
	public readonly SkillRef Skill;
	public readonly Transform Caster;
	public readonly bool Interrupted;

	public CastEnded(GameEventMeta meta, SkillRef skill, Transform caster, bool interrupted)
	{ Meta = meta; Skill = skill; Caster = caster; Interrupted = interrupted; }

	public override string ToString() => $"CastEnded[{Skill}] by {Caster.name}, interrupted={Interrupted}";
}

/// <summary>
/// [Event] 타깃 확정 알림, 
/// </summary>
public readonly struct TargetAcquired
{
	public readonly GameEventMeta Meta;
	public readonly SkillRef Skill;
	public readonly Transform Caster;
	public readonly Transform Target;
	public TargetAcquired(GameEventMeta meta, SkillRef skill, Transform caster, Transform target)
	{
		Meta = meta; Skill = skill; Caster = caster; Target = target;
	}
	public override string ToString() => $"TargetAcquired[{Target.name}] by {Caster.name} to activate {Skill}";
}
public readonly struct TargetNotFound
{
	public readonly GameEventMeta Meta;
	public readonly SkillRef Skill;
	public readonly Transform Caster;
	public TargetNotFound(GameEventMeta meta, SkillRef skill, Transform caster)
	{
		Meta = meta; Skill = skill; Caster = caster;
	}
	public override string ToString() => $"TargetNotFound by {Caster.name} to activate {Skill}";
}

#endregion

#region ===== 전투: 사실 알림(Event) =====

/// <summary>
/// [Event] 피해가 "적용된" 사실 알림(로그/연출/UI용).
/// - RawDamage: 기본 피해량
/// - FinalDamage: 실제 가하는 피해량(피해량 증감의 효과를 받는)
/// - ArmorPenetration: 총 방어 관통 계수(곱연산, <see cref="PlayerStatsBridge.TotalArmorPenetration()"/> 참조)
/// - DamageType(<see cref="EDamageType"/>): 일반(Normal)/비례(Percentaged)/고정(Fixed)
/// - HitPoint/HitNormal: 연출/넉백 방향 계산 근거
/// </summary>
public readonly struct DamageDealt
{
	public readonly GameEventMeta Meta;
	public readonly Transform Attacker;
	public readonly Transform Target;
	public readonly float RawDamage;
	public readonly float FinalDamage;
	public readonly float ArmorPenetration;
	public readonly EDamageType DamageType;
	public readonly Vector2 HitPoint;
	public readonly Vector2 HitNormal;

	public DamageDealt(
		GameEventMeta meta, Transform attacker, Transform target,
		float rawDamage, float finalDamage, float armorPenetration, EDamageType damageType,
		Vector2 hitPoint, Vector2 hitNormal)
	{
		Meta = meta;
		Attacker = attacker;
		Target = target;
		RawDamage = rawDamage;
		FinalDamage = finalDamage;
		ArmorPenetration = armorPenetration;
		DamageType = damageType;
		HitPoint = hitPoint;
		HitNormal = hitNormal;
	}

	public override string ToString()
		=> $"DamageDealt {FinalDamage} ({DamageType}) {Attacker.name}→{Target.name}";
}

/// <summary>피해 유형: 일반 피해 Normal, 비례 피해 Percentaged(최대 체력의 1% 등), 고정 피해 Fixed(피해량 변동이 없음, 그것이 고정이니까 음)</summary>
public enum EDamageType { Normal, Percentaged, Fixed }

#endregion

#region ===== 버프/이펙트: 요청(Request) & 제거(Remove) =====
// EventBus에서 "요청" 페이로드는 시스템(버프 매니저 등)에게 동작을 의뢰합니다.
// - ApplyReq: 적용 요청(성공/실패 여부는 별도 응답 이벤트로 설계해도 됨)
// - RemoveReq: 제거 요청(제거는 PlayerEffects에서 따로 수행해도 될지 모르겠음)
// ※ 요청을 "사실 알림"과 섞지 말고 분리하면 흐름 추적이 쉽습니다.

/// <summary>
/// [Request] 버프(스탯) 적용 요청.
/// - Target: 누구에게 적용?
/// - Mod: 어떤 스탯 변경?
/// - Duration: float.PositiveInfinity일 경우에만 무한(음수는 버그의 우려가 있음)
/// - Tag: 중복 정책을 위한 태그(옵션; 동일 태그는 갱신/대체 등 정책에 활용)
/// </summary>
public readonly struct BuffApplyReq
{
	public readonly GameEventMeta Meta;
	public readonly Transform Target;
	public readonly IStatModifier Mod;
	public readonly float Duration;
	public readonly string? Tag;

	public BuffApplyReq(GameEventMeta meta, Transform target, IStatModifier mod, float duration, string? tag = null)
	{ Meta = meta; Target = target; Mod = mod; Duration = duration; Tag = tag; }

	public override string ToString()
		=> $"BuffApplyReq {Mod?.GetType().Name} to {Target.name}, dur={Duration}, tag={Tag}";
}

/// <summary>
/// [Request] 이펙트(비주얼/오디오/상태) 적용 요청.
/// 버프와 동일한 정책으로 동작하지만, 시각/오디오/상태 연출 쪽에 치우친 모디파이어.
/// </summary>
public readonly struct EffectApplyReq
{
	public readonly GameEventMeta Meta;
	public readonly Transform Target;
	public readonly IEffectModifier Mod;
	public readonly float Duration;
	public readonly string? Tag;

	public EffectApplyReq(GameEventMeta meta, Transform target, IEffectModifier mod, float duration, string? tag = null)
	{ Meta = meta; Target = target; Mod = mod; Duration = duration; Tag = tag; }

	public override string ToString()
		=> $"EffectApplyReq {Mod?.GetType().Name} to {Target.name}, dur={Duration}, tag={Tag}";
}

/// <summary>
/// [Request] 버프 제거 요청.
/// 특정 Mod 인스턴스를 제거하는 전형적인 형태.
/// 정책에 따라 Tag 기반 대상을 제거하는 별도 요청을 추가할 수도 있습니다.
/// </summary>
public readonly struct BuffRemoveReq
{
	public readonly GameEventMeta Meta;
	public readonly Transform Target;
	public readonly IStatModifier Mod;

	public BuffRemoveReq(GameEventMeta meta, Transform target, IStatModifier mod)
	{ Meta = meta; Target = target; Mod = mod; }

	public override string ToString()
		=> $"BuffRemoveReq {Mod?.GetType().Name} from {Target.name}";
}

/// <summary>
/// [Request] 이펙트 제거 요청.
/// </summary>
public readonly struct EffectRemoveReq
{
	public readonly GameEventMeta Meta;
	public readonly Transform Target;
	public readonly IEffectModifier Mod;

	public EffectRemoveReq(GameEventMeta meta, Transform target, IEffectModifier mod)
	{ Meta = meta; Target = target; Mod = mod; }

	public override string ToString()
		=> $"EffectRemoveReq {Mod?.GetType().Name} from {Target.name}";
}

#endregion

#region ===== 시스템 =====

public struct OrchestratorAwake
{
	public readonly GameEventMeta Meta;
}

public struct OnTick
{
	public readonly GameEventMeta Meta;
}

#endregion

#region ===== 외부 도메인 계약(인터페이스) =====

// 외부 도메인 계약(스탯/이펙트 적용 제거).
// - 이벤트 페이로드는 이 계약에만 의존하고, 구체 구현은 시스템 내부에 숨깁니다.
// - 테스트 더블/모킹이 쉬워집니다.
public interface IStatModifier
{
	void Apply(PlayerStats s);
	void Remove(PlayerStats s);
}

public interface IEffectModifier
{
	void Apply(PlayerEffects fx);
	void Remove(PlayerEffects fx);
}

#endregion
