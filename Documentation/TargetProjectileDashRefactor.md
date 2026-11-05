# Target · Projectile · Dash 리팩터링

## Target 값과 전달

`SkillTarget`은 `Unresolved`, `Entity`, `Position`을 구분한다. `default`는 미결정이며 `(0,0)` 위치와 다르다.

```csharp
// 객체 정체성은 유지하고, 실행/이동 시 현재 위치를 읽는다.
var tracking = SkillTarget.ForEntity(enemy);

// 지연과 후속 Chain에서도 바뀌지 않는 좌표다.
var destination = SkillTarget.At(new Vector2(3, 0));

// 무효 Entity를 원점으로 대체하지 않는다.
if (tracking.TryGetPosition(out var position))
{
    // position을 사용한다.
}
```

Entity 유효성은 Unity 객체 생존, `isActiveAndEnabled`, `targetable`로 판단한다. 이미 결정된 무효 Entity는 미결정 Target으로 바꾸지 않는다. 명시적 `requireRetarget`만 Resolver를 다시 호출한다.

Collider 식별은 `EntityIdentity.FromCollider`를 사용한다. 자신부터 부모 방향의 가장 가까운 Entity를 반환한다. 명중 집합과 목표 일치 비교도 이 Entity를 기준으로 한다. 기존 Transform 호출자는 경계 어댑터를 사용할 수 있다. 부모 Entity가 있으면 Entity Target, 없으면 호출 시점의 Position으로 복사된다.

Cursor 결과는 Position으로 전달하며 좌표용 Anchor를 대여하지 않는다. 기존 Anchor 클래스·씬 참조는 유지했다. Position을 Damage/Stack 등의 객체 수신자로 해석하지 않는다.

## 이동, 실제 접촉, 물리 반영

`TravelMotion`은 Projectile과 Dash가 공유한다.

1. Rigidbody 현재 위치에서 최신 Target 위치까지 이동량을 `speed / 60`으로 제한한다.
2. 실제 이동 Collider의 `Overlap`과 `Cast`로 접촉 후보를 수집한다. 원형 근사 반경이나 Transform 좌표 일치로 명중을 만들지 않는다.
3. 거리순으로 처리하며 같은 거리에서는 벽을 우선한다. 벽 뒤 적은 명중하지 않는다.
4. Entity별 명중 기록을 먼저 추가하고 onHit를 등록한다. 관통 중 비목표 적이면 이동을 계속한다.
5. MovePosition 요청 이후 물리 반영을 기다린다. 그 사이 추가 Tick이 이동 요청을 덮어쓰지 않는다.
6. 물리 이후 이미 확인한 구간 명중/차단 결과를 먼저 처리한다. 이 결과가 없을 때 최신 Entity 위치를 다시 읽어 도착 여부를 판단한다.

도착 오차는 0.001 월드 단위다. Entity가 이동하여 도착 범위를 벗어났다면 추적을 계속한다. Entity 좌표에 도착했지만 실제 Collider 접촉이 없으면 onHit 없이 `Interrupted / TargetNotHit`로 종료한다. 실제 접촉이 있으면 `Completed / TargetHit`다. Position 도착은 `Completed / PositionReached`다.

FixedUpdate 물리는 `TravelPhysicsObserver`의 물리 후 코루틴으로 관측한다. Script simulation 호출자는 성공한 `Physics2D.Simulate` 직후 다음을 호출해야 한다.

```csharp
if (Physics2D.Simulate(1f / 60f))
    TravelPhysicsObserver.NotifyManualSimulationCompleted();
```

기존 ExitLab Harness에도 이 연결을 추가했다. 전역 Ticker/FixedUpdate 스케줄은 교체하지 않았다. 지원하는 자동 물리 모드는 프로젝트가 사용하는 FixedUpdate이며, 별도 Update simulation 경로는 추가하지 않았다.

## 종료 정책

| 사건 | 결과 | onHit |
|---|---|---|
| 지정 Entity 실제 명중 | Completed / TargetHit | 해당 Entity 1회 |
| 비관통·비목표 적 명중 | Completed / EnemyHit | 해당 Entity 1회 |
| 관통·비목표 적 명중 | 계속 이동 | 해당 Entity 1회 |
| Position 도착 | Completed / PositionReached | 없음 |
| Entity 좌표 도착, 실제 접촉 없음 | Interrupted / TargetNotHit | 없음 |
| Entity 파괴/비활성/targetable 해제 | Interrupted / TargetLost | 없음 |
| 벽 | Interrupted / WallBlocked | 없음 |
| 이동 불가 또는 속도 0으로 진행 불가 | Interrupted / MovementBlocked | 없음 |
| Dash 교체 / Teleport | Interrupted / ReplacedByDash 또는 Teleported | 없음 |
| 소유 Player 비활성 | Interrupted / OwnerDisabled | 없음 |
| Projectile 외부 비활성/파괴 | Interrupted / ExternallyDestroyed | 없음 |
| 시간 만료 | 스킬의 timeoutPolicy / TimeExpired | 없음 |

