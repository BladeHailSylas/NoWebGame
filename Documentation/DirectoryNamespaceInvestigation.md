# 디렉터리·namespace 구조 조사 및 병행 범위 제안

작성일: 2026-11-23 · 상태: 조사만, 구조 변경 미승인

> 후속 설계 결정 (2026-11-23): 사용자 방향에 따라 A의 SkillCommand/CastContext/SkillUtils 파일 분리를 현재 파이프라인 개선 설계에 반영했습니다. B 물리 이동과 C namespace 개편은 파이프라인 완료 후 진행할 v0.2 후속 작업으로 기록했습니다. 아래 선택 가능성·v0.3 권고는 조사 당시 판단이며, 최신 범위와 일정은 [파이프라인 설계 개정 2](SkillPipelineImprovementDesign.md)를 따릅니다. 코드 변경은 아직 착수하지 않았습니다.

## 1. 결론과 조사 기준

기술 파이프라인 정리와 함께 진행할 가치가 가장 높은 작업은 **여러 책임을 담은 파일에서 핵심 실행 타입을 분리하되 기존 namespace를 유지하는 것**입니다. 다음으로 공용 전투 모듈의 디렉터리를 정리할 수 있으나, 전체 namespace 개편 및 assembly 분리는 별도 작업으로 권장합니다.

현재 `PlayerScripts`에는 Player뿐 아니라 Enemy와 Summon이 사용하는 Context, Attacker, Stack, Stats 등이 들어 있습니다. `Systems/Data`에는 데이터 외에 EnemyEntity 실행 주체가 있으며, `Moves/Generals.cs`는 명령·Context·Mechanism·Chain 생성까지 포함합니다. 책임을 찾기 어렵게 하는 실제 문제입니다. 반면 모든 하위 폴더와 namespace를 즉시 일치시키는 작업은 현재 파이프라인 개선의 선결 조건이 아닙니다.

사용자에 따르면 Stack 정리 작업은 원격 dev에 병합되었습니다. 이번에는 Git 명령·원격 조회·동기화를 수행하지 않았으므로 원격 병합 커밋이나 로컬과의 동일성은 확인하지 않았습니다. 분석 대상은 현재 로컬 파일이며, 병합 후 원격에 추가된 변경까지 포함한다고 주장하지 않습니다. 구현 착수 시 별도 승인된 범위에서 기준 상태를 확인해야 합니다.

`Assets/Scripts`, `Assets/Editor`, `Assets/ExitLab/Editor`, 관련 Documentation 및 `Assets/ObjectInstances`의 직렬화 타입 기록을 읽었습니다. PlayGroundAssets는 조사·변경 대상에서 제외했습니다. Unity 실행, 코드/애셋 이동·변경, Git 작업은 하지 않았습니다. 신규 산출물은 이 보고서뿐이며 기존 파이프라인 설계안은 수정하지 않았습니다.

## 2. 세 종류의 변경을 구분

| 변경 | 의미 | 기본 판단 |
|---|---|---|
| 파일 분리 | Generals.cs의 SkillCommand 등을 개별 파일로 분리, 완전한 타입 이름 유지 | 파이프라인 개선과 직접 병행 가능 |
| 물리 경로 이동 | 파일과 대응 meta를 함께 이동, 타입 이름 유지 | 컴파일 경계/경로 의존성 확인 후 선택적으로 병행 |
| namespace·assembly 변경 | 전체 타입 이름 또는 직렬화 assembly 식별자 변경 | 애셋/도구/외부 참조 마이그레이션을 포함한 별도 작업 권장 |

파일 이동이 언제나 안전한 것은 아닙니다. Editor 폴더 경계, 파일/Unity 클래스 연결, meta 식별자, 도구의 경로 문자열을 확인해야 합니다. 폴더와 namespace가 과도기적으로 다른 상태는 문서화하면 허용할 수 있습니다. 프로젝트 고유 최상위 namespace를 일괄 추가하는 안도 세 번째 변경에 해당합니다.

## 3. 파일별 후보와 병행 가능성

경로는 `Assets/Scripts/` 기준입니다. 아래 목적지는 확정 명칭이 아닌 책임 중심 배치안입니다. ‘병행’은 이번 조사에서 실제 이동한다는 뜻이 아니라 향후 승인 범위에 포함할 수 있다는 뜻입니다.

