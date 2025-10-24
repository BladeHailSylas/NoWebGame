using UnityEngine;
using SkillInterfaces;
using ActInterfaces;
public interface ITempMechanism
{
    //public System.Type ParamType { get; }
    public void Execute(in SkillCommand cmd, int chained = 0);
}
public class HitscanSkillMechanism : ITempMechanism
{
    public void Execute(in SkillCommand cmd, int chained = 0)
    {
        if (cmd.Caster == null)
        {
            Debug.LogWarning("[HitscanSkill] Caster가 없습니다.");
            return;
        }

        if (cmd.Params is not HitscanParams p)
        {
            Debug.LogWarning("[HitscanSkill] 잘못된 Params 타입입니다.");
            return;
        }

        Vector2 origin = cmd.Caster.position;
        Vector2 direction;

        // Target 또는 Anchor로 방향 결정
        if (cmd.Target != null)
            direction = ((Vector2)cmd.Target.position - origin).normalized;
        else direction = Vector2.right;
        //else direction = (cmd.Anchor.asVector2 - origin).normalized;

        // Raycast 수행
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, p.maxRange, p.targetMask);
        if (p.debugDraw)
        {
            Debug.DrawRay(origin, direction * p.maxRange,
                hit ? Color.green : Color.red, 0.2f);
        }

        if (!hit)
        {
            Debug.Log("[HitscanSkill] 적중 없음.");
            return;
        }

        // 대상 처리
        var vulnerable = hit.collider.GetComponent<IVulnerable>();
        if (vulnerable != null)
        {
            vulnerable.TakeDamage(p.damage);
            Debug.Log($"[HitscanSkill] {hit.collider.name}에게 {p.damage} 데미지!");
        }
        else
        {
            Debug.Log($"[HitscanSkill] {hit.collider.name} 피격됨 (IDamageable 아님)");
        }
    }
}

[System.Serializable]
public class HitscanParams : ISkillParams
{
    public float maxRange = 6f;
    public float minRange = 0f;
    public int damage = 1000;
    public LayerMask targetMask;

    [Header("기타 옵션")]
    [Tooltip("디버그용 Ray 시각화 표시 여부")]
    public bool debugDraw = true;

    public HitscanParams(float MRange, int d)
    {
        this.maxRange = MRange;
        this.damage = d;
        targetMask = LayerMask.GetMask("Foe");
    }

    public HitscanParams(float mRange, float MRange, int d)
    {
        this.minRange = MRange;
        this.maxRange = mRange;
        this.damage = d;
        targetMask = LayerMask.GetMask("Foe");
    }
}