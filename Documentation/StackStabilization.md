# Stack 안정화: S0 기준 및 단계 경계

## S1 구현 계약 (2026-11-18)

- StackManager는 수량 합산을 long으로 계산한 뒤 0~maxStacks로 제한합니다. 잘못된 정의·음수 max·음수 적용 Tick은 반영 전에 거부합니다.
- CC는 Key별 CCContribution 증표 하나를 보유합니다. 증표는 소유 ActBridge와 적용 당시의 이동/공격 제한을 기록하고 한 번만 해제됩니다. 양수 갱신은 증표를 교체하거나 제한을 추가하지 않습니다.
- 새 CC 예약을 먼저 확보하고 상태·기여를 확정한 뒤 Pending 취소를 알립니다. 예약/기여 준비 실패는 이전 상태를 보존하고 새 예약을 정리합니다. 알림 실패는 기록하되 확정된 CC를 복구·재적용하지 않습니다.
- 옛 주기의 해제 증표는 새 주기를 제거하지 못합니다. 기존 직접 RemoveCC도 Stack 소유 기여를 침범하지 않습니다. 만료 처리 대상은 수집 당시 DelayId와 현재 예약을 비교합니다.
- 처리 중 ApplyStack은 다음 버퍼로 넘기며 기간 override를 보존합니다. 처리 중 Tick 재진입은 무시합니다. 큐 항목은 외부 실행 전에 소비하고 예외를 항목별로 격리합니다.
- Variable 적용/최대치 소비 시 충전 개시/주기 충전은 새 예약 확보 후 수량을 반영합니다. 실패 시 기존 raw/public 수량과 예약을 유지합니다. Switch ConsumptionChange와 충전 규칙은 변경하지 않습니다.
- Buff/Trigger도 새 예약 생성 실패 시 옛 예약을 보존합니다. Buff 효과의 총량·기여 원자성은 S2, Trigger 실행 준비/전체 소비·payload 수명은 S3에 남아 있습니다.

S1 검사 진입점은 `ExitLab.StackTransitionChecks.RunPlayMode`입니다. 내부에서 갱신된 `StackBaselineChecks.Run`도 실행합니다. 기존 회귀는 `CastLifecycleChecks.RunWithRegression`와 `RunPlayMode`입니다. `RunPlayMode`에는 `-quit`을 붙이지 않습니다.

S0의 CC 결함 관측 로그는 보존하며, 현재 CC 검사는 정상 계약(counter=0)을 요구합니다. 남은 Buff·DR·Trigger 관측 7개는 여전히 KNOWN_DEFECT이며 정상 동작 PASS로 계산하지 않습니다.

## 승인 범위와 작업 기준

- 기준 dev: `4516b020775ec56bee077b9b5ce2349b6152d21e`.
- 작업 브랜치: `fix/stack-stabilization`. 원격 dev API 응답과 로컬 origin/dev SHA 일치 확인 후 분기했습니다. 로컬 dev 이름은 과거 커밋이므로 변경하지 않았습니다.
- S0는 수정 전 결함 재현·계약·검증 환경 확보입니다. 게임 로직 수정은 S1 이후입니다.
- 사용자 지시에 따라 각 단계 완료 시 작업을 종료하고 대기합니다. 단계 완료가 다음 단계 자동 착수나 커밋·push·PR·병합 승인을 뜻하지 않습니다.

## 확정된 목표 계약

| 항목 | 목표 |
|---|---|
| Buff | Key 총량 N으로 C(N) 계산. 배치 독립적이며 갱신은 새 기여−기존 기여, 종료는 기록된 기여 회수 |
| AP/DR | Key당 clamp(Value×N,0,100) 한 기여. 다른 Key와 기존 곱셈 합성 유지. handle로 자기 기여만 회수 |
| CC | Key당 제한 하나. 0→양수 획득, 양수→양수 갱신, 0/종료 해제 |
| Trigger | StackManager가 수량·기간·payload·소비·종료 소유, Executor는 준비·직접 실행·예외 처리 |
| Trigger 소비 | 준비 성공 시 payload 확보 후 전체 소비·1회 실행. 초과분 이월 없음 |
| 재시도 | 준비 실패는 미소비. 새 적용 요청에서 실제 증가0이어도 재시도. Tick만으로 자동 재시도하지 않음 |
| 재진입 | 실행 중 같은 Key 요청은 새 누적 주기. 이전 주기 후처리로 삭제하지 않음 |
| 실행 실패 | 소비 후 실패·예외는 복구/재실행하지 않음 |
| 만료 | Trigger payload와 수량·예약 함께 정리 |
| 원자성 | 새 기여·예약 준비 후 내부 확정. Pending 취소 등 외부 알림은 확정 이후 |

