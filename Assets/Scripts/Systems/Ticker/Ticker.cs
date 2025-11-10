using UnityEngine;
using System;

#region ===== Ticker =====

/// <summary>
/// Fixed-rate ticker that publishes a tick event 60 times per second.
/// </summary>
public sealed class Ticker
{
	public const byte TicksPerSecond = 60;
	public const byte TickIntervalMs = 1000 / TicksPerSecond;
	public static Ticker Instance { get; private set; }
	public event Action<ushort> OnTick;

	public ushort TickCount { get; private set; }

	public Ticker()
	{
		Reset();
		Debug.Log("Ticker here");
		Instance ??= this;
	}
	public void Schedule(byte ticksFromNow, Action<int> action) //ticksFromNow is byte since max delay is 120 ticks(2 seconds), the smaller the better for memory and packet size
	{
		if (ticksFromNow <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(ticksFromNow), "Must be greater than zero.");
		}
		var targetTick = (TickCount + ticksFromNow < TickCount) ? (ushort)(TickCount + ticksFromNow) : (ushort)1;
		OnTick += Handler;
		return;

		void Handler(ushort currentTick)
		{
			
			if (currentTick < targetTick) return;
			action(currentTick);
			OnTick -= Handler;
		}
	}
	public void Reset() => TickCount = 0;

	// Deterministic: "한번 호출할 때마다 정확히 한 틱"만 진행
	public void Step()
	{
		TickCount++;
		//if(TickCount % TicksPerSecond == 0) Debug.Log($"Time? {TickCount}");
		if (TickCount == 0) // wrap around to avoid overflow, though unlikely to happen in practice(it needs a battle that lasts more than 18 minutes)
		//The TickCount isn't supposed to be zero here as I added by 1 above. This means the TickCount had been 65535 and became 0; The overflow.
		{
			throw new TickCountOverflowException("It seems ushort was too short");
		}
		OnTick?.Invoke(TickCount);
	}
}
public class TickCountOverflowException : Exception
{
	public TickCountOverflowException()
	{
		
	}

	public TickCountOverflowException(string msg) : base(msg)
	{
		
	}
}
#endregion