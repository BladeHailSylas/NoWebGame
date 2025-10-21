using SkillInterfaces;
using UnityEngine;
public class PlayerAttackController
{
    public void TryCast(SkillSlot slot)
    {
        Debug.Log($"Temporarily disabled. You pressed: {slot}");
    }
    public int SkillPriority(ISkillMechanism mech, ISkillParam param, SkillSlot slot)
    {
        return SkillPriority(mech, param as ICooldownParam, slot);
    }
    public int SkillPriority(ISkillMechanism mech, ICooldownParam param, SkillSlot slot)
    {
        if (mech == null || param == null)
        {
            Debug.LogWarning("SkillPriority: 메커니즘 또는 파라미터가 null입니다. Priority level이 임시로 0이 됩니다.");
            return 0;
        }
        if (!mech.ParamType.IsInstanceOfType(param))
        {
            Debug.LogError($"ParamType mismatch: {mech.ParamType.Name} 필요, {param.GetType().Name} 제공. Priority level이 임시로 -1이 됩니다.");
            return -1;
        }
        int weight = 0;
        weight += mech.ParamType.Name switch
        {
            "MeleeParams" or "MissileParams" or "HitscanParams" or "AreaParams" => 3,
            "DashParams" or "TeleportParams" => 2,
            _ => 1,
        };
        weight += slot switch
        {
            SkillSlot.Attack => 1,
            SkillSlot.AttackSkill or SkillSlot.Skill1 or SkillSlot.Skill2 => 2,
            SkillSlot.Ultimate => 3,
            _ => 0,
        };
        //Debug.Log($"[Runner] SkillPriority: {slot} 슬롯의 {mech.ParamType.Name} 타입은 {weight * 1000} priority입니다");
        return weight * 1000 + (int)param.Cooldown;
    }
}