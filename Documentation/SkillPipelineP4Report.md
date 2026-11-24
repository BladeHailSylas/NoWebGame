# 기술 파이프라인 P4 통합 검증 보고서

작성일: 2026-11-23 · 상태: 자동 검증 및 문서화 완료, 사용자 수동 검증 대기

**2026-11-24 갱신:** 사용자가 미실시 항목의 검증을 위임했습니다. 실제 Input System 입력 자동화로 30개 확인 항목을 통과했습니다. [위임 검증 보고서](SkillPipelineDelegatedValidation.md)에 결과와 한계를 기록했습니다. 아래 수동 대기 문구는 위임 전 기록이며 현재는 P4 기능 검증 완료입니다. 사람의 화면 조작·원본 씬 시각 검증까지 완료했다는 뜻은 아닙니다.

## 범위와 기준

P0/A1/P1/P2 결과를 통합 점검합니다. P3는 설계에서 제외됐으며, B 물리 이동과 C namespace 개편은 후속 작업으로 유지합니다. P4 승인을 B/C의 착수 승인으로 해석하지 않습니다. Git 명령·커밋·push·브랜치 작업은 수행하지 않습니다.

검증 사본은 기존 `audit-results/pipeline-p0-a1/validation`을 사용했습니다. 현재 원본 Scripts/Editor/ExitLab C# 142개와 사본 해시가 모두 일치합니다. P2 이후 런타임 코드를 추가 변경하지 않아 P2의 EditMode 874개 및 CastLifecycle PlayMode 68개 결과를 재사용하고, 아직 실행하지 않은 Stack/Travel 통합 검사를 보완합니다. 동일 검사의 반복 실행을 새 검증 범위로 세지 않습니다.

원본 플레이 씬과 PlayGroundAssets는 수정·저장하지 않습니다. 사본의 빈 임시 씬에서 검사를 수행하며, 이것이 원본 Battlefield의 실제 사용자 입력 검증을 대체하지는 않습니다.

## 구현 결과와 계약 점검

| 항목 | 결과 / 근거 |
|---|---|
| A1 파일 분리 | 세 타입 선언 동일성 확인. 같은 Moves namespace 및 assembly 유지, 기존 Generals meta 보존 |
| P1 준비 훅 | 공개 PrepareCast 유지. 빈 설정 경고 후 반환. Target/비용/예약/Hold 동작 추가 없음 |
| P2 단계 분리 | 검사→Variable 캡처→명령 생성→Target 준비→수락. 일반/Switch 공통 생성 |
| 자원 원자성 경계 | ApplyResources/RollbackResources 본문 동일. 공개 전 보상·공개 후 무환급, observer 격리 회귀 통과 |
| Target/Origin/Destination | 선택 고정과 실행 시 검사, 명시 대상 및 공간/Origin 회귀는 P2 결과에 포함 |
| Pending/CD | 실행 진입/종료와 CD 독립, 세대/취소/실제 Entity 수명 회귀는 P2 결과에 포함 |
| Variable/Switch | 정확한 Key 소비, 단일 지불, 후속 수락과 루트 비용 독립성 유지 |
| Stack | 아래 P4 실제 PlayMode 통합 결과로 확인 |
| 수동 입력 | 사용자 미실시 확인. 아래 절차로 별도 확인 필요 |

## 자동 검증 결과

| 실행 | 결과 | 수행 시점 |
|---|---|---|
| CastLifecycleChecks.RunWithRegression | EditMode 874개 정상 기대값 통과, exit 0 | P2 결과 재사용 |
| CastLifecycleChecks.RunPlayMode | 68개 정상 기대값 통과, exit 0 | P2 결과 재사용 |
| StackLifecycleChecks.RunPlayMode | 파괴 23, Trigger 46, Buff 78, 전이/직접 실행 101개 통과, exit 0 | P4 신규 실행 |
| 같은 실행의 StackBaselineChecks | 24개 확인 중 정상 기대값 23개, 기존 DR 결함 재현 1개 | P4 신규 실행 |
| TravelRegressionLauncher.Run | Projectile/Dash/Chain 및 실제 물리 경로 96개 통과, exit 0 | P4 신규 실행 |

