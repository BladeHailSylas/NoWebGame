using System;
using UnityEngine;

#region ===== Transform =====
/// <summary>
/// Core transform that stores a deterministic position alongside an optional planar rotation.
/// </summary>
[Serializable]
[Obsolete("EntityData already has the transform field; use it instead.")]
public struct CoreTransform
{
    public FixedVector2 position;
    public float rotation;

    public CoreTransform(FixedVector2 position, float rotation = 0f)
    {
        this.position = position;
        this.rotation = rotation;
    }

    public readonly Vector3 ToVector3(float z = 0f)
    {
        Vector2 pos2 = position.ToVector2();
        return new Vector3(pos2.x, pos2.y, z);
    }

    public readonly void ApplyTo(Transform transform)
    {
        if (!transform)
        {
            return;
        }

        Vector2 pos2 = position.ToVector2();
        Vector3 target = new(pos2.x, pos2.y, transform.position.z);
        transform.position = target;
        transform.rotation = Quaternion.Euler(0f, 0f, rotation);
    }

    public static CoreTransform FromTransform(Transform transform)
    {
        if (!transform)
        {
            return default;
        }

        Vector3 pos = transform.position;
        return new CoreTransform(new FixedVector2(pos.x, pos.y), transform.eulerAngles.z);
    }
}

/// <summary>
/// Keeps a Unity Transform in sync with a deterministic CoreTransform.
/// </summary>
[DisallowMultipleComponent]
public sealed class TransformSync : MonoBehaviour
{
    [Tooltip("Deterministic transform data that should be mirrored to the Unity Transform.")]
    public CoreTransform coreTransform;

    [Tooltip("Automatically initializes the BattleCore singleton if necessary.")]
    public bool autoInitializeBattleCore = true;

    private void Awake()
    {
        if (autoInitializeBattleCore)
        {
            BattleCore.Initialize();
        }

        /** Optional: Enable to copy from Unity Transform on Awake for editor previews. */
        // CoreTransform = CoreTransform.FromTransform(transform);
    }

    private void LateUpdate()
    {
        coreTransform.ApplyTo(transform);
    }

    public void SyncFromUnityTransform()
    {
        coreTransform = CoreTransform.FromTransform(transform);
    }
}
#endregion