| ID / 대상 | 관찰된 문제와 조정 후보 | 파이프라인과의 관계 / 판단 |
|---|---|---|
| D01 `Moves/Generals.cs` | DamageData, INewMechanism/NewMechanism, TargetMode, CastContext, SkillCommand, SwitchVariable, SkillUtils, MechanismData 등이 한 파일에 혼재 | **직접 병행 권장.** 우선 SkillCommand.cs, CastContext.cs, SkillUtils.cs 분리. namespace `Moves`, 공개 타입/서명/직렬화 이름 유지. 나머지는 필요 범위만 순차 분리 |
| D02 `PlayerScripts/Acts/Attacker.cs` | 공용 루트 수락인데 Player 소속으로 보임 | **내용 정리는 기존 P2.** `Combat/Casting/Attacker.cs` 물리 이동은 선택적 별도 단계. 내부 추출과 이동을 같은 검증 단위에 섞지 않음 |
| D03 `PlayerScripts/Skills/{CastCommit,CastPreparation,CommandCollector,CommandBridge,SkillRunner,SwitchSelection}.cs` | 공용 실행·예약 계층과 Target/표시 기능이 Skills에 혼합 | **물리 그룹화 병행 가능.** `Combat/Casting` 후보. namespace `PlayerScripts.Skills` 우선 유지. Collector/Runner 통합은 경로 정리와 별개이며 현 설계대로 보류 |
| D04 `PlayerScripts/Skills/{TargetResolver,RelativeTargetResolver,ExplicitAttackTarget}.cs`, `Moves/{SkillTarget,TargetPolicy,RelativeHitQueries}.cs` | 대상 계약과 탐색 구현이 두 루트에 분산 | **선택적 병행.** `Combat/Targeting`으로 탐색 경로 정리 가능. 데이터/탐색/명시 대상 검증 책임과 현재 namespace는 보존. Resolver 주입 방식 변경은 제외 |
| D05 `Moves/{BasePosition,SpatialExecution,TravelExit}.cs` | Moves 최상단에서 공간 준비·이동 종료 계약이 섞임 | **후속 또는 선택적 이동.** `Combat/Spatial` 후보. BasePosition.cs 내부의 밀접한 값 타입까지 무조건 한 파일씩 나눌 필요 없음. Origin/Destination 정책 변경 금지 |
| D06 `PlayerScripts/Acts/{ActBridge,Mover}.cs` | Player 외 Entity도 사용하는 행동 조정 및 이동 | **물리 이동만 조건부 병행.** `Combat/Actions`/`Movement` 후보. Mover.cs의 DashContract·TeleportContract 분리는 선택 사항. 행동 제한과 이동 구현 통합은 제외 |
| D07 `PlayerScripts/Core/Context.cs` | 공용 의존성 컨테이너인데 Player 명칭/주석. ILogger·Logger도 함께 선언 | **Logger 타입 분리는 병행 가능**, namespace 유지. Context는 `Combat/Runtime` 후보. Context 타입명 교체, 서비스 분해, 등록 순서 고정은 v0.3 |
| D08 `PlayerScripts/Core/InteractionFilter.cs`, `PlayerScripts/Stats/{StatsBridge,StatsContainer,StatsContainer.Buffs}.cs` | 공용 피해/효과 경계와 능력치 구현이 Player 아래 있음 | **후속 정리 권장.** `Combat/Interaction`, `Combat/Stats` 후보. partial StatsContainer 파일은 같은 그룹으로 이동. Buff 소유 기여 구조 재설계와 결합하지 않음 |
| D09 `PlayerScripts/Stack/{StackManager,StackTriggerExecutor,VariableStorage,VariableConsumption}.cs` | Stack runtime이 정의/Key/Registry와 다른 루트에 있음 | **후속 정리 권장.** `Combat/Stacks/Runtime` 또는 기존 `Systems/Stacks/Runtime`으로 통일 가능. 갓 검증한 Stack 동작과 파이프라인 변경의 회귀 원인을 분리 |
| D10 `Systems/Stacks/{StackKey,StackStorage,StackRegistry,StackRegistryObject}.cs`, `Definition/*`, `ExclusiveGroups/*` | StackStorage만 namespace가 `Systems.Stacks.Instances`. 실제 내용은 이름→정의 전역 Dictionary이며 ‘Instances’와 역할도 어긋남 | **소규모 경로 정리는 가능하지만 후속 권장.** Definitions/Runtime/Registry 구분 후보. StackStorage 삭제/Registry 통합은 현재 쓰임과 수명 확인 후 별도 설계. StackKey.cs의 StackStatus 분리는 낮은 우선순위 |
| D11 `Systems/Data/Interfaces.cs` | Entity/Stack/효과/이동/Stats/이전 기술 API 및 enum이 혼재; 일부 구 코드는 주석 처리됨 | **선별 분리는 병행 가능, 전체 정리는 후속.** 현재 기술 입력이 쓰는 SkillSlot 등만 필요 시 분리하고 `Systems.Data` 유지. 미사용처럼 보이는 API 일괄 삭제 금지 |
| D12 `Systems/Data/{Entity,EnemyEntity}.cs`, `PlayerScripts/Core/{PlayerEntity,PlayerInfo}.cs` | 실행 주체가 Data/Player 경로로 분산 | **후속 권장.** `Entities/Core`, `Entities/Enemy`, `Entities/Player` 후보. MonoBehaviour 경로 이동과 namespace/클래스명 교체를 구분. 임시 Trigger 예시를 임의 제거하지 않음 |
| D13 `Moves/ObjectEntity/{ProjectileEntity,AreaEntity,SummonEntity,SummonEntityBase}.cs` | 게임 효과 실행 객체와 완전한 행동 주체 Summon을 같은 분류로 묶음 | **후속 권장.** Projectile/Area는 `Combat/Effects/Runtime`, Summon은 `Entities/Summon` 후보. 생성/수명 구조 자체는 변경하지 않음 |
| D14 `Moves/ObjectEntity/EntitySettings.cs` | 파일명과 달리 ObjectPrefab MonoBehaviour 및 IAreaShapes/CircleArea/BoxArea/LaserArea 등 포함 | **후속 분리 우선 후보.** ObjectPrefab과 Shape 계약/데이터를 구분. 현재 파일·컴포넌트 참조 실태 확인 후 분리하며 파일명만 보고 즉시 재명명하지 않음 |
| D15 `Moves/Effects/Definitions/{AreaEffectEntity,MeleeEffectEntity}.cs`, `PlayerScripts/Skills/EffectDisplayer.cs` | Definitions 폴더에 Mesh 생성/자동 파괴하는 실제 표시 MonoBehaviour가 있음 | **물리 정리는 병행 가능하지만 선택적.** `Combat/Presentation` 후보. 효과 표시와 피해/Area 실행 객체를 혼동하지 않도록 분리. 표시 실패 격리 유지 |
| D16 `Moves/Mechanisms/*.cs`, `Moves/SkillData.cs` | 기능 소속은 비교적 명확하나 장기 Combat 배치와 불일치 | **현 namespace 유지 권장.** 물리 이동만 가능; namespace 개편은 직렬화 마이그레이션 필수 과제로 분리. 이름 NewMechanism에서 New 제거도 같은 범주 |
| D17 `Systems/SubSystems/{FixedMotor,ThinMotor,Teleporter,TravelMotion,TravelPhysicsObserver}.cs` | SubSystems가 책임을 설명하지 못함 | **후속 권장.** `Movement/Physics`, `Movement/Travel` 등 후보. Fixed/Thin 구현의 대체·통합 판단은 별도 조사 |
| D18 `Systems/SubSystems/{EntityRelationResolver,AllyEnemyChecker}.cs`, `Systems/Data/EntityRelation.cs`, `Systems/Anchor/*`, `Utils/CursorResolver.cs` | 관계/Anchor/입력 좌표 해석이 분산 | **선택적 폴더 정리, 기본은 후속.** 관계는 Entities/Relations, Anchor는 Combat/Targeting/Anchors, Cursor는 Input 후보. Cursor 입력과 일반 Target 탐색을 같은 책임으로 합치지 않음 |
| D19 `Systems/SubSystems/{TemporaryEnemyBrain,TemporaryAttackSupport}.cs`, `EnemyDummy.cs` | 임시 AI/지원 객체/전역 namespace 더미가 일반 시스템에 혼재 | **후속 실험 코드 분류.** `Prototypes/Enemy` 후보. Temporary라고 삭제 가능하거나 Editor 전용인 것은 아님. EnemyEntity 연결 및 씬 사용 확인 후 이동 |
| D20 `Systems/SubSystems/AttackScheduler.cs` | 파일 전체가 블록 주석인 이전 Stack 구현. 현 Cast 예약 계층으로 오해하기 쉬움 | **독립 정리 후보.** 삭제/아카이브 정책 합의 후 처리. 런타임 계층 통합 대상으로 세지 않음 |
| D21 `Moves/{AttackTrace,AttackTraceSettings}.cs`, `Debuggers/*`, `UIs/DebugOverlay.cs`, `Utils/AimLine.cs` | 실행 추적·시각 디버그 도구가 분산 | **후속 선택 정리.** Diagnostics/Runtime 후보. 런타임 진단을 Editor 폴더로 이동하지 않음 |
| D22 `Assets/Editor/*`, `Assets/ExitLab/Editor/*` | Inspector/마이그레이션 도구와 회귀 검사가 각기 타입/namespace를 참조 | **관련 이동 시 반드시 함께 점검.** 테스트와 제작 도구의 목적 구분은 유지. Editor 경계 보존. 검증 코드 개편 자체는 별도 범위 |
| D23 `Systems/Time/*`, `Systems/Data/FixedVector2.cs`, `Characters/CharacterSpec.cs` | 상대적으로 명확한 공용 시간·수학·캐릭터 정의 | **현재 유지.** 전역 이름 정책만을 이유로 움직이지 않음. Time 이름 충돌 개선도 이번 필수 범위 아님 |

