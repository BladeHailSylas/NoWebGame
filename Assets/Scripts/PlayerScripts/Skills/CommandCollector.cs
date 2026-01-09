using System.Collections.Generic;
using Moves;
using UnityEngine;
using Time = Systems.Time.Time;

namespace PlayerScripts.Skills
{
    public class CommandCollector : MonoBehaviour
    {
        private SkillRunner _runner;
        private ushort _currentTicks;
        private List<SkillCommand> _current = new();
        private List<SkillCommand> _next = new();
        public static CommandCollector Instance { get; private set; }

        private void OnEnable()
        {
            _runner = new SkillRunner(GetComponent<TargetResolver>());
            Time.Ticker.OnTick += TickHandler;
            Instance = this;
        }

        private void OnDisable()
        {
            Time.Ticker.OnTick -= TickHandler;
        }
    
        public void EnqueueCommand(SkillCommand cmd) => _next.Add(cmd);
    
        private void TickHandler(ushort tick)
        {
            if (_current.Count > 0)
            {
                foreach (var cmd in _current)
                {
                    _runner.Activate(cmd);
                }
                _current.Clear();
            }

            // swap for next tick
            (_current, _next) = (_next, _current);
        }
        public void CeaseCommand() {
            //Later
        }
    }
}