활성 Buff 중 ResetStats는 소유 기여를 재합성하여 종료 시 과차감을 방지하는 방향입니다. 새로운 리스폰 규칙을 도입하지 않습니다. DR의 `(int)Math.Max(0.15,total)` 절삭 문제는 Stack 소유권과 별개인 알려진 결함이며 이번 승인만으로 자동 수정하지 않습니다.

## 변경하지 않을 정책

- duration=65535: Variable/default는 영구, Buff/CC/Trigger는 유한 만료인 현재 차이를 보존합니다.
- disable 중 요청: Tick 중단·요청 큐 보존, enable 후 처리하는 현재 방향을 보존합니다.
- Key: Definition+applierName 동등성 유지. 같은 이름의 별도 Entity를 임의로 분리하지 않습니다.
- Variable 충전용 0 상태, raw/public 배타 저장소와 Switch 정확한 Key·증표·공개 전 보상 유지.
- Cast Target/Origin/Destination, 루트 Mana/CD/Pending·무환급, 실행된 효과의 수명 유지.
- Stack Trigger를 루트 Commit으로 옮기지 않으며 StackMechanism.onExpire를 실제 Stack 만료 콜백으로 바꾸지 않습니다.

## S0 검사 사용법과 판정

원본 Unity 프로젝트에서 실행하지 않습니다. `Assets`, `Packages`, `ProjectSettings`를 별도 사본으로 복사하고 루트에 `TargetMaskMigrationTestCopy.marker`를 둡니다. Unity 버전은 ProjectVersion에 맞춥니다. 원본/사본 C# SHA256을 비교하고 소스가 일치하는지 확인합니다. Library는 캐시일 뿐 소스 기준이 아닙니다.

Unity batch 실행 진입점:

- `ExitLab.StackBaselineChecks.RunPlayMode`: `-batchmode -nographics -executeMethod` 사용. PlayMode 진입 후 스스로 종료하므로 `-quit`은 붙이지 않습니다.
- `ExitLab.CastLifecycleChecks.RunWithRegression`: 기존 Cast/Commit/Switch/Target/시간 EditMode 회귀. `-quit` 사용.

Stack 검사 결과는 검증 사본의 부모 디렉터리 `stack-s0-results.txt`에 기록됩니다. 기존 회귀도 각 검사별 결과 파일을 같은 상위 경로에 기록합니다.

**KNOWN_DEFECT는 올바른 동작의 PASS가 아닙니다.** 수정 전의 실제 값이 예상한 결함 형태와 일치하는지 확인한 기록입니다. 예를 들어 CC가 만료됐는데 counter=1이면 기준 재현은 성공했지만 목표 계약은 실패입니다. 이후 S1~S3에서는 필요한 사례를 정상 계약 검사로 전환해야 하며 결함을 유지해야 통과하는 기준 검사를 안정화 완료 검사로 사용하면 안 됩니다.

Projectile 사례는 실제 EnemyEntity를 Caster/수신체로 만들고 실제 Collider 검사 → Projectile onHit → Chain → Collector/Runner → StackMechanism → Enemy.ApplyStack 큐 → StackManager → ActBridge를 통과합니다. 시계와 물리는 결정적으로 진행합니다. AI 판단·입력·루트 비용 Commit은 이 사례의 재현 범위에 포함하지 않으며 기존 Cast 회귀로 별도 확인합니다. 검증용 관계는 ExplicitNeutral로 명시하고 허용 Mask를 사용합니다. 실제 게임 씬을 전수검사했다는 의미가 아닙니다.

## 다음 단계의 완료 기준

1. S1: 공통 전이·일회성 효과 소유권·CC/예약 실패 보상. 반복 Projectile 사례에서 모든 CC 종료 후 counter=0. 다른 CC와 Dash는 유지.
2. S2: Buff 총량·반올림·AP/DR 소유 기여. 분할/일괄 동일, 상한 추가 증가 없음, 종료 잔여 없음, 활성 reset 정합성.
3. S3: Trigger 준비/소비/실행, 실패 미소비·상한 재시도·재진입·payload 만료·예외 격리.
4. S4: 파괴 정리, 미결 정책 현행 보존, 전체 Cast/Variable/Switch/Stack 회귀 및 수동 전투 검증.

세부 설계와 실행 로그·결과·해시 목록은 프로젝트 작업 공간의 `audit-results/stack-design-plan-2026-11-18.md` 및 `audit-results/stack-s0/`에서 관리합니다.

## S2 완료 — Buff 총량과 소유 기여

