using System.Collections.Generic;
using UnityEngine;
using SkillInterfaces;

/// <summary>
/// Defines how hitscan-based skills behave — instant ray-based hit detection.
/// </summary>
[CreateAssetMenu(fileName = "HitscanMechanism", menuName = "Skills/Mechanisms/Hitscan")]
public class HitscanMechanism : ScriptableObject, INewMechanism
{
    public void Execute(INewParams @params, Transform caster, Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[HitscanMechanism] No target provided — skipping.");
            return;
        }

        if (@params is not HitscanParams param)
        {
            Debug.LogError("Wrong Parameter");
            return;
        }
        //Transform caster = null;
        //if (@params is SkillCommand sc)
        //    caster = sc.Caster;
        // Fallback: SkillRunner provides caster via SkillCommand; params may evolve later.

        // Validate target layer
        if ((param.layerMask.value & (1 << target.gameObject.layer)) == 0)
        {
            Debug.Log("[HitscanMechanism] Target layer not allowed — skipping.");
            return;
        }

        Vector2 origin = caster != null ? caster.position : Vector2.zero;
        Vector2 direction = ((Vector2)target.position - origin).normalized;
        float distance = Vector2.Distance(origin, target.position);

        if (distance > param.maxRange || distance < param.minRange)
        {
            Debug.Log($"[HitscanMechanism] Target out of range ({distance:F2}) — skipping.");
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, param.maxRange, param.layerMask);
        if (param.debugDraw)
        {
            Color c = hit ? Color.red : Color.yellow;
            Debug.DrawRay(origin, direction * param.maxRange, c, 0.5f);
        }

        if (hit.collider != null)
        {
            //Placeholder: actual damage logic handled externally
            Debug.Log($"[HitscanMechanism] Hit detected on {hit.collider.name}");
            OnHit(hit.collider.transform);
        }

        OnFinished();
    }

    protected virtual void OnHit(Transform hitTarget)
    {
        // Placeholder for EffectSkill or damage pipeline.
        Debug.Log($"[HitscanMechanism] OnHit triggered on {hitTarget.name}");
    }

    protected virtual void OnFinished()
    {
        // Placeholder for completion callbacks or follow-ups.
        Debug.Log("[HitscanMechanism] OnFinished triggered.");
    }
}

[System.Serializable]
public class HitscanParams : INewParams
{
    [Header("Hitscan Settings")]
    public float minRange;
    public float maxRange = 10f;
    public DamageData damage = new DamageData(StatsInterfaces.DamageType.Normal, 1000, 0);
    public LayerMask layerMask = 1 << 8; // Default "Foe" layer
    public GameObject hitEffectPrefab;   // Placeholder — not used yet
    [Header("Ticker")] 
    [SerializeField] private short cooldownTicks;
    public short CooldownTicks => cooldownTicks;
    [Header("FollowUp")] 
    public INewMechanism FollowUp;
    [Header("Debug")]
    public bool debugDraw = true;
}