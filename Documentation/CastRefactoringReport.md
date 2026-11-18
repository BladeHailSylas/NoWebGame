# Cast Refactoring 결과 보고서

작성일: 2026-11-18  
대상: NoWebGame v0.2 코어 안정화  
작업 브랜치: `refactor/CastRefactor`  
기준 dev: `162509b7`  
통합 자동 검증 기준: `5207802`

## 1. 결과 및 완료 판정

Cast Refactoring의 Phase 1–5 구현·검증을 완료했습니다. 핵심 결과는 **발동 확정(Cast Commit)과 실제 실행을 분리하여, Target·비용·Cooldown을 먼저 확정하고 Delay 이후 실행하는 계약**을 마련한 것입니다.

통합 자동 검증은 9개 실행 모두 성공했으며 1,388개 검사가 통과했습니다. 사용자의 누적 플레이 테스트에서 Target 확정, 비용 소비, 쿨다운, 취소, 실행된 효과의 후속 처리 및 취소 후 Mana 무환급을 확인했습니다.

중복 CC 적용 후 행동 제한이 남을 수 있는 **STACK-CC-01**은 미해결입니다. 사용자 결정에 따라 향후 Stack 시스템 재정비 과제로 분리했으며, 이번 완료 판정은 이 결함의 해결을 의미하지 않습니다.

## 2. 목적과 범위

v0.2 필수 안정화 과제인 쿨다운 등록, Delay 의미 변경, Mana 소비를 하나의 발동 계약으로 통합했습니다. 이를 위해 시간 시스템 안정화, 입력/Target 준비 분리, Switch 소비 정확성, 취소·수명·예외 처리까지 포함했습니다.

새 콘텐츠, Hold 기능, Stack 시스템 전면 재설계, 신규 부활/리스폰, 네트워크·멀티스레드 발동 처리는 도입하지 않았습니다. 기존 AI·Detect·Chain과 Target/공간 계산의 의미를 유지하는 범위에서 변경했습니다.

## 3. 단계별 구현 결과

| Phase | 주요 결과 | 커밋/상태 |
|---|---|---|
| 1 — 시간 시스템 안정화 | 절대 Tick을 long으로 통일하고 Scheduler 및 시간 소비자를 정비했습니다. TickerRunner 재활성화 시 같은 시계와 예약을 유지합니다. | `361d0f0` |
| 2 — 준비 계약과 Target 확정 | PrepareCast에서 Target을 선택하지 않고 실제 입력 처리 시 확정합니다. 루트 Mode는 SkillData를 참조합니다. | `c782b15` |
| 3 — Mana·Switch 소비 및 Commit | ManaCost/소비 API, 정확한 Variable 소비, 슬롯별 CD·Pending·Delay를 Commit에 연결했습니다. | `1a00656` |
| 4 — 취소·수명·예외 안정화 | 미실행 루트 취소, 실행된 효과 수명 보존, 명령별 예외 격리 및 일회성 정리를 구현했습니다. | `5207802` |
| 5 — 통합 검증 및 결과 정리 | 전체 회귀 재실행, 누적 수동 결과 반영, 알려진 결함과 검증 범위를 문서화했습니다. | 검증 완료, 문서 미커밋 |

## 4. 최종 Cast 계약

### 준비 → 확정 → 실행

1. **PrepareCast:** 향후 Hold를 위한 준비 단계입니다. Target 확보, 비용 소비, CD/Delay 예약을 하지 않습니다.
2. **입력 수집 및 처리:** 입력을 모은 뒤 Attacker Tick에서 시전자 상태·슬롯·CD·Pending 등을 검사하고 Target과 Switch 분기를 준비합니다.
3. **Cast Commit:** 자원과 조건이 유효하면 비용을 지불하고 CD·Pending·Delay를 등록하여 발동을 확정합니다.
4. **Delay 대기:** 실행 시각은 CommitTick + DelayTicks입니다. Collector가 늦게 처리하더라도 Delay를 처음부터 다시 세지 않습니다.
5. **실행 직전 검증:** 시전자의 활성·생존·공격 가능 상태, Collector의 유효성 및 기존 Target 계약을 확인합니다. 선택 사거리는 다시 검사하지 않습니다.
6. **실행 및 후속 처리:** 실행에 들어가면 Pending을 해제합니다. 이후 효과는 자체 종료 조건을 따르며 Chain은 기존 후속 수집 경로로 전달합니다.

