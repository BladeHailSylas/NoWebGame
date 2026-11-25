# C3+C4 완료 보고서 — Unity 객체와 관리 참조 namespace 이관

작성일: 2026-11-24. 승인된 C3+C4를 통합 완료했습니다. C5는 착수하지 않았으며 Git 조회·커밋·push 및 원본 Unity 실행은 하지 않았습니다.

## 적용 범위

C1 manifest의 남은 104개 타입을 이관하여 대상 197개 타입 모두 새 완전명을 사용합니다. 클래스명, assembly, 파일 배치, 필드 및 실행 계약은 유지했습니다. 기존 C# 124개(Scripts 89, Editor 5, ExitLab 29, PlayGroundAssets 1)를 수정했습니다. 소비 코드의 참조 수정도 이 수에 포함됩니다.

새 파일은 `Assets/Editor/NamespaceTypeMap.cs`와 `Assets/ExitLab/Editor/NamespaceMigrationChecks.cs` 및 각각의 meta입니다. 전자는 197개 타입의 명시적 구→신 이름 해석을, 후자는 호환성과 타입 탐색 회귀 검사를 담당합니다. Editor/ExitLab 자체 namespace는 유지합니다.

저장 식별자는 111개 애셋(asset 91, prefab 12, unity 8)에 적용했습니다. 이 중 PlayGroundAssets 85개는 필요한 타입 식별자만 변경했습니다. 목록과 전후 해시는 [애셋 변경 명세](NamespaceC34AssetMigration.csv)에 있습니다. 합계 적용 파일은 239개(기존 코드 124 + 기존 애셋 111 + 새 코드/meta 4)입니다. 기존 meta는 변경하지 않았습니다.

## 직렬화와 도구 호환

- 이관 대상 최상위 class/struct에 구 namespace·타입명·assembly를 명시하는 MovedFrom을 적용했습니다. partial 선언은 속성을 한 번만 부여하고, generic metadata 이름을 보존했습니다.
- 승인에 따라 구체 Mechanism 7종(DetectMechanism, DummyMechanism, RayMechanism, StackMechanism, SummonMechanism, SwitchMechanism, TeleportMechanism)과 Shape 3종(CircleArea, BoxArea, LaserArea)에 `[System.Serializable]`을 추가했습니다. 베이스의 선언만으로 구체 타입의 직렬화 가능성을 보장할 수 없어 실제 탐색 대상 전체를 검사했습니다.
- TargetMaskMigration은 명시적 매핑을 통해 구/신 완전명을 해석하고 두 Mechanism namespace를 인식합니다. 알 수 없는 타입·assembly·지원하지 않는 YAML 배치는 REVIEW로 남깁니다. 이번 작업에서 기존 애셋에 TargetMask 정책 변환을 실행하지 않았습니다.
- 저장 데이터의 class/ns/asm 및 m_EditorClassIdentifier를 식별자 단위로 이관했습니다. 나머지 원문, rid, 공유/null 참조, 리스트 순서, 필드값, GUID/fileID를 보존했습니다. 전체 애셋 강제 재저장은 하지 않았습니다.
- NoWebGame.Input과 UnityEngine.Input의 이름 충돌, Time alias 및 partial 속성 중복은 검증 사본에서 발견하여 수정했습니다. 런타임 동작 변경은 포함하지 않습니다.

## 검증 결과

Unity 6000.3.10f1 검증 사본에서 import·재시작·실행 검사를 수행했습니다.

| 검사 | 결과 |
|---|---|
| namespace/도구 전용 검사 | 229 PASS |
| 기존 EditMode 회귀 | 874 PASS |
| CastLifecycle PlayMode | 68 PASS |
| Stack 주요 회귀 | 248 PASS |
| Stack 별도 기준선 | 정상 23 + 기존 DR 절삭 현상 확인 1, 예상대로 유지 |
| Travel | 96 PASS |
| 위임 입력 검증 | 30 PASS |
| 직렬화 값 대조 | 12,386개 기록, 호환 로드·명시적 이관·재시작 모두 차이 0 |
| 검사 대상 애셋 로드 | 550개 기록, 누락 참조 등 문제 0 |
| 원본 meta와 import 사본 GUID 대조 | 575개 일치 |
| 저장 데이터의 잔존 구 완전명 | 발견 0 |
| 식별자 이관 재적용 | 변경 0 |
| 적용 실패·동시 변경 시험 | 기존 파일 복구, 새 파일 제거, 적용 전 변경 감지 통과 |

중첩 Switch/Chain, 공유 참조, null, 수치가 포함된 별도 fixture도 사본에서 검사했습니다. 이 fixture와 사본 전용 보조 도구는 원본에 추가하지 않았습니다. 구/신 namespace에서 legacy TargetMask 128→Enemy 4 변환과 타입 식별자 보존을 각각 검사했습니다. 구체 Mechanism 13종의 탐색·생성·직렬화 가능성도 확인했습니다.

의미 기반 소스 대조는 허용된 namespace·using·타입 한정 및 속성 차이를 제거한 후 검사했습니다. 124건 PASS에는 사본 전용 도구 1개가 포함됩니다. 원본 기존 코드에서는 123개가 동일 구조이며 TargetMaskMigration 1개만 위에 설명한 명시적 호환 처리 변경을 가집니다. 새 도구/검사 2개는 별도로 검토했습니다.

초기 사본 컴파일 및 Serializable 검사 실패 기록은 남겨 두었습니다. 위 표는 수정 후 최종 실행 결과입니다. 기존 Visual Scripting 초기화 로그 및 의도적인 예외 주입 기록이 있으므로 전체 로그가 무오류였다는 의미는 아닙니다. 인간 플레이 테스트 결과로 간주하지 않습니다.

## 원본 반영과 보존

반영 직전 원본 Assets/Packages/ProjectSettings 기준 파일 1,077개의 해시가 모두 작업 시작 때와 같음을 확인했습니다. 검증된 코드와 데이터의 해시를 다시 검사하고 대상 파일별 백업 후 적용했습니다. 적용 후 239개 파일은 계획과 일치했고 다른 기준 파일은 그대로였습니다. 동시 편집 및 일부 적용 실패 시험은 별도 임시 폴더에서 수행했습니다.

Cast의 Target/Origin/Destination, 루트 Mana/CD/Pending과 취소, Stack/Variable/Switch의 소비·보상·직접 실행 계약을 유지합니다. 영구 지속시간·비활성 적용·적용자 식별 및 DR 절삭 정책도 변경하지 않았습니다.

근거는 저장소 상위 `audit-results/namespace-c34/`에 있습니다: `original-apply-plan.json`, `original-apply-results.txt`, `source-baseline.json`, `structure-verification.json`, `value-comparison.json`, `transaction-test-results.txt`, `namespace-migration-results.txt`, `regression-results/` 및 최종 `explicit-verified`, `reload`, `cast-play`, `stack`, `travel`, `input` 로그/종료 코드. 초기 실패 로그와 최종 성공 로그를 구분해야 합니다.

## 다음 단계

원본은 아직 Unity로 열지 않았습니다. **다음 원본 Unity 실행 시 변경된 코드와 애셋의 import·컴파일이 필요합니다.** 사본 검증은 통과했지만 원본 Editor import 완료를 의미하지는 않습니다. C5 최종 통합 점검은 별도 승인 후 진행합니다. 현재 단계에서 정지하며 커밋하지 않습니다.
