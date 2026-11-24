# 기술 파이프라인 P0/A1 작업 기록

작성일: 2026-11-23 · 범위: P0 기준 고정 및 A1 파일 분리 · P1 이후 미착수

## 기준과 작업 제한

사용자가 P0와 필요한 A1을 승인했습니다. Git 명령을 실행하지 않았으며 브랜치·커밋·원격 동기화도 하지 않았습니다. 원격 dev 병합은 사용자 전달 사항으로 기록하고 현재 로컬 파일을 기준으로 작업합니다. 기존 사용자 변경을 되돌리지 않습니다.

검증은 `audit-results/pipeline-p0-a1/validation` 사본에서 Unity 6000.3.10f1로 실행합니다. 이전 검증 사본의 의존성/캐시를 복사하고 현재 Scripts/Editor/ExitLab 소스를 반영했습니다. 원본 플레이 씬은 열거나 저장하지 않습니다. PlayGroundAssets는 복사·수정 대상에서 제외했습니다. 원본 소스 비교 및 결과는 작업 공간 `audit-results/pipeline-p0-a1/`에 보관합니다.

## P0 — 보존할 순서와 실패 경계

1. Input/ActBridge는 입력을 큐에 수집하며, performed의 PrepareCast는 Target·비용·예약을 확정하지 않습니다.
2. Attacker.Tick의 TryCast는 수명 → CC → CD → 소유자 → Pending → 동일 Tick → binding/설정 → 비용·Tick 범위 → Collector → 명시 대상 mode 순서로 확인합니다. ValidateLifetime 내부는 세대 → 소유자 활성 → 사망 → CanAttack → Collector 가용성 순서입니다.
3. Switch Variable을 선택하고 SkillCommand를 생성합니다. DamageData 캡처 시점은 이 단계입니다. 일반/Switch 생성 분기의 통합은 P2까지 하지 않습니다.
4. Collector/Runner/CastPreparation에서 Target을 준비한 뒤 수명을 재검사하고 Mana와 SwitchSelection을 확인합니다.
5. Collector는 실행 예약과 비공개 큐 항목을 확보합니다. Attacker의 ApplyResources는 CD 예약 → Variable 소비 시작 → Mana 지출 → CD/Pending/마지막 Tick 기록 → 소비 Complete 순서입니다.
6. Commit 공개 후 observer에게 알립니다. 공개 전 실패만 RollbackResources와 Collector의 명령/예약 정리로 보상합니다. 공개 후 취소·실행 실패는 환급하지 않습니다.
7. 예약 dispatch에서 수명을 재검사하고 BeginExecution으로 Pending을 해제한 뒤 Runner를 호출합니다. Target은 재탐색하지 않으며 Origin은 명시값 또는 실행 시 위치입니다. 공간 준비와 Mechanism 실행, 표시 예외 격리는 기존 경계를 유지합니다.
8. Chain은 별도 후속 수락이며 루트 비용을 추가하지 않습니다. Stack Trigger는 준비·전체 소비·직접 실행 계약을 유지합니다.

기존 거절 사유와 순서, Commit 상태 전이는 변경하지 않았습니다. P2에서 지켜야 할 검사 기준이지 새로운 실행 정책이 아닙니다.

## 기존 검사와 보강 항목

| 계약 | 기존/보강 근거 |
|---|---|
| 정상 준비는 무소비, 같은 Tick 중복 방지, Pending/CD 구분 | CastCommitChecks.ManaAndLocks / Timing |
| Target 선택 고정, 명시 AI 대상 수명·관계, 사거리 정책 | CastPreparationChecks.ExplicitAI 및 Target 관련 기존 검사 |
| 정확한 Variable 소비, 후속 단일 지불 | CastCommitChecks.SwitchCosts |
| 공개 전 예약·소비 실패 보상, 공개 후 무환급, observer 예외 | CastCommitChecks.FailureAndObservers |
| CC/disable/파괴/재진입 취소 | CastLifecycleChecks |
| 중첩 SerializeReference 보존, 공간/Origin/Chain | CastCommitChecks.Serialization 및 기존 TargetContract/Origin/BasePosition 검사군 |
| 빈 binding/SkillData/MechanismData/Mechanism | 이번 PipelineBaseline 보강 |
| Target 준비 콜백 중 취소 후 오래된 입력의 Commit 방지 | 이번 PipelineBaseline 보강: ExplicitAttackTarget의 isCurrent 콜백에서 CancelPending |

