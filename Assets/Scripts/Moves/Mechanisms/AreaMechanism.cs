using System;
using System.Collections.Generic;
using Moves.Effects.Definitions;
using Moves.ObjectEntity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Moves.Mechanisms
{
    [Serializable]
    public class AreaMechanism : NewMechanism, INewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Settings")] 
        public AreaEntity areaPrefab;
        [SerializeReference] public List<MechanismData> onEnter;
        [SerializeReference] public List<MechanismData> onExpire;
        public AreaEffectEntity effectPrefab;
        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not AreaMechanism mech) return;
            var prefab = mech.areaPrefab;

            // Target이 유효한 위치를 제공하면 해당 위치에 생성합니다.
            // 그렇지 않으면 Caster 위치를 사용합니다.
            var centerPos = ctx.Caster.position;
            bool hasTargetPosition = ctx.Target.TryGetPosition(out var targetPos);

            if (hasTargetPosition)
            {
                centerPos = targetPos;
            }

            // Box 회전에만 최초 시전 원점을 사용합니다. 구형 직접 호출의 null은 현재 위치로 보완합니다.
            // 생성 위치와 Laser/Circle의 기존 기준은 변경하지 않습니다.
            var rotationOrigin = prefab.areaShape is BoxArea
                ? ctx.OriginCastPosition ?? (Vector2)ctx.Caster.position
                : (Vector2)ctx.Caster.position;
            Vector3 dir = hasTargetPosition
                ? (Vector3)(targetPos - rotationOrigin)
                : ctx.Caster.right;

            // Target과 Caster가 같은 위치라면 방향 벡터가 0이 되므로
            // Caster의 오른쪽 방향을 대신 사용합니다.
            if (dir.sqrMagnitude < 0.000001f)
            {
                dir = ctx.Caster.right;
            }

            // 기존 구현과 동일한 회전 계산입니다.
            var rotation = Quaternion.LookRotation(
                Vector3.forward,
                dir.normalized
            );

            // 생성 시점에 위치와 회전을 함께 적용합니다.
            var instance = Object.Instantiate(
                prefab,
                centerPos,
                rotation
            );

            instance.Init(ctx);
        }
    }
}
