# Cast Refactor 통합 검증 — Phase 5

검증일: 2026-11-18. 브랜치: refactor/CastRefactor. 검증 코드: **5207802**.
**Phase 5 검증 완료. 통합 자동 검증에 더해, 남아 있던 사망/Collector 부재 시 대기 Cast 취소 및 Mana 무환급을 사용자가 확인했습니다. STACK-CC-01은 별도 미해결 과제로 유지합니다.**

## 구현 이력

| 단계 | 커밋 | 결과 |
|---|---|---|
| Phase 1 시간 안정화 | 361d0f0 | long Tick, 시간 소비자 호환, TickerRunner 수명 |
| Phase 2 준비/Target 확정 | c782b15 | Prepare 무선택, 실제 입력에서 Target 확정, 루트 SkillData Mode |
| Phase 3 소비/Commit | 1a00656 | Mana·Switch 소비, CD·Pending·Delay 통합, 공개 후 무환급 |
| Phase 4 취소/예외 | 5207802 | 미실행 루트 취소, 효과 수명 보존, 명령별 예외 격리 |

Phase 4는 사용자 승인에 따라 중복 CC 결함을 후속 과제로 등록한 뒤 종료·커밋했습니다. 이번 Phase 5에서는 추가 런타임 코드를 변경하지 않았습니다.

## 최종 계약과 검증 근거

| 계약 | 자동 근거 | 누적 수동 결과 |
|---|---|---|
| Tick 65535 경계 이후 정상 진행 | TimeSystemChecks, TimeLifecycle | 일반 Play 확인; 경계까지 직접 대기하지 않음 |
| PrepareCast에서 Target 확보/소비 없음 | CastPreparation, CastCommit | 확인 |
| 실제 입력에서 Entity/Position 확정, 루트 SkillData Mode | CastPreparation, TemporaryAI, Target 회귀 | A/B 전환 및 Position P 확인 |
| Delay 중 사거리 이탈 허용, 실행 위치/Origin/Destination 보존 | CastPreparation, Origin, BasePosition, Destination | 사거리 이탈 및 장애물 뒤 이동 후 시전 확인 |
| CD 시작=Commit, Delay 중 중복 차단 | CastCommit | CD180 Dash 주기 확인; 연타는 화면 관찰 |
| Mana 부족 거부, Commit 후 무환급 | CastCommit, CastLifecycle | Mana60/100 및 대상 손실 취소 확인 |
| Switch 정확한 소비, 비소비 옵션 및 공유 증표 | CastCommit, 직렬화 재로드 | Consume 양쪽 분기·Periodic 재획득 확인 |
| CC/비활성화가 미실행 Root만 취소 | CastLifecycle | Enemy Projectile → Chain Stack CC 및 비활성화로 확인 |
| 이미 실행된 효과/Chain은 자체 종료까지 유지 | CastLifecycle, Travel, Destination | 실행 후 CC/비활성화에도 Chain 실행 확인 |
| Delay 중 사망 즉시 취소 | CastLifecycle.DeathAndSuspend | 사망 시 Commit된 미실행 Cast 취소 및 Mana 무환급 확인 |
| Collector 종료/재시작, 이전 세대 입력 거부 | CastLifecycle.CollectorLifetime/Reentrancy, TimeLifecycle | 실행 대기 중 Collector 부재가 발생하면 기존 Cast 취소 및 Mana 무환급 확인; 세대별 새 입력 수락 등 세부 경계는 자동 검사 근거 |
| 입력/예약/실행/표시/관측 예외 격리 및 일회성 정리 | CastLifecycle, CastCommit 실패 주입 | 수동 예외 주입은 요구하지 않음 |
| 기존 AI·Detect·Chain·Target/공간 계약 | 기존 TargetRefactor 전체 관련 회귀 | 위 전투 결과로 보완; 모든 콘텐츠의 수동 전수검사는 아님 |

## 자동 실행 결과

Unity 6000.3.10f1의 별도 검증 복사본에서 커밋 코드를 실행했습니다. 원본과 복사본의 C# 167개가 SHA256 기준으로 일치합니다. 사용자 테스트 에셋을 일괄 복사하거나 원본 씬을 조작하지 않았습니다.

9개 실행 모두 exit 0, **1,388 PASS / 0 FAIL**입니다. 결과 파일의 total 요약행은 수에서 제외했습니다.

