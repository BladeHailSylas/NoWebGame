# Base-Pos와 Destination 사용 및 테스트 가이드

## 구현 계약

Base-Pos는 극좌표를 월드 좌표로 해석합니다. `BasePositionResolver`는 입력 값만 사용하며 Camera, Transform, Physics에 의존하지 않습니다. 기준점은 CasterPosition / InputTargetPosition / OriginCastPosition / ExitPosition이고, 방향은 WorldRight / CasterToInputTarget / OriginToInputTarget입니다. 양의 각도는 월드 XY에서 반시계 방향이며 거리는 월드 단위의 유한한 0 이상 값입니다. 거리 0이면 기준점만 검사하고 방향과 각도를 해석하지 않습니다.

InputTargetPosition은 각 Mechanism이 실행되기 직전 최종 Target의 위치입니다. 원시 마우스 위치나 최초 루트 Target 좌표가 아닙니다. requireRetarget, 명중 Entity 전달, targetSource=ExitPosition을 먼저 적용하고 Destination을 계산합니다.

Destination 설정은 `NewMechanism`에 있고 기본값은 Disabled입니다. 활성화하면 실제 실행 직전에 한 번 해석하여 `CastContext.Destination`에 저장합니다. Target은 교체하지 않습니다. 같은 SkillData를 동시에 실행해도 결과는 각 Context에 따로 보관됩니다. 자식은 부모 Destination을 상속하지 않고 자신의 설정으로 계산합니다. OriginCastPosition은 기존대로 같은 Chain에 상속합니다.

일반 Runner의 Target 확보 조건은 바꾸지 않았습니다. CasterPosition만 사용하는 설정이어도 Target 탐색 실패를 우회하지 않습니다. Stack 등 직접 실행에서는 실제로 필요한 입력만 있으면 계산할 수 있습니다.

### Mechanism별 의미

| Mechanism | Destination의 역할 | 유지되는 제한 |
| --- | --- | --- |
| Projectile / Dash | 고정 이동 목적지 | 속도, 시간, 충돌, 관통, CC/lifecycle |
| Teleport | 요청 이동 지점 | 기존 MaxRange와 벽/적 차단 |
| Circle Area | 생성 중심 | 기존 반경/수명/관계 필터 |
| Box Area | 생성 중심 및 Origin에서 바라볼 지점 | 기존 Box 크기/수명 |
| Laser Area | Caster에서 바라볼 끝점 입력 | 기존 MaxRange로 실제 길이 제한 |
| Summon | 생성 위치와 방향 입력 | 기존 Owner/소속/후속 Target |
| Ray | 조준 지점 | 지점까지 거리 × rangeMultiplier |
| Hitscan | 조준 방향 | 기존 MaxRange 길이 |
| Melee | 부채꼴 조준 방향 | 중심 Caster, 기존 반경/각도 |
| Detect/Damage/Stack/Switch/Dummy | 지원하지 않음 | 기존 Target 기반 처리 |

공간적으로 방향이 필요한 실행에서 목적지와 방향 원점이 같으면 활성 Destination 경로는 SpatialDirectionUndefined로 Abort합니다. Circle, Travel, 360도 Melee처럼 방향이 필요 없는 경우는 해당 검사를 생략합니다. Disabled 경로의 기존 방향 보완 동작은 그대로입니다.

## 종료와 실패

