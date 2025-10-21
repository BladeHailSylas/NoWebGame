using System;
using UnityEngine;

namespace Intents
{
    /// <summary>
    ///     Deterministic dispatcher that forwards intents to the correct gameplay subsystem.
    ///     It intentionally keeps the logic minimal so the core loop remains predictable.
    /// </summary>
    public sealed class IntentRouter
    {
        private readonly CoreMotor _coreMotor;
        //private readonly SkillRunner _skillRunner;
        private readonly IntentValidator _validator;
        public static IntentRouter Instance { get; private set; }
        public IntentRouter(CoreMotor coreMotor/*, SkillRunner skillRunner*/)
        {
            _coreMotor = coreMotor ?? throw new ArgumentNullException(nameof(coreMotor));
            //_skillRunner = skillRunner ?? throw new ArgumentNullException(nameof(skillRunner));
            _validator = BattleCore.Validator;
            Instance ??= this;
        }
        public void TickHandler(ushort tick)
        {
            RouteIntent(_validator.ValidatedIntents);
        }
        /// <summary>
        ///     Routes every supplied intent to the subsystem responsible for handling it.
        ///     Null entries are ignored but reported so upstream collectors can be verified.
        /// </summary>
        /// <param name="intents">Batch of intents captured during the current tick.</param>
        public void RouteIntent(IIntent[] intents)
        {
            if (intents == null || intents.Length == 0)
            {
                return;
            }

            for (int i = 0; i < intents.Length; i++)
            {
                var intent = intents[i];
                if (intent == null)
                {
                    Debug.LogWarning($"[IntentRouter] Null intent at index {i}; skipping entry.");
                    continue;
                }

                try
                {
                    switch (intent.Type)
                    {
                        case IntentType.Move:
                            RouteMoveIntent(intent);
                            break;
                        case IntentType.Cast:
                            RouteCastIntent(intent);
                            break;
                        case IntentType.None:
                            throw new InvalidOperationException("Cannot route intents marked as None.");
                        default:
                            throw new InvalidOperationException($"Unknown intent type: {intent.Type}.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[IntentRouter] Failed to route intent {intent.IntentID}: {ex.Message}");
                    // Consider promoting this to a structured logger once diagnostics tooling is ready. 
                }
            }
        }

        private void RouteMoveIntent(IIntent intent)
        {
            if (intent is not MoveIntent moveIntent)
            {
                throw new InvalidCastException("Intent type Move must be a MoveIntent instance.");
            }

            _coreMotor.Move(moveIntent.Movement, moveIntent.MoverID);
        }

        private void RouteCastIntent(IIntent intent)
        {
            if (intent is not CastIntent castIntent)
            {
                throw new InvalidCastException("Intent type Cast must be a CastIntent instance.");
            }

            //_skillRunner.Cast(castIntent);
        }
    }
}
