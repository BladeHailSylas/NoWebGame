using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class PlayerInstaller : MonoBehaviour
{
    [SerializeField] private CharacterSpec spec;
    [SerializeField] private PlayerEntity playerEntity;

    private void Awake()
    {
        if (spec is null || playerEntity is null)
        {
            Debug.LogError("[PlayerInstaller] Missing spec or player script reference.");
            return;
        }

        playerEntity.InstallSpec(spec);
    }
}
