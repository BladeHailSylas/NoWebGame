using UnityEngine;
public sealed class PlayerInstaller : MonoBehaviour
{
    [SerializeField] private CharacterSpec spec;
    [SerializeField] private InputBinder controller;

    private void Awake()
    {
        if (spec == null || controller == null)
        {
            Debug.LogError("[PlayerInstaller] Spec �Ǵ� Controller�� ��� �ֽ��ϴ�.");
            return;
        }
        controller.spec = spec;
    }
}