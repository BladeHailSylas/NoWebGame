# Goal
Create an **IntentRouter system** for a Unity project that receives multiple `IIntent` objects and routes them to appropriate subsystems (`CoreMotor2D`, `SkillRunner`) using a simple switch-based routing mechanism.  
Also, extend the existing `IntentType` enumeration and intent definitions to include a new `CastIntent`.

---

# Context
The project uses an intent-driven architecture where subsystems act upon specific `IIntent` types.  

Each intent carries metadata such as its `OwnerID`, `IntentID`, and `GeneratedTick`.  
Currently, `MoveIntent` and `CastIntent` exist as concrete implementations.  

Routing rules:  
- `IntentType.Move` → handled by **CoreMotor2D** subsystem.  
- `IntentType.Cast` → handled by **SkillRunner** subsystem.  
- Any unrecognized or invalid intent type → **throw an exception** (non-critical) and skip that intent.

The `SkillRunner` exposes a single `Cast(CastIntent intent)` method.  
`CastIntent` contains both target references and skill metadata encapsulated in a `SkillInfo` struct.  

`SkillInfo` holds information about a skill’s mechanism and parameters.

---

# Input
**Provided interfaces and components:**
- `IIntent` interface and `IntentType` enum (`None`, `Move`, `Cast`).
- Subsystems: `CoreMotor2D` and `SkillRunner`.

**Routing requirements:**
- Each intent specifies an `IntentType`.
- The router should use a switch-case approach to determine routing.
- Subsystems are injected through the `IntentRouter` constructor.

---

# Output
**Deliverables:**
1. Add `Cast` to the `IntentType` enumeration.
2. Implement a new `CastIntent` class that:
   - Implements `IIntent`.
   - Contains `OwnerID`, `IntentID`, `GeneratedTick`, `TargetID`, `TargetPosition`, and `SkillInfo` properties.
   - Uses `FixedVector2D` instead of Unity’s `Vector2`.
3. Implement an `IntentRouter` class that:
   - Has a single public method `RouteIntent(IIntent[] intents)`.
   - Routes each intent based on its `IntentType`.
   - Delegates to the appropriate subsystem (`CoreMotor2D.Move()` or `SkillRunner.Cast()`).
   - Logs warnings for any failed intent routing attempts but continues processing others.

---

# Constraints
- Must be deterministic and Unity-friendly.  
- Do **not** use Unity engine types like `Vector2` inside intents.  
- `FixedVector2D` should be used for positional data.  
- The router must remain simple and non-reflective (use switch-case).  
- Should log warnings using `Debug.LogWarning()` when an intent fails to route.  
- Throw a non-critical exception when an unknown `IntentType` is encountered but continue processing remaining intents.

---

# Procedure
1. Extend the `IntentType` enum to include a `Cast` type.  
2. Define a `SkillInfo` struct containing `Mechanism` and `Param` fields.  
3. Implement the `CastIntent` class that carries `SkillInfo` and target information.  
4. Implement the `IntentRouter` class:
   - Accept `CoreMotor2D` and `SkillRunner` through its constructor.
   - Implement the `RouteIntent(IIntent[] intents)` method.
   - Use a switch on `IntentType` to delegate intent handling.
   - Catch any exceptions, log warnings, and safely continue.  
5. Integrate the router into your intent-handling pipeline so that all received intents are passed into `RouteIntent()` for dispatch.

---

# Summary
This specification defines how to implement a deterministic, subsystem-based `IntentRouter` that cleanly distributes intents to the correct Unity gameplay systems. It also introduces the `CastIntent` for skill activation and integrates clean error handling for robustness.