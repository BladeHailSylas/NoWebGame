# Goal
Refactor the Player system into a **centralized, modular, and maintainable structure** where `PlayerScript` acts as the **root orchestrator** of all Player-related modules.  
All logic-bearing modules (e.g., `InputBinder`, `PlayerActController`, `PlayerAttackController`, `PlayerStatsBridge`, `PlayerEffects`) become **pure C# objects** without MonoBehaviour dependencies.  

---

# Context
Currently:
- Each module operates somewhat independently.
- `InputBinder` instantiates all modules but doesn’t coordinate them.
- Debugging is difficult due to scattered entry points.

Target design:
- `PlayerScript` (MonoBehaviour) is the **only runtime entry point** for the Player.
- It initializes, updates, and partially delegates logic to the modules.
- Modules communicate through a **shared PlayerContext** or controlled references.
- Only `PlayerScript` is responsible for Unity-specific callbacks (`Awake`, `OnEnable`, `OnDestroy`).
- If a module(except the PlayerScript) is a MonoBehaviour script, remove the MonoBehaviour; Especially about PlayerEffects, you can(even recommended) freely remove the current code since it's outdated.

---

# Input
Existing modules (as pure C# classes):
- `InputBinder`
- `PlayerActController`
- `PlayerAttackController`
- `PlayerStatsBridge`
- `PlayerEffects`

Desired features:
- One `PlayerScript` orchestrating initialization, updates, and destruction.
- Controlled logic delegation (PlayerScript can call specific module methods like `TakeDamage`).
- Modules remain testable and independent of Unity.

---

# Output
A refactored, orchestrated Player system with:
- A single MonoBehaviour entry point (`PlayerScript`).
- A shared `PlayerContext` passed to each module.
- Controlled lifecycle and partial delegation.
- Simplified debugging and centralized logging.

---

# Constraints
- **Only `PlayerScript`** is a `MonoBehaviour`.  
- All modules must be **pure C#**, containing no Unity engine dependencies except through `PlayerContext`.  
- Modules can only communicate **through `PlayerContext`** or **explicit method calls from `PlayerScript`**.  
- `PlayerScript` is the **sole owner** of lifecycle events (Awake, Update, OnDestroy).  
- The design must remain **debuggable**, i.e., logs and exceptions traceable from `PlayerScript`.  
- Avoid circular dependencies between modules.

---

# Procedure
1. **Define the PlayerContext**
   - Holds references to root `PlayerScript` and commonly accessed Unity elements (`Transform`, `GameObject`, etc.).
   - Optionally include a `Logger` or `DebugProxy` for unified logging.

2. **Refactor Modules**
   - Convert all modules into pure C# classes with a `PlayerContext` dependency.
   - Replace any Unity API calls (like `transform.position`) with equivalents via `context.Transform`.

3. **Implement PlayerScript**
   - Initialize all modules in `Awake()`.
   - Centralize their update calls inside `Update()`.
   - Delegate specific public logic (e.g., `TakeDamage`) as needed.

4. **Add Centralized Debugging**
   - Implement a shared `Logger` accessible through `PlayerContext`.
   - Optionally expose module states in the Unity inspector for debugging.

5. **Test Lifecycle**
   - Verify that all modules initialize, update, and dispose correctly.
   - Ensure modules remain functional when tested in isolation (without Unity runtime).

---

# Notes
- The `PlayerContext` serves as the single point of dependency injection.
- Each module follows a simple lifecycle pattern (`Initialize`, `Tick`, `Dispose`).
- The `PlayerScript` update loop determines the execution order for consistent and deterministic player behavior.
- Centralized orchestration improves maintainability and debugging by consolidating logic flow under a single control point.
