/*using Intents;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#region ===== Core =====
/// <summary>
/// Provides the foundational global systems for the battle simulation layer.
/// Ensure this class is touched before using other BattleCore utilities to
/// guarantee that the internal runner MonoBehaviour exists.
/// </summary>
public static class BattleCore
{
	private static bool _initialized;
	private static GameObject _runner;
	static BattleCore()
	{
		Initialize(); //Initialize doesn't work I think
	}
	/// <summary>
	/// Global entity controller, it applies the updated status into every entity.
	/// </summary>
	public static TheWorld World { get; } = new();
	/// <summary>
	/// Global ticker operating with 60 ticks per real-time second.
	/// </summary>
	//public static Ticker Ticker { get; } = new();
	/// <summary>
	/// Validates Intent from Collector.
	/// </summary>
	public static IntentValidator Validator { get; private set; }
	public static SessionManager Manager { get; private set; }
	public static IntentCollector Collector { get; private set; }
	public static IntentRouter Router { get; private set; }
	//public static SkillRunner Runner { get; private set; }
	public static CoreMotor Motor { get; private set; }
	/// <summary>
	/// Creates an invisible runner GameObject (if required) and keeps it alive across scenes.
	/// </summary>
	public static void Initialize()
	{
		Debug.Log("BattleCore initializing...");
		//Validator = new();
		//Manager = new();
		//Collector = new();
		Motor = new();
		//Runner = new();
		//Router = new(Motor);
		Debug.Log("BattleCore initialized.");
	}
}
#endregion*/