- 선행 S0/S1 변경은 `c74e7dd` (`fix(stacks): own CC restrictions and stabilize stack transitions`)로 커밋했습니다. S2는 별도 미커밋 변경입니다. S3는 통합하지 않았습니다.
- StackManager는 Key별 BuffContribution handle을 보관하고, StatsContainer는 해당 handle의 실제 수치·비율 기여를 보관합니다. 갱신은 총량 N의 새 기여로 교체하고 종료는 기록된 기여만 회수합니다.
- DamageBoost/ArmorBoost/Haste/Slow는 `Base × Value × N`을 long으로 계산하고 `(값+50)/100`으로 한 번 반올림합니다. 계산·최종 능력치의 int 범위를 벗어나면 적용을 거부합니다. 기존 일반 BuffData 호출은 변경하지 않았습니다.
- AP/DR은 Key당 `clamp(Value×N,0,100)` 하나의 기여를 소유 목록에서 관리합니다. 기존 public 목록은 일반 BuffData 호출용으로 유지하며, 최종 계산에서 양쪽을 곱셈 합성합니다. 회수는 값 비교가 아닌 handle로 수행하고 100% 해제 시에도 역나눗셈을 하지 않습니다.
- 준비에서 수치/소유권을 검증하고 새 예약을 확보한 뒤 외부 콜백 없이 상태와 기여를 확정합니다. 실패 시 기존 상태·기여·예약을 보존합니다. 활성 ResetStats는 기록된 소유 기여를 다시 합성하며 Stack 수명은 갱신하지 않습니다.
- 검증 진입점 `ExitLab.StackBuffChecks.RunPlayMode`는 별도 사본에서 S2·S1·기준 재현 검사를 연속 실행합니다. S2 78개, S1 95개, 기존 EditMode 866개, Cast PlayMode 68개가 통과했습니다. 기준 검사 21개 중 2개는 남은 결함의 형태 확인이므로 정상 동작 통과로 집계하지 않습니다.
- 남은 결함은 DR 정수 절삭(별도 정책)과 Trigger 만료 payload(S3)입니다. 수동 전투 검증 및 파괴 정리는 S4 범위입니다. 로그와 원본/사본 C# 171개 일치 근거는 `audit-results/stack-s2/`에 있습니다.

## S3 완료 — Trigger 누적·준비·전체 소비·직접 실행

S2 변경을 `1dd0230` (`fix(stacks): calculate buffs from owned total contributions`)로 커밋한 뒤 진행했습니다. S3는 별도 미커밋 변경입니다.

`StackManager.ApplyTrigger`가 Key 수량·payload·기간을 확정합니다. payload는 실제 수량 증가와 무관하게 요청당 한 번 합산하며 int overflow 또는 예약 실패 때 요청 전체를 거부하고 이전 상태를 보존합니다. 양수 수량에서 threshold(양수이며 maxStacks 이하)에 도달하면 요청당 한 번 준비합니다. 설정 오류는 진단을 남기고 미소비 상태로 유지합니다. 수량 0은 종료입니다.

상태 없는 `StackTriggerExecutor`에는 선택한 Mechanism/Mode, 적용자, payload 값 사본, 출처만 전달합니다. `TryPrepare`는 기존 null Target·적용자 Origin·Normal DamageData 계약으로 공간을 준비하고 성공 여부를 반환합니다. 준비 실패/예외는 미소비입니다. `ExecutePrepared`는 Manager가 소비를 확정한 뒤 직접 실행하며, 본문 예외를 기록하고 격리합니다. 실제 명중이나 효과 발생까지 보장하는 성공 판정은 아닙니다.

Manager는 준비 성공 후 해당 예약 ID가 여전히 현재 상태인지 확인하고 수량·payload·예약을 함께 제거합니다. 실행 후에는 Key를 재삭제하거나 payload=0을 다시 쓰지 않습니다. 기존 처리 가드 덕분에 준비/실행 중 재진입 요청은 다음 Tick으로 지연됩니다. 준비 실패 후 재진입은 기존 주기를, 소비 후 재진입은 새 주기를 이어갑니다. 이미 처리 버퍼에 있던 별개의 요청 두 개는 같은 Tick에도 각각 한 번 실행될 수 있습니다.

새 적용 요청은 실제 증가 0이어도 준비를 재시도할 수 있습니다. Tick 자체로는 재시도하지 않습니다. 초과 중첩 이월·소비 후 복구·자동 재실행은 없습니다. 만료와 명시적 0 전이도 payload를 함께 정리합니다. 루트 Mana/CD/Pending·CanAttack·Switch 소비를 새로 적용하지 않습니다.

