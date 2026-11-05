#if UNITY_EDITOR
namespace ExitLab
{
    // 입력/스탯/Awake 의존성 없이 기존 Entity의 정체성 검사만 제공합니다.
    public sealed class ExitLabEntity : Systems.Data.Entity { }
}
#endif