## 4. namespace 변경의 구체적인 위험 근거

### 4.1 기술 애셋은 타입 이름을 저장하고 있음

`Moves/SkillData.cs:12`는 MechanismData를 SerializeReference로 보유하며, `Moves/Generals.cs:341`은 INewMechanism을 SerializeReference로 저장합니다. 실제 `Assets/ObjectInstances/SkillData/SkillData 1.asset:22,31`에는 다음 식별이 있습니다.

```text
class: MechanismData, ns: Moves, asm: Assembly-CSharp
class: DashMechanism, ns: Moves.Mechanisms, asm: Assembly-CSharp
```

따라서 namespace/assembly 변경은 using 수정과 컴파일 통과만으로 완료 판정할 수 없습니다. 기존 중첩 Chain/Switch 정의의 읽기·저장·재열기 및 누락 참조 검사가 필요합니다. 구 타입→새 타입 매핑을 준비하고, 사용할 Unity 버전의 지원 이관 방식과 실제 복원 결과를 별도로 검증해야 합니다. 특정 호환 속성을 붙이면 모든 자산이 자동 해결된다고 가정하지 않습니다.

### 4.2 도구에도 namespace 문자열 의존성이 있음

`Assets/Editor/TargetMaskMigration.cs:66–68`은 assembly 포함 타입 문자열을 해석하고 `Moves.Mechanisms`를 비교하며, 109행의 정규식도 같은 namespace를 찾습니다. `SerializeReferenceDrawers.cs`는 구체 타입별 Drawer를 연결합니다. namespace를 바꾸면 이러한 도구와 기존 migration 입력까지 함께 검토해야 합니다.