StackBaseline의 `observedKnownDefects=1`은 기존 DR 정수 절삭 재현입니다. 파이프라인 변경의 새 회귀로 단정하지 않으며, 정상 동작 통과나 해결 완료로 집계하지 않습니다. CC/Buff/Trigger 검사에는 실제 Projectile 명중→Chain→StackMechanism 적용 경로 및 Trigger 이후 후속 Chain이 포함됩니다.

P4 근거는 작업 공간 `audit-results/pipeline-p4/`의 `source-copy-sha256.json`, `p4-stack.log`, `p4-stack-exit.txt`, Stack 결과 txt 및 Travel 로그/결과입니다. P2 근거는 `audit-results/pipeline-p2/`에 그대로 유지합니다. 예외 격리 검사의 의도적 로그와 기존 결함 재현이 있으므로 전체 로그 무오류라고 표현하지 않습니다.

## 수동 플레이 확인 절차

사용자는 이번 파이프라인 변경 후 수동 검증을 아직 하지 않았다고 답했습니다. 이전 Stack 플레이 성공과 구분합니다. 다음은 사용자 검증용 절차이며, 이 문서 작성 중 씬/애셋 구성을 대신 수정하지 않았습니다. 각 시험은 플레이를 재시작해 상태를 분리하고, 필요한 설정이 없는 항목은 미실시로 기록합니다.

| 확인 | 수행과 기대 결과 |
|---|---|
| 정상 Attack 입력 | 유효한 Attack을 누르는 준비 단계에서 비용/예약/효과 없음. 놓은 뒤 처리 Tick에서 수락되면 Mana 한 번 차감, CD/Pending 적용, 효과 한 번 실행 |
| 빈 설정 | 빈 Attack 설정에서 누르기/놓기를 반복해 NullReferenceException 없음, 경고/거절만 발생, Mana·CD·Pending 변화 없음. Skill1/2/Ultimate는 현재 canceled만 연결되므로 동일한 performed 로그를 기대하지 않음 |
| 지연 중 이동 | 대상이 있는 지연 기술을 수락한 뒤 이동. Target 재선택 없이 기존 선택 유지, Origin은 명시값 또는 실행 시 caster 위치. 기존 공간 설정의 결과와 비교 |
| 지연 중 CC | 실행 전 공격 취소 CC를 적용. Pending 해제, 실행 취소, 이미 지불한 Mana/CD 무환급. CC 종료 후 CD 조건을 만족하면 재사용 가능. Rooted처럼 공격 취소가 아닌 CC와 구분 |
| AI 명시 대상 | AI가 선택한 대상에게 실행. 준비/실행 전에 대상 비활성화 등 수명 조건을 깨면 거부하며 커서 대상으로 대체하지 않음 |
| Switch와 Chain | 선택 Variable이 기존 정책대로 한 번 소비되고 후속 Chain이 이어짐. 이미 지불한 증표로 새로 누적한 Variable을 다시 소비하거나 루트 Mana를 다시 차감하지 않음 |
| Stack 연동 | 기존 사용 안내의 반복 CC 종료 복구, Buff 중첩/만료 수치 복귀, Trigger 소비 후 새 누적 주기가 계속 성립 |

결과를 전달할 때 기술/슬롯, 사용한 설정, 기대/관측 결과와 필요 시 Console 내용을 함께 기록하면 됩니다. 본문에서 나열한 모든 항목을 확인하기 전에는 수동 검증 포함 전체 완료로 표시하지 않습니다.

## 남은 작업

수동 검증 결과를 반영한 후 파이프라인 작업의 최종 완료 여부를 판단합니다. B/C는 v0.2 후속 과제로 기록되어 있으나 별도 대상/이관/검증 계획과 승인 전에는 시작하지 않습니다. DR 절삭, Stack sentinel·disable 적용·적용자 식별 등 기존 정책 과제도 변경하지 않습니다.

P4의 자동 검증과 문서화를 완료하고 중지합니다. 이번 단계에서 런타임/검사 코드의 추가 수정은 없으며 수동 검증을 포함한 전체 완료로 표시하지 않습니다.
