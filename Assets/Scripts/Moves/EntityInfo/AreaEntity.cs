using System.Collections;
using System.Collections.Generic;
using SkillInterfaces;
using UnityEngine;

public class AreaEntity : MonoBehaviour
{
    public ushort lifeTick;
    private ushort _tickElapsed;
    public IAreaShapes areaShape;
    public DamageData damage;
    public List<MechanismRef> onInterval;
    public List<MechanismRef> onExpire;
    public byte activateTick = 15;
    public byte intervalActivated;
    public FixedVector2 location;
    public Transform originalCaster;
    public void Init(IAreaShapes area, DamageData dmg, List<MechanismRef> interval, List<MechanismRef> expire, Transform caster, ushort life = 0)
    {
        areaShape = area;
        damage = dmg;
        onInterval = interval;
        onExpire = expire;
        lifeTick = life;
        location = new FixedVector2(transform.position);
        originalCaster = caster;
        ActivateInterval();
        if (lifeTick < activateTick)
        {
            Debug.Log("lifetime is under 0.25s; instant activation");
            StartCoroutine(Expire());
        }
        else
        {
            Ticker.Instance.OnTick += TickHandler;
        }
    }
    void OnDisable()
    {
        StartCoroutine(Expire());
    }
    void TickHandler(ushort tick)
    {
        lifeTick--; _tickElapsed++;
        if (lifeTick == 0)
        {
            StartCoroutine(Expire());
        }
        else if(_tickElapsed >= activateTick)
        {
            _tickElapsed = 0;
            ActivateInterval();
        }
    }

    public void ActivateInterval()
    {
        if (areaShape is CircleArea circle)
        {
            Vector2 worldCenter = circle.CenterCoordinate.AsVector2;
            float radius = circle.Radius / 1000f;
            // 물리 감지 (Player/Enemy 등 대상 레이어 필터 적용 가능)
            var results = Physics2D.OverlapCircleAll(worldCenter, radius, LayerMask.GetMask("Foe"));
            foreach (var col in results)
            {
                var entity = col.GetComponent<Entity>();
                if (entity is null) continue;

                // OnHit FollowUp 실행
                foreach (var followup in onInterval)
                {
                    if (followup.mechanism is not INewMechanism mech) continue;
                    SkillCommand cmd = new(originalCaster, TargetMode.TowardsEntity,
                        location, mech, followup.@params, damage, entity.transform);
                    CommandCollector.Instance.EnqueueCommand(cmd);
                }
            }
            intervalActivated++;
        }
        else if (areaShape is BoxArea box)
        {
            Vector2 center = box.CenterCoordinate.AsVector2;
            Vector2 size = new(box.Height / 1000f, box.Width / 1000f);

            var results = Physics2D.OverlapBoxAll(center, size, 0f, LayerMask.GetMask("Foe"));
            foreach (var col in results)
            {
                var entity = col.GetComponent<Entity>();
                if (entity is null) continue;

                foreach (var followup in onInterval)
                {
                    if (followup.mechanism is not INewMechanism mech) continue;

                    SkillCommand cmd = new(originalCaster, TargetMode.TowardsEntity,
                        location, mech, followup.@params, damage, entity.transform);
                    CommandCollector.Instance.EnqueueCommand(cmd);
                }
            }
            intervalActivated++;
        }
    }
    public IEnumerator Expire()
    {
        Ticker.Instance.OnTick -= TickHandler;
        Debug.Log($"{intervalActivated} times of activation");
        foreach (var followup in onExpire)
        {
            if (followup.mechanism is not INewMechanism mech) continue;

            SkillCommand cmd = new(originalCaster, TargetMode.TowardsEntity,
                location, mech, followup.@params, damage);
            CommandCollector.Instance.EnqueueCommand(cmd);
        }
        yield return null;
        Destroy(gameObject);
    }
    /*private void OnDrawGizmos()
    {
        if (areaShape == null) return;

        Gizmos.color = Color.cyan;

        // 원형
        if (areaShape is CircleArea circle)
        {
            var pos = areaShape.CenterCoordinate.AsVector2;
            Gizmos.DrawWireSphere(pos, circle.Radius / 1000f);
        }
        // 박스형
        else if (areaShape is BoxArea box)
        {
            var pos = areaShape.CenterCoordinate.AsVector2;
            var size = new Vector3(box.Height / 1000f, box.Width / 1000f, 0f);
            Gizmos.DrawWireCube(pos, size);
        }
    }*/
}