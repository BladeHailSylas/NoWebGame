# Cast 준비와 Target 확정 (Phase 2)

## Root Mode의 설정 소유자

Root Cast는 `SkillData.mechanismData.mode`를 사용합니다. 일반 루트, Switch 루트, 명시적 AI 입력의 허용 검사와 임시 AI 지원 검사가 모두 이 값을 따릅니다. `SkillBinding.mode`는 과거 에셋의 역직렬화 호환성을 위해 남겨 둔 값이며 Inspector에서 숨기고 실행에서는 무시합니다. 옛 슬롯 값을 SkillData로 자동 복사하지 않습니다. 동일한 SkillData를 사용하는 슬롯은 같은 루트 Mode를 사용합니다.

Chain 자식은 기존대로 해당 자식 MechanismData의 mode를 사용합니다. Detect의 탐색은 기존 requiredMode를 유지합니다. 임시 AI의 TowardsEntity 전용 제한도 유지하므로, SkillData가 TowardsCursor인 공격은 옛 슬롯 값이 TowardsEntity여도 지원하지 않습니다.

## 두 준비 상태

`SkillCommand.Preparation`은 **Target 선택을 마쳤는지**만 나타냅니다. 비용을 지불했거나 실행을 보장했다는 뜻이 아닙니다.

| 상태 | 용도 | Runner 동작 |
|---|---|---|
| `Deferred` (기본값) | 외부 직접 명령, Chain 자식 | 기존처럼 미지정 Target을 실행 직전에 선택 |
| `RootTargetPrepared` | Attacker가 실제 입력을 처리한 루트 | 저장된 결과를 검증하여 사용; 커서 재탐색 없음 |

Target이 `Unresolved`여도 Detect의 **미발견으로 확정된 결과**일 수 있습니다. `Target.Kind`만으로 재탐색 여부를 판단하면 Delay 중 새 Target이 생겼을 때 실패 분기가 성공으로 바뀝니다. 별도 준비 상태가 이를 막습니다.

## 처리 순서

1. `PrepareCast`: 기존 준비 알림만 수행합니다. Target을 확보하지 않습니다.
2. `EnqueueCast`: 슬롯·추적 ID·선택적 AI 요청을 입력 버퍼에 넣습니다.
3. `Attacker.Tick → TryCast`: 기존 CC·CD·binding 검사 및 명령 생성 후, `Collector.TryPrepareRoot`를 호출합니다.
4. `CastPreparation`: Runner와 동일한 Resolver로 루트 Target을 선택합니다. AI 요청은 이 시점에 선택 사거리까지 검사합니다. 일반 탐색 실패는 큐에 넣지 않고 거부하며, Detect의 미발견은 허용합니다.
5. `Collector.EnqueueCommand`: 준비된 명령을 수집합니다. 기존 Collector Tick에서 Delay를 시작합니다.
6. Delay 종료 시 `SkillRunner`: 준비된 Target을 검증하고 실제 실행을 요청합니다. 외부/Chain의 `Deferred` 명령은 기존 실행 직전 탐색 경로를 사용합니다.

**Phase 3 전까지 Delay 시작은 Commit 기준이 아닙니다.** Mana 소비·CD 등록·Pending 잠금과 Switch 소비 이동은 아직 연결하지 않았습니다. `RootTargetPrepared`를 `Committed`로 해석해서는 안 됩니다.

## Target, Origin, Destination

- Entity Target은 동일한 Entity의 정체성을 유지합니다. 해당 Entity의 위치는 실행 시 조회합니다.
- Position Target은 입력 처리 시 선택된 좌표 값으로 유지됩니다.
- `CastPosition`은 기존 입력 처리 시 시전자 위치를 유지합니다.
- `OriginCastPosition`은 준비 단계에서 채우지 않습니다. 최초 실제 실행 직전에 시전자 위치로 결정하고 Chain에 상속합니다. 이미 전달된 원점은 유지합니다.
- `Destination`은 기존 `SpatialExecution`에서 각 Mechanism 실행 직전에 계산합니다. Target을 Destination으로 치환하지 않습니다.
- 준비 결과 복사는 Trace/CommandId, Source, Damage, Variable, Mask/TargetPolicy, Exit 및 기존 원점을 유지합니다.

## Delay 후 검증

일반 준비된 루트는 Target의 존재·활성·targetable과 상대 관계 정책을 확인합니다. AI 루트는 기존 요청 수명, 시전자 정체성·활성, 대상 유효성과 관계도 확인합니다. **플레이어와 AI 모두 선택 최소/최대 사거리는 다시 검사하지 않습니다.** 실제 타격 범위나 이동 제한 등 Mechanism 자체 규칙은 그대로입니다.

Detect는 미발견 또는 무효 Target을 실행 거부로 바꾸지 않고 기존 `onNotFound` 분기로 전달합니다. requiredComponent와 관계 조건은 Detect 실행 시 평가합니다. AI 명시 요청의 수명/정체성 검사 실패는 기존처럼 실행 거부입니다.

일반 루트의 CC·사망·비활성화에 따른 Pending 취소는 Phase 4에서 통합합니다. 여기서 새 취소 정책이나 이미 실행된 효과의 수명을 변경하지 않습니다.

## Chain과 외부 명령 호환성

새 `SkillCommand`의 기본 상태는 `Deferred`입니다. Chain은 부모의 Target/Origin/Exit을 기존 규칙으로 전달하되 루트의 준비 상태를 상속하지 않습니다. `requireRetarget`은 자식 실행 시 재탐색하고, ExitPosition보다 우선합니다. 명시적 AI 요청을 포함하는 외부 직접 명령도 별도 준비를 호출하지 않았다면 기존 실행 시 사거리 검사를 유지합니다.

## 검증 진입점

별도 Unity 검증 사본과 `TargetMaskMigrationTestCopy.marker`가 필요합니다.

- `ExitLab.CastPreparationChecks.RunWithRegression`: 신규 입력/Target/AI/Detect/Chain 검사 후 기존 시간·TargetRefactor EditMode 회귀 검사.
- `ExitLab.CastPreparationChecks.RunPlayMode`: 같은 새 계약을 실제 PlayMode 객체 수명에서 검사. 시계는 테스트가 직접 Step하여 결정적으로 진행합니다.
- 기존 Lifecycle / Destination / Travel / RelativeTarget / MigratedSkill PlayMode 검사도 수행합니다.

신규 검사는 OS 커서를 조작하지 않고 검증용 카메라를 이동하여 실제 Resolver의 조준 입력을 바꿉니다. 수동 전투 감각이나 전체 게임 씬의 장시간 실행을 대신하는 검사는 아닙니다.