PipelineBaseline은 8개 확인을 추가합니다. 그중 3개는 PrepareCast의 null 구성에서 NullReferenceException이 발생하는 **기존 결함의 재현 확인**이며 정상 동작 통과로 분류하지 않습니다. 실제 실행 요청은 동일 구성에서 자원/Pending/명령을 만들지 않는지 별도로 확인합니다. P1에서 준비 훅을 수정할 때 이 3개 기대값을 예외 없는 반환으로 바꾸어야 합니다. 이번에는 준비 훅을 수정하지 않았습니다.

## A1 범위

Generals.cs의 CastContext, SkillCommand, SkillUtils 선언과 관련 주석을 각각 같은 Moves 폴더의 파일로 분리했습니다. namespace `Moves`, assembly, 공개 API, 본문, 생성자, 필드/특성, With 계열 및 Chain 정책을 그대로 유지했습니다. 기존 Generals.cs와 meta는 보존하고 새 파일에만 고유 meta를 두었습니다.

추가 using 배치를 제외한 세 선언 텍스트의 동일성을 확인했습니다. CastContext 생성자 공통화(P3), Logger/Interfaces 분리, 폴더 이동(B), namespace 개편(C)은 수행하지 않았습니다.

## 검증 결과

P0 및 A1 이후에 `ExitLab.CastLifecycleChecks.RunWithRegression`을 각각 실행했습니다. 두 실행 모두 exit 0이며 아래 결과가 같습니다.

| 검사군 | P0 / A1 확인 수 |
|---|---:|
| CastLifecycle | 53 / 53 |
| CastCommit (P0 보강 포함) | 57 / 57 |
| CastPreparation | 38 / 38 |
| TimeSystem | 43 / 43 |
| BasePosition | 106 / 106 |
| RelativeTargetPipeline | 77 / 77 |
| EntityRelation | 407 / 407 |
| TemporaryEnemy | 40 / 40 |
| OriginCastPosition | 21 / 21 |
| AttackTrace | 19 / 19 |
| TargetContract | 13 / 13 |
| 합계 | 874 / 874 |

각 실행의 874개는 정상 기대값 871개와 알려진 null 준비 결함 재현 3개입니다. 결함을 수정 완료한 것으로 세지 않습니다. 예외 격리 검사에서 의도적으로 기록한 예외도 있으므로 로그 전체가 무오류라고 표현하지 않습니다.

최초 실행은 제한된 환경에서 라이선스 연결에 실패했으며, 첫 재시도도 이전 프로세스가 사본을 점유하여 종료됐습니다. 이 작업이 시작한 기존 검증 프로세스만 종료한 뒤 권한을 조정한 실행에서 P0/A1 검사가 완료됐습니다. 사용자 원본 Unity 프로세스는 종료하지 않았습니다. 초기 실패 로그는 삭제하지 않았습니다.

근거 파일:

- `p0-verified.log`, `p0-verified-exit.txt`: P0 실제 검사 완료. `p0-results/`에 분리 전 결과 보관.
- `a1.log`, `a1-exit.txt`: A1 실제 검사 완료.
- `a1-declaration-equality.json`: 세 타입 선언·본문의 원본과 동일성.
- `pre-a1-sha256.json`, `a1-existing-file-changes.json`: Scripts/Editor/ExitLab/ObjectInstances/Prefabs/Scenes/ProjectSettings/Packages의 기존 파일 비교. A1 전후 변경된 기존 파일은 Generals.cs 하나이며 삭제 없음. P0 검사 보강은 이 스냅샷 전에 수행했습니다.
- `a1-meta-check.json`: 새 GUID 세 개가 Scripts 범위에서 각각 유일함을 확인.
- `p0-copy-sha256.json`, `a1-copy-sha256.json`: 원본과 검증 사본의 Scripts/Editor/ExitLab C# 파일이 각각 139개/142개 모두 일치.

새 PlayMode 실행 및 사용자 수동 전투 검증은 이번에는 수행하지 않았습니다. 선언의 동일성, 컴파일, 관련 EditMode 회귀로 파일 분리를 검증했습니다. 검증 사본의 이전 콘텐츠를 원본 최신 씬과 동일하다고 가정하지 않으며, 원본 애셋은 변경 전후 해시로 보존 여부를 확인했습니다. 이전 Stack 검사 통과나 사용자 플레이 테스트를 이번 변경의 검증 결과로 대체하지 않습니다.

## 완료 상태와 다음 단계

P0와 A1을 완료했습니다. 실제 런타임 변경은 세 타입의 파일 분리뿐이고, 검사 변경은 CastCommitChecks의 8개 기준 확인 추가입니다. P1 입력 준비 방어/명칭 정리, P2 Attacker 내부 정리, P3, B/C는 미착수입니다. 다음 승인 전까지 여기서 중지합니다. Git 작업은 수행하지 않았습니다.