검사 코드에는 `PlayerScripts.Core.Logger` 등 완전한 이름과 private 멤버 reflection도 있습니다. 경로 이동만으로 private 멤버를 바꿀 이유는 없으며, 파이프라인 내부 정리 때 필요한 테스트 갱신과 namespace 이관 때문에 필요한 갱신을 구별해야 합니다.

### 4.3 공용 모듈인데 Player 의존으로 보이는 근거

`EnemyEntity.cs:82–100`과 `SummonEntityBase.cs:51–71` 모두 Context/StatsBridge/Attacker/ActBridge/StackManager를 구성합니다. 폴더의 Player라는 이름은 실제 사용 범위를 나타내지 못합니다. 다만 이름을 Combat으로 바꿔도 Context의 역방향 의존이나 초기화 순서는 그대로 남습니다. 경로 정리를 아키텍처 의존성 해소로 보고하지 않아야 합니다.

### 4.4 assembly 분리는 별도 설계

현재 조사한 Assets에서 asmdef/asmref 파일은 찾지 못했습니다. 이 결과는 패키지 전체에 assembly가 없다는 뜻은 아닙니다. 실제 기술 애셋의 assembly는 위와 같이 Assembly-CSharp입니다. 새 asmdef를 도입하면 타입 식별과 컴파일 참조 경계가 동시에 달라집니다.