검증 진입점은 분리 사본 전용 `ExitLab.StackTriggerChecks.RunPlayMode`입니다. S3 검사 후 Buff·전이·기준 검사를 연속 실행합니다. `StackTransitionChecks`에는 S3 직접 실행과 기존 루트 Pending/Mana/CD 독립성을 검증하는 6개 항목을 추가했습니다. 자세한 결과는 작업 공간 `audit-results/stack-s3/S3-report.md`에 기록합니다. S4 파괴 정리·수동 전투 검증과 별도 DR 절삭 문제는 남아 있습니다.

S3 46개, S2 78개, 전이·직접 실행 계약 101개, 기존 EditMode 866개, Cast PlayMode 68개가 통과했습니다. 기존 공간 검사의 private `ResolveTrigger` 호출은 공개 `ApplyStack` 검사로 갱신했습니다. 기준 검사 21개 중 남은 결함 형태 확인은 DR 절삭 1개이며, Trigger payload 만료는 정상 기대값으로 전환했습니다. 최종 세 실행 모두 exit 0이고 원본/사본 C# 173개 SHA256이 일치합니다. S3 완료 후 중지하며 S4는 아직 착수하지 않았습니다.

## S4 — 파괴 정리와 통합 검증

S3를 `d1253e0` (`fix(stacks): separate trigger preparation from consumption and execution`)로 커밋한 뒤 진행했습니다. S4 변경은 미커밋 상태입니다.

`StackManager.Dispose`는 한 번만 종료하며 요청·Tick·추가 소비를 차단합니다. 현재/다음 큐, 소유 예약, CC·Buff 기여, Variable 저장 상태, Trigger payload를 정리합니다. 파괴 시 CC 취소 알림이나 Stack 만료 Chain을 다시 발행하지 않습니다. 일반 효과와 다른 예약은 보존합니다. Player/Enemy의 OnDestroy는 Attacker 정리를 시도한 뒤 finally에서 Stack을 정리합니다. OnDisable은 Dispose를 호출하지 않습니다.

열린 Variable 소비 작업의 이전/새 예약도 회수하고 보상을 닫으므로, 뒤늦은 Rollback/Complete가 파괴된 소유자의 상태를 복원하거나 결제로 표시하지 않습니다. Buff는 기록된 소유 기여 전체를 long으로 합산 회수하여 반대 효과의 제거 순서 때문에 일시적으로 int 범위를 넘지 않도록 합니다. 기존 일반 능력치 API의 int 표현은 유지합니다.

분리 사본 전용 `ExitLab.StackLifecycleChecks.RunPlayMode`가 파괴/disable/재진입 종료 검사와 S1~S3 및 실제 Projectile 통합 검사를 실행합니다. 원본/사본 C# 174개 SHA256 일치 근거 및 실행 결과는 `audit-results/stack-s4/`에 보관합니다. 사용자는 수동 전투 검증 미실시를 확인했으며, 이 항목은 자동 검사로 대체 완료 처리하지 않습니다. 보고서와 수동 절차는 `S4-report.md`, `manual-combat-guide.md`에 기록합니다. 영구 sentinel·disable 중 적용·이름 기반 적용자 Key 및 별도 DR 절삭 정책은 변경하지 않습니다.

## 사용자 플레이 테스트 확인 (2026-11-23)

사용자가 Stack 플레이 테스트에서 사용 안내에 기재된 기대 결과를 실제 관측했다고 보고했습니다. 안내 범위의 수동 검증을 완료로 갱신합니다. 앞선 수동 미실시·미커밋 표기는 각 단계 작성 당시의 기록입니다. 이 확인은 별도의 예외 주입·모든 경계 조건 수동 검증을 뜻하지 않습니다. 이번 커밋은 S4와 임시 자기 피해 Trigger 예시, 현재 씬·캐릭터·Area 프리팹 변경을 함께 보존합니다. 무시된 PlayGroundAssets는 추가하지 않습니다. DR 정수 절삭과 미합의 정책은 별도 과제로 유지합니다.

## v0.2 릴리스 전 DR 수정 (2026-11-25)

앞선 단계에서 별도 과제로 남긴 DR 정수 절삭을 수정했습니다. 곱셈 합성과 잔여 피해 배율 하한 0.15를 유지하며 최종 배율의 int 변환만 제거했습니다. Stack 기준선의 DR 결함 재현은 정상 기대값 검사로 바꾸었고, Buff/DR 143개 및 기존 통합 회귀를 통과했습니다. 상세 결과는 [DR 안정화 보고서](V02DRStabilizationReport.md), 사용자 최종 시험은 [통합 플레이 가이드](V02FinalPlaytestGuide.md)를 기준으로 합니다. 위의 미해결 표기는 각 단계의 역사적 기록입니다. 현재 상태는 자동 검증 완료·최종 사용자 플레이 결과 대기이며 Version Bump는 하지 않았습니다.
