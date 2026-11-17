# 전투 시간 계약 — CastRefactor Phase 1

## Tick과 지속시간

- `Ticker.TickCount`와 `OnTick`의 인자는 `long`인 절대 Tick입니다. 0에서 시작하며 Step마다 1 증가합니다. 65,535 및 int.MaxValue를 지나도 순환하지 않습니다.
- `Step`은 long 범위를 벗어날 때 명시적으로 실패하며 기존 Tick을 유지합니다. 실행 중 시계를 0으로 되돌리는 Reset API는 제공하지 않습니다. 새 전투 세션은 새 시계로 시작합니다.
- `Schedule(n, callback)`은 현재 Tick+n에서 한 번 호출합니다. n=0은 거부합니다. 콜백 호출 전에 구독을 해제하여 예외 시 중복 호출하지 않습니다. 이는 명령 전체의 예외 격리를 의미하지 않습니다.
- 에셋에 저장한 Delay(byte), Cooldown(short), 지속시간/주기(ushort)는 그대로입니다. **짧은 지속시간과 긴 절대 만료 시각을 구분**합니다. 기존 Variable의 duration=65535 영구 규칙도 바꾸지 않습니다.
- Stack의 LastTick, Dash의 절대 EndTick, Entity/Act/AI/Collector의 Tick 전달은 long입니다. Area/Projectile의 자체 경과 카운터는 에셋 수명 기준의 상대값입니다.

## DelayScheduler

`Start(now, duration)`의 만료 시각은 now+duration입니다. 0 Delay는 같은 Tick에 완료됩니다. 음수 시작 시각/지속시간과 표현할 수 없는 만료 시각은 거부합니다.

`ModifyDelta`의 양수는 만료를 앞당기고 음수는 늦춥니다. 만료 판정과 남은 시간 표시는 같은 보정값을 사용합니다. 조회가 예약을 제거하지는 않습니다. Remove된 핸들은 완료되지 않은 것으로 조회되며 남은 시간 표시는 0입니다.

핸들 번호는 프로세스 내 Scheduler 사이에서 공유되는 long 번호입니다. 새 세션에서 옛 핸들이 우연히 같은 번호의 예약을 제거하지 못합니다. 저장/네트워크용 식별자나 여러 스레드에서 접근하는 API로 설계된 것은 아닙니다.

## TickerRunner 수명

| 사건 | 동작 |
|---|---|
| Awake | 시계·Scheduler를 한 번 생성하고 Time 및 Instance 별칭을 연결 |
| 첫 OnEnable | 한 개의 Tick 코루틴 시작. 첫 Tick은 초기화·구독 후 한 간격 뒤 발행 |
| 컴포넌트 disable / GameObject inactive | 코루틴 중지. Tick·예약·구독자 참조는 보존 |
| 재활성화 | 같은 시계/Scheduler에서 재개. 즉시 추가 Tick 없음, 정지 시간 보충 없음 |
| Destroy | 해당 소유자의 Time 관측 구독과 전역 별칭 해제 |
| 새 소유자 | 새로운 세션, Tick=0. 이전 세션 예약을 이관하지 않음 |
| 다른 Runner 동시 생성 | 오류 진단 후 중복 Runner 비활성화. 기존 시계 교체 금지 |

실행 순서 -80은 기존 TickerRunner meta 설정과 일치합니다. 새 컴포넌트도 같은 기본값을 사용합니다. Entity·Collector는 실제로 구독한 Ticker에서 해제합니다. Area도 비활성화 시 구독을 멈추고 재활성화 시 한 번만 복구합니다. 디버그 표시의 파괴된 Unity 참조가 시간 관측을 방해하지 않게 정리합니다.

세션 소유자를 파괴하고 새로 만들면서 기존 Entity/StackManager/Collector를 그대로 유지하는 **세션 간 객체 이관은 지원하지 않습니다**. 이 객체들은 해당 세션의 Scheduler를 보관합니다. 일시 정지는 Runner disable/re-enable을 사용하고, 새 전투는 소비자도 새 세션에 맞춰 생성해야 합니다.

60 Tick/초의 기존 WaitForSecondsRealtime 기반 진행을 유지합니다. 프레임 정체 후 누락 시간을 따라잡는 누적 시간 방식은 이번에 도입하지 않았습니다.

## 변경하지 않은 전투 규칙

이 변경은 Phase 1입니다. Target 확정 시점, ManaCost/소비, 쿨다운 등록, Pending Cast, Switch Consume, CC에 의한 명령 취소 및 명령별 예외 격리는 구현하지 않습니다. 일반 OnTick 구독자가 예외를 던지면 다음 구독자/코루틴 진행에 영향을 줄 수 있는 기존 문제는 후속 Phase 4 검토 대상입니다.

CommandCollector는 기존 수집→예약→만료→실행 흐름을 유지하며 Delay는 Collector 예약 Tick부터 계산합니다. Cast Commit 기준으로 변경하는 것은 Phase 3입니다. Runner의 구독 순서 자체를 중앙 실행 단계로 재설계하지 않았습니다.

## 검증

원본 프로젝트가 아닌 `TargetMaskMigrationTestCopy.marker`가 있는 검증 사본에서 실행합니다.

- EditMode: `-batchmode -nographics -quit -executeMethod ExitLab.TimeSystemChecks.RunWithRegression`
- 수명 PlayMode: `-batchmode -nographics -executeMethod ExitLab.TimeLifecyclePlayModeLauncher.Run`
- Destination/Travel 경계: `-batchmode -nographics -executeMethod ExitLab.DestinationPlayModeLauncher.Run`
- 기존 Travel: `-batchmode -nographics -executeMethod ExitLab.TravelRegressionLauncher.Run`
- 기존 상대 Target: `-batchmode -nographics -executeMethod ExitLab.RelativeTargetPlayModeLauncher.Run`
- 기존 콘텐츠: `-batchmode -nographics -executeMethod ExitLab.MigratedSkillPlayModeLauncher.Run`

PlayMode 진입점에는 `-quit`을 붙이지 않습니다. 검사 종료 시 자체 종료합니다. 신규 검사는 실제 65,540회 Step, 65,535/int.MaxValue 전후 Delay·Stack·Buff·CC·Dash, Runner 정지/재개·새 세션·핸들 분리 및 구독 해제를 포함합니다. Destination 시나리오는 0/65,534/int.MaxValue-1에서 각각 시작하여 동일한 Projectile/Dash 물리·종료 계약을 확인합니다.
