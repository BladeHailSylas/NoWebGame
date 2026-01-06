using Moves;

namespace PlayerScripts.Skills
{
    public class SkillRunner
    {
        private TargetResolver _targetResolver;
        public SkillRunner(TargetResolver resolver)
        {
            _targetResolver = resolver;
        }

        public void Activate(in SkillCommand cmd)
        {
            //Enforce maximum chain depth
            //Determine target
            var target = cmd.Target;
            var anchor = cmd.CastPosition;

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
                anchor = result.Anchor;
            }

            //Debug.Log($"Let's apply damage {cmd.Damage.Attack}");
            //Execute skill mechanism
            //Debug.Log($"Variable은 {cmd.Var.Variable?.displayName}");
            cmd.Mech.Execute(new CastContext(cmd.Params, cmd.Caster, target,
                cmd.Damage, cmd.Var, cmd.TargetMode));
        
        }
    }
}