예를 들어 Systems.Data.Interfaces는 Moves/PlayerScripts.Acts/Stack 정의를 참조하고, 이 공용 기능들은 다시 Systems.Data를 사용합니다. 현재 폴더를 그대로 assembly로 바꾸면 독립 모듈이 된다고 볼 수 없습니다. 계약 방향과 Context 조립 책임을 먼저 다루는 v0.3 설계 대상으로 둡니다.

## 5. 권장 목표 배치의 윤곽

아래는 탐색을 돕기 위한 물리 폴더 후보이며, 당장 이 구조 전체를 생성하거나 namespace에 적용하자는 제안은 아닙니다.

```text
Assets/Scripts/
  Combat/
    Casting/       # Attacker, Commit, 준비, Collector, Runner, 명령
    Targeting/     # 대상 계약/탐색/관계 연결/Anchor
    Spatial/       # 공간 준비와 위치 계약
    Mechanisms/    # 현재 Mechanism 구현
    Stacks/        # Definitions / Runtime / Registry
    Stats/         # 능력치와 Buff 소유 기여
    Runtime/       # 공용 Context 등 조립 의존성
    Presentation/  # 표시만 담당하는 객체
    Effects/       # Projectile/Area 등 실행 객체
  Entities/        # Core / Player / Enemy / Summon / Relations
  Movement/        # 행동 이동 및 Travel/Physics
  Input/           # 입력 장치/커서 해석
  Systems/Time/    # 기존 시간 서비스 유지
  Diagnostics/    # 런타임 추적과 시각 진단
  Prototypes/      # 실제 사용 여부를 확인한 실험 코드
```

작은 디렉터리를 미리 만드는 것보다 실제 이동 대상이 생길 때 생성하는 편을 권장합니다. `Contracts`, `Common`, `Utils`를 새로운 대형 혼합 폴더로 만드는 방식은 피합니다. SkillUtils 역시 파일 분리 후 이름만 보고 범용 Utils로 보내지 않고 실제 Chain 생성 책임을 기준으로 배치합니다.

## 6. 기존 P0~P4와 결합하는 최소안

**권장안 A — 파일 분리만 병행:** D01의 SkillCommand/CastContext/SkillUtils를 원래 Moves namespace로 분리합니다. P0 기준 확인 뒤, P2 내부 절차 정리 전에 별도 검증 단위로 처리합니다. 기존 Generals.cs의 나머지 타입은 남겨도 됩니다. CastContext 파일 분리는 기존 선택 P3의 생성자 변경을 승인한 것으로 취급하지 않습니다. Logger 분리와 Interfaces 선별 분리는 가독성 이익이 명확할 때만 추가합니다.

**선택안 B — 핵심 실행 경로의 물리 이동까지 병행:** A에 D02~D04의 Casting/Targeting 경로 정리를 더합니다. 기존 namespace를 유지하고 경로 대응표를 남깁니다. 파이프라인 내부 수정 완료 후 별도 단계로 이동하여 실패 원인을 구분합니다. Stack/Stats/Entity 전체 이동은 이 단계에서 제외합니다.

**보류안 C — namespace 전면 개편:** Combat 루트로 namespace까지 일치시키거나 프로젝트 고유 루트를 추가하는 안입니다. 직렬화·도구 마이그레이션과 범용 Context 의존성 설계가 포함되어 현재 최소 개선의 범위를 크게 늘립니다. v0.3 또는 독립 구조 정리 작업으로 권장합니다.

