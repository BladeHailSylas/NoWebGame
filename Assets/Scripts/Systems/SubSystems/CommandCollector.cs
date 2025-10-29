using System.Collections.Generic;
using UnityEngine;
public class CommandCollector : MonoBehaviour
{
    private SkillRunner _runner;
    private ushort _currentTicks;
    private List<SkillCommand> _current = new();
    private List<SkillCommand> _next = new();
    public static CommandCollector Instance { get; private set; }
    void OnEnable()
    {
        //Debug.Log("Ready to collect garbage");
        _runner = new SkillRunner(GetComponent<TargetResolver>());
        Ticker.Instance.OnTick += TickHandler;
        Instance = this;
    }

    void OnDisable()
    {
        Ticker.Instance.OnTick -= TickHandler;
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
}