Delay=0도 입력 함수 안에서 즉시 재귀 실행하지 않고 다음 실행 가능한 Collector 순회에서 처리합니다. 구독 순서에 따라 같은 Tick 또는 다음 Tick일 수 있습니다.

### Target과 공간값

- 일반 루트는 실제 입력 처리 시 Target을 확정합니다. Prepare만으로 미리 선택하지 않습니다.
- Entity Target은 선택한 대상의 정체성을 유지하고 해당 대상의 현재 위치를 사용합니다. Position Target은 선택한 좌표값을 유지합니다.
- Delay 중 커서가 이동해도 준비한 Target을 다시 선택하지 않습니다. 사거리 이탈만으로 실행을 취소하지 않습니다.
- OriginCastPosition은 최초 실제 실행 시 확정하여 Chain에 전달합니다. Destination은 각 Mechanism의 기존 공간 실행 규칙에 따라 계산합니다.
- Detect 미발견은 준비 결과로 유지하여 onNotFound 분기를 실행할 수 있습니다. Chain의 명시적 재탐색 계약도 유지합니다.
- SkillSlot의 옛 Mode는 직렬화 호환용으로 남기고, 루트 선택 방식은 SkillData 내부 Mode를 사용합니다.

### 비용·Cooldown·Pending

| 항목 | 확정된 동작 |
|---|---|
| Mana | SkillData.ManaCost를 Commit에서 지불합니다. 기본값은 0이며 음수/부족 비용은 거부합니다. |
| Cooldown | 슬롯별로 CommitTick부터 시작합니다. 실행이나 취소 시점부터 다시 시작하지 않습니다. |
| Pending | Delay 동안 같은 슬롯 재발동을 막습니다. CD가 먼저 끝나도 Pending이 남으면 거부합니다. |
| 같은 Tick 중복 | 동일 슬롯은 같은 Tick에 최대 한 번 Commit합니다. |
| 공개 전 실패 | 비공개 준비 중 실패하면 자원·예약·슬롯 상태를 복구합니다. |
| Commit 이후 실패/취소 | Mana·소비 Variable·CD를 환급하지 않습니다. |

공개 전 실패 복구는 Commit 이후 환급과 구분합니다. 비활성화·재활성화만으로 CD를 초기화하지 않으며, 영구 파괴 시에는 더 이상 사용하지 않을 소유자의 CD 핸들을 정리합니다.

### Switch 소비

선택한 정확한 StackKey와 현재 수량을 기준으로 소비합니다. 루트 Commit 또는 아직 지불되지 않은 후속 Switch의 수락 시점에 소비하며, 공유 소비 증표로 중복 차감을 방지합니다. 소비 후에도 선택 분기와 필요한 Variable 자료는 후속 실행에 전달됩니다.

코드의 Consume 기본값은 true입니다. 직렬화 필드는 반대 의미의 preserveVariable이며 Inspector의 Preserve Variable을 켜면 Consume=false입니다. 필드가 없는 옛 에셋은 기존 소비 동작을 유지합니다. 중첩 SerializeReference 저장·재로드도 검사했습니다.

소비가 필요한 새 외부 호출은 Collector 수락 경계를 사용해야 합니다. 미지불 Variable을 제공한 Consume=true Switch의 직접 Execute 호출은 거부합니다.

### 취소와 효과 수명

Suppressed/Stunned/Tumbled, 사망, Caster 비활성화·파괴 및 PreventActivation Dash 시작은 해당 시전자의 미처리 입력과 미실행 루트를 취소합니다. Rooted만으로는 공격을 금지하지 않습니다.

