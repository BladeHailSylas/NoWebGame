using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class PlayerInstaller : MonoBehaviour
{
    [SerializeField] private CharacterSpec spec;
    [SerializeField] private PlayerScript playerScript;

    private void Awake()
    {
        if (spec == null || playerScript == null)
        {
            Debug.LogError("[PlayerInstaller] Missing spec or player script reference.");
            return;
        }

        playerScript.InstallSpec(spec);
    }
}
