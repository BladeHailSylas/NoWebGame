using UnityEngine;

/// <summary>
/// Shared dependency container for all player modules. It bundles Unity
/// specific references and exposes a centralised logger.
/// </summary>
public sealed class PlayerContext
{
    public PlayerScript Owner { get; }
    public GameObject GameObject { get; }
    public Transform Transform { get; }
    public TargetResolver TargetResolver { get; }
    public CommandCollector CommandCollector { get; }
    public CharacterSpec Spec { get; }
    public IPlayerLogger Logger { get; }

    public PlayerStatsBridge Stats { get; private set; }
    public PlayerEffects Effects { get; private set; }

    public PlayerContext(PlayerScript owner, GameObject gameObject, Transform transform, TargetResolver resolver, CommandCollector collector, CharacterSpec spec, IPlayerLogger logger)
    {
        Owner = owner;
        GameObject = gameObject;
        Transform = transform;
        TargetResolver = resolver;
        CommandCollector = collector;
        Spec = spec;
        Logger = logger;
    }

    public void RegisterStats(PlayerStatsBridge stats)
    {
        Stats = stats;
    }

    public void RegisterEffects(PlayerEffects effects)
    {
        Effects = effects;
    }
}

/// <summary>
/// Logger abstraction that can later be replaced with in-game consoles or
/// analytics hooks.
/// </summary>
public interface IPlayerLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

public sealed class PlayerLogger : IPlayerLogger
{
    private readonly string _prefix;

    public PlayerLogger(string prefix)
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