이미 실행된 루트와 그 효과를 시전자별 Pending 취소에 포함하지 않습니다. Projectile·Area·Chain은 기존 충돌, Target 손실, 시간 만료 등 자체 조건을 따릅니다. Dash의 이동 불가·소유자 비활성화에 따른 기존 종료 조건도 유지합니다. Caster가 파괴되어 참조 자체가 사라지면 기존 검증에 따라 효과나 후속 명령이 종료·거부될 수 있습니다.

Collector 자체가 비활성화·파괴되면 **전체 대기 큐와 예약을 정리하며 아직 실행되지 않은 후속 명령도 폐기**합니다. 서비스를 복구해도 옛 요청을 재생하지 않습니다. 반면 TickerRunner만 일시 정지하면 기존 시계·예약을 보존합니다.

### 예외와 추적

입력·예약·실행 예외를 항목별로 격리하여 다음 명령을 계속 처리합니다. 콜백 전에 현재 항목을 큐에서 분리하고 종료·정리를 한 번만 수행합니다. 표시나 관측자 예외로 이미 실행된 효과를 다시 실행하지 않습니다.

Commit 상태와 종료 사유를 기록하며 ExecutionReturned, DispatchAborted, Cancelled, ExecutionException을 구분합니다. ExecutionReturned는 실행 함수 반환을 뜻하고, 효과 만료나 명중 성공을 보장하지 않습니다.

## 5. 검증 결과

### 자동 검증

- 환경: Unity 6000.3.10f1, 원본과 분리된 검증 프로젝트.
- 기준: 커밋 `5207802`; 검증 시 원본/복사본의 C# 167개 SHA256 일치.
- 결과: **9개 실행 exit 0, 1,388 PASS / 0 FAIL**. 별도 total 요약행은 검사 수에서 제외했습니다.
- 범위: Cast Commit/준비/취소·수명, 시간 경계, 기존 TargetRefactor·관계·AI·Detect·Chain, Origin/BasePosition/Destination, Travel 물리, 기존 변환 콘텐츠.
- Phase 4 신규 검사: EditMode 53개, PlayMode 68개로 전체 합계에 포함됩니다.
- Tick 65,535 및 int.MaxValue 경계, 재활성화, 같은 Tick 입력, 비용 경쟁, 실패 복구, 직렬화, 예외 주입, 재진입 및 후속 처리의 일회성을 확인했습니다.

신규 동기 PlayMode 검사에는 명시적 물리 후처리를 사용하며, 실제 물리 프레임 검사는 기존 Travel/Destination 실행으로 보완했습니다. 이 결과가 모든 실제 씬의 물리 배치를 전수검증했다는 의미는 아닙니다.

### 사용자 수동 검증

| 항목 | 확인 결과 |
|---|---|
| Prepare와 Target 선택 | Prepare만으로 대상이 선택되지 않음 |
| Entity Target 고정 | 입력 시 선택한 B를 향해 발사; 이후 조준 변경으로 바뀌지 않음 |
| 사거리/시전자 위치 | Delay 중 사거리 밖으로 이동해도 실행; 실행 시 Player 위치에서 발사 |
| Position Target | 루트 SkillData Mode 수정 후 지점 P 선택 정상 |
| Cooldown | CD180 Dash의 일정 주기 재사용 확인 |
| Mana | Mana60/100에서 1회 발사 후 추가 발사 불가, 취소 후에도 무환급 |
| Switch | 비소비 시 조건 유지, 소비 시 Periodic 재획득 전까지 기본 분기 실행 |
| CC·비활성화 중 Pending | Commit된 미실행 Root 일괄 취소 |
| 실행된 효과 | 실행 후 Caster CC·비활성화에도 적절한 Chain 실행 |
| Delay 중 사망 | Commit된 미실행 Cast 취소 |
| Collector 부재 | 실행 대기 중 Collector 부재가 발생하면 기존 Cast 취소 |
| 취소 후 자원 | 소모한 Mana 반환 없음 |

