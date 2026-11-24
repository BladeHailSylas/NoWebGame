# 기술 파이프라인 P1 작업 보고서

작성일: 2026-11-23 · 범위: 입력 준비 훅 의미 정리 및 빈 설정 방어

## 변경 내용

`Attacker.PrepareCast(SkillSlot, byte)`의 공개 서명을 유지하고 private `NotifyInputPrepared`에 위임하도록 했습니다. ActBridge 등 기존 호출자는 변경하지 않았습니다. 주석은 이 훅이 입력 준비 알림이며 Target 선택·자원 소비·예약·Hold 시작을 수행하지 않는다고 설명합니다.

기존 CC 차단과 binding 누락 진단 순서를 유지했습니다. binding은 있지만 SkillData, MechanismData 또는 Mechanism이 없으면 새 경고를 남기고 반환합니다. 기존에는 로그 작성 중 NullReferenceException이 발생할 수 있었습니다. 유효한 설정의 `Preparing ... (cooldown ...)` 로그 형식과 횟수는 유지합니다.

실제 실행 요청의 TryCast 검증, Target 준비, Mana/CD/Pending, Switch/Variable 소비, Chain 및 Stack 직접 실행은 변경하지 않았습니다. P2 내부 메서드 추출이나 Hold 기능은 구현하지 않았습니다.

## 검사 변경

CastCommitChecks.PipelineBaseline에서 P0의 알려진 결함 재현 3개를 예외 없는 반환과 무소비 기대값으로 전환했습니다. 각 빈 설정에서 명령 0, Mana 유지, 예약 0, Pending 없음까지 확인합니다. 같은 설정의 실행 요청 거부, binding 누락 및 Target 준비 중 취소 검사도 유지합니다.

기존 정상 준비 무소비, 실제 입력 처리, 비용/소비/보상/취소, 대상/공간/Chain 검사는 그대로 실행합니다. 이 단계에서 검사 개수는 늘리지 않았습니다.

## 검증

Unity 6000.3.10f1의 기존 검증 사본에서 `ExitLab.CastLifecycleChecks.RunWithRegression`을 실행하여 exit 0으로 완료했습니다. 원본과 사본의 Scripts/Editor/ExitLab C# 142개 해시가 모두 일치했습니다.

| 검사군 | 확인 수 |
|---|---:|
| CastLifecycle | 53 |
| CastCommit | 57 |
| CastPreparation | 38 |
| TimeSystem | 43 |
| BasePosition | 106 |
| RelativeTargetPipeline | 77 |
| EntityRelation | 407 |
| TemporaryEnemy | 40 |
| OriginCastPosition | 21 |
| AttackTrace | 19 |
| TargetContract | 13 |
| 합계 | 874 |

874개 모두 정상 기대값을 충족했습니다. P0/A1의 결함 재현 3개는 이번에 정상 반환 기대값으로 전환했으므로 동일 개수라도 의미가 다릅니다. 의도적인 예외 격리 검사 로그는 포함되며 전체 로그가 무오류라는 뜻은 아닙니다.

근거는 작업 공간 `audit-results/pipeline-p1/p1.log`, `p1-exit.txt`, 각 검사 결과 txt와 `source-copy-sha256.json`에 보관했습니다. 재사용 사본의 출력이 이전 보고서 근거를 덮어쓰지 않도록 A1 결과를 `pipeline-p0-a1/a1-results/`에 보관하고 실행 뒤 기존 위치에도 복원했습니다. 검증 사본은 P1 소스 상태입니다.

새 PlayMode 실행이나 수동 플레이 테스트는 수행하지 않았습니다. 이번 결과는 컴파일 및 EditMode 회귀 검사이며 이전 사용자 Stack 플레이 결과를 P1 검증으로 대신하지 않습니다.

P1 완료 후 중지하며 P2 승인을 기다립니다.

## 범위 준수

A1의 세 파일 분리와 기존 미커밋 변경을 유지했습니다. Git 명령·커밋·push·브랜치 작업은 수행하지 않았습니다. PlayGroundAssets와 원본 플레이 씬은 수정하거나 저장하지 않았습니다. P2 이후 작업은 시작하지 않습니다.