| 실행 순서 후보 | 완료 기준 |
|---|---|
| P0 + 이동 전 목록 | 승인된 기준 상태에서 원본 경로·타입 완전명·meta GUID·직렬화 참조·Editor 경계 기록 |
| 파일 분리(A) | 중복 타입 없음, 기존 완전명/공개 API 유지, 컴파일 및 기존 Cast 핵심 검사 통과 |
| 기존 P1/P2 | 입력 준비 방어와 Attacker 내부 정리, Target/자원/취소/소비 계약 회귀 통과 |
| 선택적 물리 이동(B) | 대응 meta 유지, 예상 경로만 변경, 누락 Script/타입 없음, 씬·프리팹·기술 애셋 로드 확인 |
| P4 통합 | Cast/Target/시간/Chain/Stack 회귀, 입력·AI·Switch 수동 확인, 경로 문서 갱신 및 변경 범위 보고 |

읽기 전용 로드 검사와 저장/재열기를 요구하는 마이그레이션 검사는 구분합니다. 후자는 원본 플레이 씬을 임의 저장하지 않고 승인된 검증 사본에서 실시합니다. 기존 Stack 수동 테스트 성공을 이동·namespace 개편 후 테스트 성공으로 대신하지 않습니다.

## 7. 별도 정책과 미확정 사항

- **범위 선택:** 권장 A만 포함할지, B까지 포함할지 구현 승인 시 정하면 됩니다. 조사 단계의 진행을 막는 질문은 없습니다.
- **namespace 일치 시점:** 당분간 경로와 namespace 불일치를 허용할지 결정해야 합니다. A/B는 이를 허용하는 안입니다.
- **최상위 이름:** 장기적으로 Combat/Entities 같은 도메인 루트를 사용할지, 프로젝트 고유 루트 아래 둘지는 미확정입니다. 프로젝트 이름 변경 가능성도 고려해 일괄 접두사를 지금 고정하지 않습니다.
- **실험 코드 보존:** EnemyDummy, TemporaryEnemyBrain/Support, EnemyEntity 임시 Trigger 및 주석 파일 AttackScheduler의 보존/아카이브/삭제 정책은 별도 승인 사항입니다. 사용처 검색에 안 보인다는 이유만으로 삭제하지 않습니다.
- **외부 사용 범위:** 외부 패키지, 다른 브랜치, 에디터 사용자 도구에서 공개 타입/파일 경로를 참조하는지는 로컬 검색만으로 확정할 수 없습니다.
- **제외 애셋:** PlayGroundAssets는 검사하지 않았으므로 해당 애셋까지 namespace 변경 호환성을 보증하지 않습니다. 향후 실제 namespace 이관이 승인되면 그 검증 범위를 별도로 합의해야 합니다.
- **동작 정책:** Cast/Stack/Variable/Switch 계약, 영구 sentinel·disable 적용·적용자 식별, Target/Origin/Destination, 루트 비용과 Pending 의미는 구조 조정으로 변경하지 않습니다.

## 8. 근거 파일 안내

- [기존 파이프라인 설계](SkillPipelineImprovementDesign.md)
- [복합 실행 타입 Generals.cs](../Assets/Scripts/Moves/Generals.cs)
- [복합 계약 Interfaces.cs](../Assets/Scripts/Systems/Data/Interfaces.cs)
- [공용 Context와 Logger](../Assets/Scripts/PlayerScripts/Core/Context.cs)
- [Enemy의 공용 모듈 구성](../Assets/Scripts/Systems/Data/EnemyEntity.cs)
- [Summon의 공용 모듈 구성](../Assets/Scripts/Moves/ObjectEntity/SummonEntityBase.cs)
- [namespace 문자열에 의존하는 마이그레이션 도구](../Assets/Editor/TargetMaskMigration.cs)
- [Shape와 ObjectPrefab 혼합 파일](../Assets/Scripts/Moves/ObjectEntity/EntitySettings.cs)
- [정의 저장소의 현재 namespace](../Assets/Scripts/Systems/Stacks/StackStorage.cs)

이 보고서의 파일별 판단은 코드 배치와 사용 관계에 관한 정적 조사 결과입니다. 타입 이관 성공, 씬 참조 보존, 성능 개선은 아직 검증한 결과가 아닙니다.