연타 시 중복 발동 없음은 화면 관찰이며 정밀 중복 판정은 자동 검사로 보완했습니다. Collector 재시작 후 새 입력 수락 등 사용자 보고에 포함되지 않은 세부 조건은 자동 검사 근거로 구분합니다.

## 6. 알려진 결함과 후속 과제

**STACK-CC-01: 중복 CC 적용 후 ImmovableCount/InnoxiousCount 잔류 가능성**

Enemy AI Projectile의 Chain StackMechanism으로 CC를 반복 적용할 때, 이동 또는 공격 제한 카운터가 정상 해제되지 않을 수 있음이 확인되었습니다. 원인은 아직 확정하지 않았습니다.

향후 Stack 재정비에서는 실제 명중 → Chain → 적용/갱신/중첩/만료/제거 → 행동 제한 해제 경로를 조사해야 합니다. 모든 관련 CC가 종료되면 제한이 해제되고, 다른 유효 CC가 남으면 제한을 유지하는 회귀 검사가 완료 기준입니다. 직접 카운터를 균형 있게 증감하는 검사만으로 이 문제의 해결을 판정하지 않습니다.

상세 과제는 `Documentation/StackSystemFollowUps.md`에 기록했습니다. 사용자 승인에 따라 Cast Refactoring 종료와 분리했지만 해결된 것으로 처리하지 않았습니다.

## 7. 호환성 및 검증 범위 밖 항목

에셋에 저장되는 짧은 Delay/CD/지속시간 자료형과 긴 절대 Tick을 구분했습니다. 세션 간 기존 Entity/StackManager를 새 시계로 이관하는 기능은 제공하지 않습니다. 기존 60 Tick/초 진행 방식과 정지 시간 미보충 정책을 유지합니다.

Standalone 빌드, 모든 콘텐츠·씬의 전수검사, 부활/리스폰 및 Stack 전면 재설계는 수행하지 않았습니다. 기존 Unity 시작 과정의 패키지 경로·라이선스 갱신·Search 인덱싱 진단은 프로젝트 코드의 컴파일/검사 실패와 구분하여 기록했습니다.

자동 검증 후 로컬 StatsContainer에 사망 확인용으로 보이는 로그 추가가 존재합니다. 기존 자동 검사 수치를 이 후속 변경의 재검증 결과로 표현하지 않습니다. 사용자 에셋·씬·로그 변경은 이번 결과 보고서 작성 과정에서 수정하거나 커밋하지 않았습니다.

## 8. 산출물과 Git 상태

| 산출물 | 위치 |
|---|---|
| 본 결과 보고서 | Documentation/CastRefactoringReport.md |
| 세부 통합 검증표 | Documentation/CastRefactorValidation.md |
| 미해결 Stack 과제 | Documentation/StackSystemFollowUps.md |
| 구현 계약 | Documentation/TimeSystem.md, CastPreparation.md, CastCommit.md |
| 자동 검사 결과·로그·해시 | audit-results/phase5/ |
| 수동 재검증 참고 절차 | audit-results/phase5-play-test-guide-2026-11-18.md |

Phase 1–4는 커밋되어 있고 Phase 5 문서 및 본 보고서는 미커밋 상태입니다. 기준 dev부터 `5207802`까지의 커밋에는 ObjectInstances/Prefabs/Scenes 콘텐츠 변경을 포함하지 않았습니다. 현재 작업 트리에는 별도의 사용자 에셋·씬·코드 변경이 있으므로 전체 작업 트리를 Phase 5 산출물과 동일시하지 않습니다.

원격 push, PR 생성, dev 병합은 수행하지 않았습니다. 이번 요청에 대해서는 결과 보고서 작성과 커밋 메시지 추천만 제공하며 실제 커밋·병합을 수행하지 않습니다.