- Destination 도달은 `Completed / DestinationReached(13)`입니다. Entity를 맞히지 않았다면 onHit은 없습니다.
- 지정 Target에 실제 명중하면 Destination에 이르기 전이라도 `Completed / TargetHit`으로 종료합니다. 지정 Target을 관통하는 기능은 없습니다.
- 다른 허용 Entity는 penetrative=true일 때 명중 후 계속 이동하고, false이면 `Completed / EnemyHit`으로 종료합니다.
- Destination 활성 이동 중 Target이 소실되어도 고정 지점까지 이동합니다. Disabled는 기존 TargetLost 중단 및 Entity 추적을 유지합니다.
- 벽/이동 불가/시간 만료/외부 취소는 기존 규칙입니다. Projectile/Dash에 새 누적 이동 거리 상한을 추가하지 않았습니다.
- Classified 모드만 onCompleted/onInterrupted로 분기합니다. Legacy 모드는 기존 onExpire를 유지합니다.
- Exit.Position은 실제 종료 위치입니다. 요청 Destination과 다를 수 있습니다. onHit 자식에는 실제 Entity를 전달하고, 종료 자식 Target은 기존 상속/ExitPosition 설정을 따릅니다.
- 해석 실패는 실행 전 Abort입니다. prefab 생성, 새 Dash, Teleport 전에 중단하고 Travel 콜백을 만들지 않습니다. 형제 명령은 계속 처리합니다.
- Destination을 켠 Stack 트리거는 준비가 실패하면 Stack과 누적 metadata를 보존합니다. 즉시 재귀 재시도하지 않으며 이후 정상 트리거 기회에 다시 시도합니다. Disabled의 기존 소비 정책은 유지합니다.

## 코드 확장 시 주의

Mechanism 실행 본문은 이제 `protected override ExecuteCore(CastContext ctx)`에 작성합니다. `new Execute`로 숨기지 마십시오. public `Execute` 및 `TryExecute`가 공통 준비 경계를 담당합니다. `TryExecute`의 true는 본문 호출을 뜻하며, 비동기 이동 완료나 실제 피해 발생을 뜻하지 않습니다.

`SpatialExecution.TryPrepare`는 직접 실행도 같은 방식으로 준비합니다. 이미 준비된 같은 Context는 다시 계산하지 않습니다. 새로운 실행은 새 Context를 생성하십시오. `WithExit`은 현재 실행의 Destination을 보존하지만 새 Chain Command에는 복사하지 않습니다.

공간 좌표는 `TryGetSpatialPosition`으로 조회합니다. Damage/Stack/Detect에는 이 API로 Target을 대체하지 않습니다. Projectile.Init, Mover의 Dash/Teleport 및 Area.Init도 준비 경계를 확인합니다. 저수준 TravelMotion 생성에 해석 불가능한 Context를 직접 전달하면 인자 예외가 발생하므로, 호출자는 먼저 TryPrepare의 실패를 처리해야 합니다. 정상 Mechanism 진입은 실패를 Abort로 반환합니다.

`AttackLog.Enabled`를 켜면 DestinationResolved, ExecutionAborted, FixedDestinationTargetLost, TravelStarted/TravelEnded에서 설정·좌표·출처를 확인할 수 있습니다. Skill 출처는 이름/런타임 instanceID/slot/root이며 Chain은 trigger[index] 경로를 덧붙입니다. Stack은 정의 이름/instanceID/effect로 표시합니다. instanceID는 세션 간 영속 ID가 아닙니다.

## 수동 테스트 준비

1. 원본 에셋을 보존하고 검증용 SkillData 및 씬을 복제합니다. 기존 에셋은 Destination=Disabled 상태로 먼저 플레이하여 기존 추적을 확인합니다.
2. Player/Enemy 및 벽을 두고, 피해 확인이 필요하면 InteractionFilter의 허용 설정을 확인합니다. Target Relations와 물리 충돌 설정은 서로 별개입니다.
3. AttackTrace를 켜고 해당 Caster로 필터링합니다. 실행마다 attack/command/effect ID와 source 경로를 함께 확인합니다.
4. 새 설정은 Inspector에서 Destination=BasePosition을 선택합니다. 지원하지 않는 Entity 효과/제어 Mechanism에는 설정이 표시되지 않습니다.

## 필수 수동 확인표