| 실행 묶음 | 결과 |
|---|---|
| CastLifecycle + CastCommit + CastPreparation + 시간/Target/AI/Origin/BasePosition/추적 EditMode 회귀 | 성공 |
| CastLifecycle PlayMode | 성공 |
| CastCommit PlayMode | 성공 |
| CastPreparation PlayMode | 성공 |
| TimeLifecycle PlayMode | 성공 |
| Destination PlayMode | 성공 |
| Travel 물리/Player 통합 PlayMode | 성공 |
| Relative Target PlayMode | 성공 |
| 기존 변환 콘텐츠 PlayMode | 성공 |

Phase 4 신규 검사는 EditMode 53개, PlayMode 68개이며 위 전체 합계에 포함됩니다. 신규 동기 수명 검사에서 물리 후처리는 명시 호출하고 실제 물리 프레임은 기존 Travel/Destination 검사로 보완합니다.

실행 스크립트: audit-results/run-phase5-validation.ps1.
증거: audit-results/phase5/test-summary.json, execution-summary.json, source-copy-sha256.json, results 및 각 실행 로그.
검증 프로젝트는 기존 audit-results/phase2/validation을 재사용했지만 이번 실행 로그/결과는 phase5에 별도로 보관했습니다.

Unity 시작 중 기존 VisualScripting YamlDotNet 경로/라이선스 갱신/Search 인덱싱 진단이 남을 수 있습니다. 프로젝트 코드의 컴파일·검사 실패와 구분합니다. 실패 주입 검사에서 의도한 예외는 해당 구간에서만 허용하며 예상하지 못한 오류를 무시하지 않습니다.

## 미해결 결함 및 검증 한계

**STACK-CC-01 — 중복 CC 적용 후 ImmovableCount/InnoxiousCount 잔류 가능성**은 미해결입니다. 발견 경로는 Enemy AI Projectile의 Chain StackMechanism입니다. 사용자 요청에 따라 향후 Stack 시스템 재정비로 이관했고, 원인은 확정하지 않았습니다. 조사 범위와 완료 기준은 Documentation/StackSystemFollowUps.md에 기록했습니다.

직접 ApplyCC/RemoveCC를 균형 있게 호출하는 카운터 검사는 실제 Stack의 갱신·중복 적용·만료 경로를 보장하지 않습니다. 따라서 통합 자동 검사 통과를 이 결함의 해결 근거로 사용하지 않습니다.

후속 수동 결과: Delay 중 Caster가 사망하면 Commit된 미실행 Cast가 취소됩니다. Commit 이후 Execute 전 Collector가 부재한 순간이 있으면 해당 Cast가 취소되며, 소모된 Mana는 반환되지 않습니다. 사용자가 보고하지 않은 세부 재시작/새 입력 조건까지 수동 확인했다고 확대하지 않습니다. 기존 가이드는 재검증 참고용으로 보존합니다.

Standalone 빌드, 모든 실제 씬·콘텐츠의 전수검사, 신규 부활/리스폰 구현, Stack 시스템 재설계는 수행하지 않았습니다. Collector가 시전자와 같은 GameObject에 붙어 있으면 그 GameObject 비활성화는 서비스 종료 정책도 적용합니다. 서비스 종료 중 후속 명령 거부와 시전자별 Pending 취소는 구분해야 합니다.

## 작업 보존 및 현재 상태

- Phase 4 커밋은 기존 검증 파일에 후속 과제 문서를 추가한 21개 파일입니다. 테스트 파일 끝의 불필요한 빈 줄도 커밋 검사 과정에서 정리했습니다.
- 사용자 .gitignore 및 테스트 에셋 등 17개 변경 파일은 작업 전후 SHA256이 일치합니다. 근거: audit-results/phase5-user-files-before.json.
- 기준 dev 162509b7부터 HEAD까지의 커밋에는 ObjectInstances/Prefabs/Scenes 콘텐츠 변경을 포함하지 않았습니다.
- Phase 5 문서는 검토용으로 남깁니다. 원격 push·PR·dev 병합은 하지 않았습니다.
- 자동 검증과 사용자 보고의 범위를 구분하여 기록했으며, 요청한 Cast Refactor의 Phase 5 검증을 완료한 것으로 판단합니다. STACK-CC-01 및 위 검증 범위 밖 항목은 그대로 남습니다.


### 수동 결과 반영 시 작업 상태

결과 반영 당시 StatsContainer.cs에 사용자 변경으로 보이는 사망 로그(Debug.Log의 Oof 메시지)가 추가되어 있었습니다. 이를 수정하거나 커밋하지 않았습니다. 자동 1,388개 검사의 기준은 기존 5207802이며 이 로그 추가에 대한 재실행 결과로 표현하지 않습니다. 이번 후속 작업은 문서 갱신만 수행했습니다.

