using Moves;
using Moves.Mechanisms;
using PlayerScripts.Core;

namespace PlayerScripts.Skills
{
    public class SkillRunner
    {
        private readonly TargetResolver _targetResolver;
        public SkillRunner(TargetResolver resolver)
        {
            _targetResolver = resolver;
        }

        public void Activate(in SkillCommand cmd)
        {
            //Enforce maximum chain depth
            //Determine target
            var target = cmd.Target;

            if (target is null)
            {
                var req = new TargetRequest(cmd.Caster, cmd.TargetMode);
                // TODO: Later support range/mask overrides from skill data.

                var result = _targetResolver.ResolveTarget(req);
                if (!result.Found)
                {
                    // No target found — silently return (placeholder behavior)
                    return;
                }

                target = result.Target;
            }
            //Execute skill mechanism
            cmd.Mech.Execute(new CastContext(cmd.Params, cmd.Caster, target,
                cmd.Damage, cmd.Var, cmd.TargetMode));
        
        }
    }
}