| 검사 | 설정/행동 | 기대 결과 |
| --- | --- | --- |
| 기본 극좌표 | Caster=(2,3), Target=(5,3), 기준점 InputTarget, 방향 CasterToInputTarget, 각도90, 거리2 | Destination=(5,5) |
| 거리0 | CasterPosition, 거리0, Target과 Caster 같은 위치 | 기준점 반환. 방향/각도 입력 비활성. Circle은 정상 생성 |
| 실패 | Root에서 ExitPosition 선택 또는 양수 거리의 CasterToInputTarget 방향이 0 | ExecutionAborted, 위치 fallback/Travel 콜백/prefab 없음 |
| Delay | 지연 중 Caster/Target 이동 | 실행 직전 위치로 계산. Origin은 최초 실제 실행 위치 |
| 고정/추적 비교 | 같은 Projectile을 Disabled와 BasePosition으로 각각 발사한 뒤 Target 이동 | Disabled는 추적, 활성은 최초 해석 지점으로 이동 |
| 도달 미명중 | Target 옆으로 Destination 지정 | DestinationReached / Completed, onHit 없음 |
| Target 소실 | 이동 중 Target 비활성화/파괴 | 활성은 계속 이동, Disabled는 TargetLost |
| 지정 Target | Destination보다 가까운 지정 Target과 충돌 | penetrative와 무관하게 TargetHit로 종료 |
| 비대상 Entity | 이동 경로에 다른 허용 Entity 배치 | 관통 true면 계속, false면 EnemyHit. Entity당 onHit 한 번 |
| 벽/시간 | 목적지 전 벽 또는 짧은 수명 | 기존 WallBlocked 또는 TimeExpired. timeoutPolicy에 맞는 Kind |
| Chain | onHit Damage와 종료 Area를 연결 | Damage는 실제 Entity, Area는 자신의 Destination 설정. 부모 목적지를 암묵 상속하지 않음 |
| ExitPosition | 종료 자식에 targetSource=ExitPosition 또는 Base-Pos 기준점 ExitPosition | 실제 종료 위치 사용. requireRetarget과 동시에 켜면 기존대로 재탐색 우선 |
| 공간 범위 | Target 선택 사거리 안에서 Destination을 멀리 지정 | 좌표 계산은 성공. Teleport/Laser/Hitscan/Melee는 각각 기존 실행 제한 유지 |
| 생성/연출 | Circle/Box/Laser/Summon에 다른 Destination 적용 | 생성 위치·방향·실제 물리 범위·화면 효과가 일치 |
| 조준형 | Target은 위쪽, Destination은 오른쪽 | Ray/Hitscan/Melee는 오른쪽 조사. 후속 Target은 실제 명중 Entity |
| Stack 직접 실행 | Exit 없는 Trigger에 ExitPosition → CasterPosition으로 변경 | 실패 시 Stack/metadata 보존, 성공 시 한 번 소비·실행 |
| 실패 부작용 | Dash 중 새 Dash/Teleport에 해석 실패 설정 | 기존 Dash를 취소하지 않음 |
| 직렬화 | 설정 저장 후 Unity 재로드 | 설정/기존 관계 Mask/콜백/관리 참조 유지. 기존 에셋은 Disabled |

각 항목을 Player와 Enemy 양쪽에서 확인하십시오. 실패 시 설정, source, Trace ID, DestinationResolved/ExecutionAborted 및 TravelEnded 로그를 함께 기록하면 원인을 구분할 수 있습니다.

## 자동 검사 실행

검증 사본 루트에 `TargetMaskMigrationTestCopy.marker`가 있어야 새 검사 진입점이 실행됩니다. 원본 프로젝트와 다른 `-projectPath`를 사용하십시오.

- EditMode: `-batchmode -nographics -quit -executeMethod ExitLab.BasePositionChecks.RunWithRegression`
- PlayMode: `-batchmode -nographics -executeMethod ExitLab.DestinationPlayModeLauncher.Run` — `-quit` 없이 실행하며 검사 종료 시 자체 종료합니다.
- 기존 Travel 회귀: `ExitLab.TravelRegressionLauncher.Run` — 역시 `-quit` 없이 실행합니다.

새 EditMode 검사는 극좌표, Delay/Chain, 직접 실행, Stack 소비, 실제 TravelMotion, 조준형 공격, Teleport 거리 분리, managed-reference JSON 왕복을 검사합니다. 새 PlayMode 검사는 Projectile/Dash 각각 도달·소실·명중·벽·관통·시간 만료·속도0과 콜백/Target 전달을 검사합니다. Inspector 시각 조작, 실제 Battlefield 배치 및 Area/Summon의 화면 효과는 위 수동 확인표로 검증합니다.

StartPosition, MassCast, CasterFacing, 새 이동 거리 제한 및 onAbort Chain은 구현 범위에 포함하지 않았습니다.
