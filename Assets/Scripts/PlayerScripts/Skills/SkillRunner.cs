using Moves;
using Moves.Mechanisms;
using UnityEngine;

namespace PlayerScripts.Skills
{
    public class SkillRunner
    {
        private readonly TargetResolver _targetResolver;
        private readonly EffectDisplayer _displayer;
        public SkillRunner(TargetResolver resolver)
        {
            _targetResolver = resolver;
        }

        public SkillRunner(TargetResolver resolver, EffectDisplayer displayer)
        {
            _targetResolver = resolver;
            _displayer = displayer;
        }

        public void Activate(in SkillCommand cmd)
        {
            //Enforce maximum chain depth
            //Determine target
            var target = cmd.Target;
            var mode = TargetMode.TowardsEntity; // default
            if (cmd.Mech is DetectMechanism detect && target is null)
            {
                var result = _targetResolver.Detect(cmd.Caster, detect);
                target = result.Target;
                mode = detect.requiredMode;
            }
            //TODO: Consider refactoring into: single target acquisition / post-acquisition interpretation
            if (target is null)
            {
                var req = new TargetRequest(cmd.Caster, cmd.Mech.MinRange, cmd.Mech.MaxRange, cmd.TargetMode, cmd.Mask);
                //Temporarily Foe, should be added further target request
                // TODO: Later support range/mask overrides from skill data.
                var result = _targetResolver.ResolveTarget(req);
                if (!result.Found)
                {
                    // No target found — silently return (placeholder behavior)
                    return;
                }
                target = result.Target;
                mode = cmd.TargetMode;
            }
            Debug.Log(cmd.Mech.GetType());
            cmd.Mech.Execute(new CastContext(cmd.Mech, cmd.Caster, target, cmd.Damage, cmd.Var, mode));
            _displayer?.Display(cmd);
        }
    }
}