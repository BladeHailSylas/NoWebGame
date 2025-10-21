using ActInterfaces;
using EffectInterfaces;
using StatsInterfaces;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
public class PlayerEffects : MonoBehaviour, IAffectable, IEffectStats
{
    public bool IsImmune { get; private set; } = false;
    public bool IsMovable { get; private set; } = true;
	public bool IsAttackable { get; private set; } = true;
	public float EffectResistance { get; private set; } = 0f; // 효과 저항력 (0% 기본)
    public Dictionary<Effects, EffectState> EffectList { get; private set; } = new();
	public List<EffectState> StackList { get; private set; } = new();
    public HashSet<Effects> PositiveEffects { get; private set; } = new() { Effects.Haste, Effects.DamageBoost, Effects.ArmorBoost, Effects.APBoost, Effects.DrBoost, Effects.Invisibility, Effects.Invincible};
    public HashSet<Effects> NegativeEffects { get; private set; } = new() { Effects.Slow, Effects.Stun, Effects.Suppressed, Effects.Root, Effects.Tumbled, Effects.Damage };
    public HashSet<Effects> DisturbEffects { get; private set; } = new() { Effects.Slow, Effects.Stun, Effects.Suppressed, Effects.Root, Effects.Tumbled }; //방해 효과, Damage는 엄연한 공격 효과이므로 EffectResistance의 영향을 받지 않음, CC ⊂ 방해
    public HashSet<Effects> CcEffects { get; private set; } = new() { Effects.Stun, Effects.Suppressed, Effects.Root, Effects.Tumbled };
    public bool HasEffect(Effects e) => EffectList.ContainsKey(e); //CC 확인에 필요함
	public void ApplyStack(string name, int amp = 1, GameObject go = null) //내가 내 스택을 쌓으려면 ApplyStack을 직접 호출, 내가 남의 스택을 쌓는 경우 ApplyEffect로 전달해야 한다
	{
		if (amp == 0) amp = 1;
		if (go == null) go = gameObject;
		foreach (var stack in StackList)
		{
			if (stack.effectName == name)
			{
				stack.amplifier += amp;
				return;
			}
		}
		StackList.Add(new EffectState(name, amp, go));
	}
	public void ApplyEffect(Effects buffType, GameObject effecter, float duration = float.PositiveInfinity, int amp = 0, string name = null)
	{
		if (buffType == Effects.Stack)
		{
			ApplyStack(name, amp, effecter);
		}
		else if (EffectList.ContainsKey(buffType))
		{
			EffectList[buffType].duration = Mathf.Max(EffectList[buffType].duration, duration);
			EffectList[buffType].amplifier += amp;
		}
		else
		{
			EffectList.Add(buffType, new EffectState(duration, amp, effecter));
			StartCoroutine(CoEffectDuration(buffType, duration));
		}
		Affection(buffType, duration, amp); // FixedUpdate()와 호출이 중복되어 2번 적용되는 것에 유의, 추후 이펙트 구현할 때 핸들링 필요
											// FixedUpdate()는 Verify 및 reload 용도로 사용하고 싶으므로 구현할 때 고려해야 함
	}
	public void Affection(Effects buffType, float duration, float amplifier = 0) // Amplifier는 효과의 강도, 예를 들어 Haste/Slow면 이동속도 변동성, Damage면 초당 피해량 -> 참고로, Damage의 Amplifier는 상대가 가하는 원래 피해량을 받으므로 여기서 DamageResistance를 계산해야 함
    {
        Debug.Log($"Effect {buffType.GetType()} has affected for {duration} with amp {amplifier}"); //Effect 적용할 때 Tumbled 이외의 DisturbEffects는 EffectResistance 적용할 것
        if(CcEffects.Contains(buffType))
        {
            IsMovable = false;
        }
    }
    public IEnumerator CoEffectDuration(Effects e, float duration) //Effect 지속시간 관리
    {
        while (duration > 0f)
        {
            duration -= Time.deltaTime;
            EffectList[e].duration = duration;
            yield return null;
        }
        EffectList.Remove(e);
    }
    public void Purify(Effects buffType) // 정화, Cleanse가 모든 버프를 제거할 수 있는지 생각할 필요가 있음
                                                // 물론 Cleanse는 통념적으로 긍정적 효과를 제거하지 않는 것으로 인식되므로 제거하지 않는 게 나을 것
                                                // 한편 넘어짐(Tumbled)은 가장 강력한 CC 효과로 설계했기에 정화로 제거하지 않아야 하는지 고려 필요
    {
        if (EffectList.ContainsKey(buffType))
        {
            EffectList.Remove(buffType);
        }
    }
    public void ClearNegative() // 방해 효과 전체 제거: 지속 피해 효과는 제거하지 않는 설계가 좋음(지딜을 살), Cleanse가 호출하는 메서드
                                // 방해 효과의 정의를 "종료/해제될 때까지 제대로 행동할 수 없게 만드는 효과" 로 정의하면 됨, 지속 피해는 받든 어쩌든 행동이 되니까
                                // "지속 피해로 죽으면 제대로 행동할 수 없잖아요!" << 지딜이 종료/해제된 후에도 제대로 행동할 수 없는 거니까 모순 없음
    {
        foreach (var effect in EffectList.Keys)
        {
            if (DisturbEffects.Contains(effect) && EffectList[effect].duration != float.PositiveInfinity)
            {
                EffectList.Remove(effect);
            }
        }
    }
	/*void OnEnable()
	{
		BattleCore.Ticker.OnTick += TickHandler;
	}
	void OnDisable()
	{
		BattleCore.Ticker.OnTick -= TickHandler;
	}
	public void TickHandler(ushort tick)
	{
		return;
	}*/
	/*void OnEnable()
{
   EventBus.Subscribe<BuffApplyReq>(OnBuffApply);
   EventBus.Subscribe<BuffRemoveReq>(OnBuffRemove);
}
void OnDisable()
{
   EventBus.Unsubscribe<BuffApplyReq>(OnBuffApply);
   EventBus.Unsubscribe<BuffRemoveReq>(OnBuffRemove);
}

void OnBuffApply(BuffApplyReq e)
{
   if (e.Target != transform) return;
   e.Mod.Apply(this);
   if (e.Duration > 0) StartCoroutine(CoExpire(e.Mod, e.Duration));
}
void OnBuffRemove(BuffRemoveReq e)
{
   if (e.Target != transform) return;
   e.Mod.Remove(this);
}
IEnumerator CoExpire(IEffectModifier mod, float dur)
{
   yield return new WaitForSeconds(dur);
   mod.Remove(this);
}*/
}