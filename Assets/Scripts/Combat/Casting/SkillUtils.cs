using System.Collections.Generic;
using PlayerScripts.Skills;
using Systems.Data;

namespace Moves
{
    public static class SkillUtils
    {
        public static void ActivateChain(IEnumerable<MechanismData> chains, CastContext ctx, SkillTarget? target = null, string trigger = "Unspecified")
        {
            if (chains == null || !ctx.Caster) { AttackLog.Write(ctx.Trace, "ChainSkipped", $"trigger={trigger} reason={(chains == null ? "NoChildren" : "MissingCaster")}", true); return; }
            var childIndex = -1;
            foreach (var chain in chains)
            {
                childIndex++;
                if (chain?.mechanism is not { } mech) { AttackLog.Write(ctx.Trace, "ChainSkipped", "reason=MissingMechanism"); continue; }
                var next = target ?? ctx.Target;
                if (chain.requireRetarget) next = default;
                else if (chain.targetSource == ChainTargetSource.ExitPosition)
                {
                    if (!ctx.Exit.HasValue) { AttackLog.Write(ctx.Trace, "ChainSkipped", "reason=MissingExitPosition"); continue; }
                    next = SkillTarget.At(ctx.Exit.Value.Position);
                }
                AttackLog.Write(ctx.Trace, "Chain", $"trigger={trigger} child={mech.GetType().Name} retarget={chain.requireRetarget} source={chain.targetSource} target={next}");
                CommandBridge.Enqueue(new SkillCommand(ctx.Caster, chain.mode,
                    new FixedVector2(ctx.Caster.position), mech, ctx.Damage, next, ctx.Var, TargetPolicy.From(mech).IsRelative ? mech.Mask : ctx.Mech.Mask, ctx.Exit,
                    ctx.OriginCastPosition, trace: ctx.Trace.Command(), source: $"{ctx.Source}/{trigger}[{childIndex}]"));
            }
        }
    }

}
