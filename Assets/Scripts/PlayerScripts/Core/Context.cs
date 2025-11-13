using UnityEngine;

/// <summary>
/// Shared dependency container for all player modules. It bundles Unity
/// specific references and exposes a centralised logger.
/// </summary>
public sealed class Context
{
    public PlayerEntity Owner { get; }
    public GameObject GameObject { get; }
    public Transform Transform { get; }
    public TargetResolver TargetResolver { get; }
    public CommandCollector CommandCollector { get; }
    public CharacterSpec Spec { get; }
    public ILogger Logger { get; }

    public StatsBridge Stats { get; private set; }
    public ActBridge Act { get; private set; }
    
    public StackManager StackManager { get; private set; }

    public Context(PlayerEntity owner, GameObject gameObject, Transform transform, TargetResolver resolver, CommandCollector collector, CharacterSpec spec, ILogger logger)
    {
        Owner = owner;
        GameObject = gameObject;
        Transform = transform;
        TargetResolver = resolver;
        CommandCollector = collector;
        Spec = spec;
        Logger = logger;
    }

    public void RegisterStats(StatsBridge stats)
    {
        Stats = stats;
    }

    public void RegisterAct(ActBridge bridge)
    {
        Act = bridge;
    }

    public void RegisterStackManager(StackManager stackManager)
    {
        StackManager = stackManager;
    }
}

/// <summary>
/// Logger abstraction that can later be replaced with in-game consoles or
/// analytics hooks.
/// </summary>
public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

public sealed class Logger : ILogger
{
    private readonly string _prefix;

    public Logger(string prefix)
    {
        _prefix = prefix;
    }

    public void Info(string message)
    {
        Debug.Log(Format("INFO", message));
    }

    public void Warn(string message)
    {
        Debug.LogWarning(Format("WARN", message));
    }

    public void Error(string message)
    {
        Debug.LogError(Format("ERROR", message));
    }

    private string Format(string level, string message) => $"[Player:{_prefix}][{level}] {message}";
}