시간 정책은 Target 종류와 독립적이다. 기본값은 Completed라 기존 데이터의 의미를 보존하며, 신규 추적 스킬에는 Interrupted를 권장한다. Legacy Projectile의 15 Tick 미만 즉시 만료도 유지한다. 신규 Classified 모드에서는 짧은 수명도 정상 Tick 정책을 사용한다.

확인한 구간 명중이 물리 반영 대기 중이라면 그 결과가 만료보다 우선한다. 외부 명시 취소/CC는 진행 중 계약을 중단할 수 있다. Player 비활성화는 Dash를 정리하며 독립 Projectile을 취소하지 않는다. 다만 후속 Chain의 시전자 자체가 파괴되면 해당 Chain은 실행할 수 없다.

`TravelCompletion.TryFinish`는 종료 상태를 먼저 기록하고 정리 후 콜백을 등록한다. 종료 후 재호출은 효과가 없다. Dash의 외부 취소는 아직 물리에 반영되지 않은 이동 요청도 취소한다. Play 종료·씬 정리는 새 게임플레이 Chain을 등록하지 않는다.

Exactly-once는 실행당 종료 확정과 종료 Chain 등록에 적용된다. Collector 취소/시전자 파괴/씬 정리에도 게임플레이 후속 실행을 반드시 보장하는 트랜잭션은 아니다.

## 새 스킬 설정과 기존 콘텐츠

- `callbackMode = Legacy` (0): 기존 `onExpire`만 사용한다. 새 lifecycle 정리 때문에 과거 없던 외부 Destroy/OwnerDisabled onExpire를 추가하지 않는다.
- `callbackMode = Classified` (1): `onCompleted` 또는 `onInterrupted`만 사용한다. `onExpire`는 동시에 실행하지 않는다.
- `timeoutPolicy`: Completed (0) 또는 Interrupted (1). 콜백 모드/Target 종류와 독립적으로 선택한다.
- `expireWhenUnexpected`는 이전과 마찬가지로 실행 정책으로 해석하지 않고 직렬화 호환용으로 보존했다.
- 기존 에셋은 일괄 변환하지 않았다. 새 필드가 없는 데이터는 Legacy/Completed/Inherit가 기본값이다.

종료 Chain의 `CastContext.Exit`에는 결과, Cause, 실제 종료 위치, 명중 Entity가 값/참조로 보존된다. 원래 Target은 자동 교체하지 않는다. 종료 위치에서 후속 효과를 실행하려면 해당 `MechanismData.targetSource = ExitPosition`으로 지정한다. `requireRetarget`가 참이면 재지정이 우선한다. 종료 데이터 없이 ExitPosition을 요구한 Chain은 생략된다.

SkillData는 실제 에셋 저장을 위해 별도 `SkillData.cs`로 분리했다. 기존 타입명/네임스페이스는 `Moves.SkillData` 그대로다. 기존 Generals.cs GUID `13cc4e30249147108873ff264a883568`을 SkillData.cs에 이전하여 스크립트 참조를 보존했으며 Generals.cs에는 새 GUID를 부여했다. 다른 기존 스크립트 GUID와 기존 프리팹·씬은 바꾸지 않았다.

## 검증 실행

Unity 6000.3.10f1 배치 모드에서 다음 진입점을 사용한다.

- `ExitLab.TargetContractChecks.Run`: Target/종료 계약, 직렬화 왕복 및 신규 검증 에셋 저장·재로드. `-quit` 사용.
- `ExitLab.TravelRegressionLauncher.Run`: 빈 임시 씬에서 Play 진입 후 실제 물리와 Production 시계 검증. `-quit` 없이 실행하며 테스트가 종료 코드를 반환한다.

Play 검증은 수동 Collider 물리, Collider 없는 Entity, 자식/다중 Collider, T02/T06/T07/TargetShape에 대응하는 회귀, 시간 정책, 지연 Chain, 외부 취소, 실제 Player 연결, 기존 Projectile 프리팹 생성, 씬 unload 및 Production을 포함한다. 배치 전용 검증이므로 열려 있는 편집 작업을 대상으로 실행하지 않는다.

이전 A/B 로그의 Tick별 좌표를 그대로 재현하는 것이 합격 조건은 아니다. Collider 형상 검사와 물리 후 종료 확정으로 정확한 명중 Tick은 달라질 수 있다. 통과 방지, Entity별 명중 횟수, 이동 지속, 종료 원인 및 Chain 등록/실행 횟수를 비교한다.

원본 Battlefield의 누락된 CharacterSpec/스킬 콘텐츠는 복구하지 않았다. 합성 Player 경로와 가용 프리팹/직렬화 샘플의 검증을 원본 게임 전체 콘텐츠 검증으로 확대하지 않는다. 전역 ushort Ticker의 장시간 오버플로는 이번 범위 밖이다.
