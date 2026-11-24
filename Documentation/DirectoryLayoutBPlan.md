# B 계획 — namespace를 유지하는 물리 배치 조정

작성일: 2026-11-24 · 상태: B0~B2 완료, B3/B4 이동·정적 검증 완료 / Unity 검증 및 B5 대기

진행 기록: 사용자 승인에 따라 B0와 B1을 일괄 수행했습니다. [B0/B1 보고서](DirectoryLayoutB0B1Report.md)에 실제 범위와 검증 결과를 기록합니다. 이어서 [B2 보고서](DirectoryLayoutB2Report.md)의 공용 상태·Stack 19개 이동을 완료했습니다. 아래 표는 원래 이동 명세이며 B1~B4의 원본 경로는 이제 목적지로 변경됐습니다. B3는 [B3 보고서](DirectoryLayoutB3Report.md)에 기록한 대로 이동·정적 검증을 마쳤고 Unity 실행 검증은 대기합니다. B4 역시 [B4 보고서](DirectoryLayoutB4Report.md)에 기록한 대로 이동·정적 검증을 마쳤습니다. B3/B4의 Unity 실행 검증과 B5 통합 검증은 대기 상태입니다. 이번 실행에서 Git 작업은 하지 않았습니다.

## 1. 기준과 목적

파이프라인 정리 결과는 dev `229bbfeb3f8b4b0b680a5822c929e607860dfcf3`에서 분기한 `refactor/skill-pipeline-cleanup`의 `c9d47aac4b3e2ce0283d9ec7a5555c5df4bb04e1`에 커밋했습니다. 분기 전 fetch로 로컬/원격 dev가 동일함을 확인했습니다. push·PR·merge는 수행하지 않았습니다. Git에서 제외된 PlayGroundAssets의 검증 도구는 로컬에 보존했으며 강제 추가하지 않았습니다. 이 계획은 위 커밋 이후 작성한 검토용 문서입니다.

B의 목적은 공용 전투 기능을 PlayerScripts라는 이름에서 분리하고, 실행·상태·대상·이동·표시 파일을 책임에 맞게 찾을 수 있도록 배치하는 것입니다. B에서는 **C# 파일 내용·namespace·클래스명·assembly·직렬화 형식·게임 규칙을 바꾸지 않습니다.** namespace가 경로와 다른 과도기 상태는 의도된 것이며 C에서 별도로 처리합니다.

사람의 실제 플레이 테스트는 B/C 후 v0.2 최종 시험으로 진행하는 방향에 동의합니다. 현재 입력 자동화는 전투 계약의 근거가 되지만 화면 표현·실물 입력·체감 지연을 대신하지 못합니다. B/C의 참조·컴파일·직렬화 검증은 각 단계에서 수행하고, 최종 사람 시험만 모아서 진행합니다. C까지 진행한다면 최종 플레이는 C 이후가 적절합니다. B 뒤 릴리스를 결정한다면 그 시점에 수행합니다.

## 2. 불변 조건과 제외 범위

- 기존 `.cs`와 대응 `.meta`를 한 쌍으로 이동합니다. 파일명과 GUID, 본문 및 meta 내용의 해시를 보존합니다. 새 상위 폴더만 새 GUID를 갖습니다.
- Unity 컴포넌트 연결, SerializeReference의 namespace/class/assembly 문자열, 필드 이름, 상속·partial 관계를 유지합니다. 새 asmdef/asmref를 도입하지 않습니다.
- Editor 폴더 경계를 넘지 않습니다. Assets/Editor와 Assets/ExitLab의 기존 배치는 유지합니다.
- 코드 정리, 추가 파일 분리, 미사용 타입 삭제, private 이름 변경, Context 등록 순서 변경을 섞지 않습니다.
- PlayGroundAssets는 이동·삭제·정리하지 않습니다. 원본 씬/프리팹/기술 애셋의 일괄 재저장도 하지 않습니다.
- 이동 중 코드 변경이 꼭 필요한 경로 의존성이 발견되면 해당 묶음을 멈추고 근거를 보고합니다. 검증 스크립트/문서의 경로 수정과 런타임 동작 변경을 구분합니다.
- 새 경로로 문서 링크를 갱신하되 과거 로그·해시 목록·당시 경로를 담은 감사 기록은 역사적 자료로 보존합니다.

## 3. 단계별 이동표

아래 경로의 공통 접두사는 `Assets/Scripts/`입니다. 중괄호 목록은 명시한 파일만 뜻하며 폴더 전체를 무조건 옮기는 규칙이 아닙니다. 신규 파일이 발견되면 별도로 분류합니다. 각 파일의 정확한 대응은 함께 제공하는 `DirectoryLayoutBManifest.csv`를 기준으로 합니다.

계획 작성 시 명세 86개 원본과 대응 meta가 모두 존재하고, 목적지 기존 파일 및 원본/목적지 중복이 없음을 확인했습니다. Scripts C# 97개 중 명세에 없는 11개는 아래 유지 목록과 일치합니다. 이 사전 확인은 B0의 실제 이동 직전 GUID/해시·참조 검증을 대신하지 않습니다.

### B0 — 이동 기준 고정

1. 현재 사용자 변경, 진행 중인 Unity import/PlayMode, 승인된 기준 상태를 확인합니다. 기존 변경을 덮어쓰거나 stash/reset하지 않습니다.
2. 명세의 모든 원본 파일과 meta 존재, 목적지 중복·기존 파일 충돌, GUID 유일성을 확인합니다. `.cs`/`.meta` 해시와 완전한 타입 이름을 기록합니다.
3. 현재 컴파일 및 씬/프리팹/기술 애셋의 누락 참조·관리 참조 기준선을 수집합니다. 기존 누락을 이번 이동의 결함이나 복구 대상으로 혼동하지 않습니다.
4. 경로 문자열 검색 범위는 런타임, Editor/검사 코드, 설정, 빌드 도구와 현행 문서입니다. 이번 정적 검색에서는 Assets/Editor·ExitLab C#에 `Assets/Scripts` 등의 이전 소스 경로 문자열이 발견되지 않았지만 착수 때 재확인합니다.
5. 임시 검증 도구는 로컬 전용입니다. 다른 환경에서는 도구 존재와 동일 해시를 확인한 뒤 활용하거나 승인 범위 내에서 검증 사본에 재구성합니다.

### B1 — Casting와 Targeting (19개)

| 원본 | 목적지 |
|---|---|
| PlayerScripts/Acts/Attacker.cs | Combat/Casting/Attacker.cs |
| PlayerScripts/Skills/{CastCommit,CastPreparation,CommandCollector,CommandBridge,SkillRunner,SwitchSelection}.cs | Combat/Casting/동일 파일명 |
| Moves/{CastContext,SkillCommand,SkillUtils}.cs | Combat/Casting/동일 파일명 |
| PlayerScripts/Skills/{TargetResolver,RelativeTargetResolver,ExplicitAttackTarget}.cs | Combat/Targeting/동일 파일명 |
| Moves/{SkillTarget,TargetPolicy,RelativeHitQueries,TargetMaskVersionAttribute}.cs | Combat/Targeting/동일 파일명 |
| Systems/Anchor/{SkillAnchor,AnchorRegistry}.cs | Combat/Targeting/Anchors/동일 파일명 |

TargetResolver와 RelativeTargetResolver는 같은 partial 타입이므로 함께 이동합니다. SkillUtils는 실제 Chain 생성 책임 때문에 Casting에 둡니다. `Moves`와 `PlayerScripts.Skills` namespace를 그대로 유지하며 Resolver 연결이나 Collector 전역성은 변경하지 않습니다.

완료 기준: 동일 GUID로 이전 MonoScript가 새 경로에서 검색되고, Cast/Target/Origin/Trace 관련 검사와 명시 대상 입력 검증이 통과해야 합니다.

### B2 — 공용 Context·효과 수신·Stats·Stack (19개)

| 원본 | 목적지 |
|---|---|
| PlayerScripts/Core/Context.cs | Combat/Runtime/Context.cs |
| PlayerScripts/Core/InteractionFilter.cs | Combat/Interaction/InteractionFilter.cs |
| PlayerScripts/Stats/{StatsBridge,StatsContainer,StatsContainer.Buffs}.cs | Combat/Stats/동일 파일명 |
| PlayerScripts/Stack/{StackManager,StackTriggerExecutor,VariableStorage,VariableConsumption}.cs | Combat/Stacks/Runtime/동일 파일명 |
| Systems/Stacks/StackKey.cs | Combat/Stacks/Runtime/StackKey.cs |
| Systems/Stacks/Definition/{StackDefinition,BuffDefinition,CCDefinition,TriggerableDefinition,VariableDefinition}.cs | Combat/Stacks/Definitions/동일 파일명 |
| Systems/Stacks/ExclusiveGroups/ExclusiveGroup.cs | Combat/Stacks/Definitions/ExclusiveGroup.cs |
| Systems/Stacks/{StackStorage,StackRegistry,StackRegistryObject}.cs | Combat/Stacks/Registry/동일 파일명 |

StatsContainer partial 파일은 함께 이동합니다. StackStorage는 현재 정의 이름 조회 저장소이므로 Registry로 분류하되 타입·전역 수명·namespace는 유지합니다. Context 내부 Logger/ILogger, StackKey 내부 StackStatus는 분리하지 않습니다. `AllowEnemyDamage`의 객체 이름 기반 조회 등 기존 정책도 그대로 유지합니다.

완료 기준: Stack 정의 애셋 연결, CC/Buff/Trigger/Variable 소비·보상·파괴 정리, 입력에서 Switch와 Stack으로 이어지는 경로를 확인합니다. 기존 DR 절삭 재현은 별도 기록하고 통과로 세지 않습니다.

### B3 — 행동·이동·Entity·관계·임시 AI (20개)

| 원본 | 목적지 |
|---|---|
| PlayerScripts/Acts/ActBridge.cs | Combat/Actions/ActBridge.cs |
| PlayerScripts/Acts/Mover.cs | Movement/Mover.cs |
| Systems/SubSystems/{FixedMotor,ThinMotor,Teleporter}.cs | Movement/Physics/동일 파일명 |
| Systems/SubSystems/{TravelMotion,TravelPhysicsObserver}.cs | Movement/Travel/동일 파일명 |
| Moves/TravelExit.cs | Movement/Travel/TravelExit.cs |
| Systems/Data/Entity.cs | Entities/Core/Entity.cs |
| Systems/Data/EnemyEntity.cs | Entities/Enemy/EnemyEntity.cs |
| PlayerScripts/Core/{PlayerEntity,PlayerInfo}.cs | Entities/Player/동일 파일명 |
| Moves/ObjectEntity/{SummonEntity,SummonEntityBase}.cs | Entities/Summon/동일 파일명 |
| Systems/Data/EntityRelation.cs | Entities/Relations/EntityRelation.cs |
| Systems/SubSystems/{EntityRelationResolver,AllyEnemyChecker}.cs | Entities/Relations/동일 파일명 |
| Systems/SubSystems/{TemporaryEnemyBrain,TemporaryAttackSupport}.cs | Prototypes/Enemy/동일 파일명 |
| EnemyDummy.cs | Prototypes/Enemy/EnemyDummy.cs |

Prototypes는 런타임 코드의 위치이며 Editor 전용 또는 삭제 가능 표시가 아닙니다. EnemyEntity의 기존 임시 Trigger 예시도 유지합니다. Mover 내부 DashContract/TeleportContract는 그대로 둡니다.

완료 기준: Player/Enemy/Summon 스크립트 연결과 활성화·비활성화·파괴, AI 명시 대상, 실제 입력, Projectile/Dash 이동·충돌·Chain·씬 unload 검사가 통과해야 합니다. 원본 씬의 baseline 대비 새 Missing Script가 없어야 합니다.

### B4 — Mechanism·효과 객체·표시·공간·진단·입력 (28개)

| 원본 | 목적지 |
|---|---|
| Moves/Mechanisms/{Area,Damage,Dash,Detect,Dummy,Hitscan,Melee,Projectile,Ray,Stack,Summon,Switch,Teleport}Mechanism.cs | Combat/Mechanisms/동일 파일명 |
| Moves/SkillData.cs | Combat/Definitions/SkillData.cs |
| Moves/ObjectEntity/{ProjectileEntity,AreaEntity}.cs | Combat/Effects/Runtime/동일 파일명 |
| Moves/Effects/Definitions/{AreaEffectEntity,MeleeEffectEntity}.cs | Combat/Presentation/동일 파일명 |
| PlayerScripts/Skills/EffectDisplayer.cs | Combat/Presentation/EffectDisplayer.cs |
| Moves/{BasePosition,SpatialExecution}.cs | Combat/Spatial/동일 파일명 |
| Moves/{AttackTrace,AttackTraceSettings}.cs | Diagnostics/Runtime/동일 파일명 |
| Debuggers/{TargetAnchorDebug,MtvVisualizer}.cs | Diagnostics/Runtime/동일 파일명 |
| UIs/DebugOverlay.cs | Diagnostics/Runtime/DebugOverlay.cs |
| Utils/AimLine.cs | Diagnostics/Runtime/AimLine.cs |
| Utils/CursorResolver.cs | Input/CursorResolver.cs |

Combat/Definitions는 이번에는 SkillData 한 파일의 목적지가 됩니다. 혼합 Generals를 억지로 여기에 넣지 않습니다. CursorResolver가 Input에 있어도 플레이어/AI Target 정책을 합치지 않습니다. 표시 MonoBehaviour는 Definitions라는 기존 폴더명에서 분리하지만 해당 namespace는 그대로입니다.

완료 기준: SerializeReference의 모든 기존 타입 문자열 보존, Drawer/마이그레이션 도구 연결, 공간 설정·투사체·Area·표시 컴포넌트의 로드/참조 확인. 애셋을 재저장해서 문제를 숨기지 않습니다.

### 유지하는 11개

| 대상 | 유지 사유 |
|---|---|
| Systems/Time/{Time,Ticker,TickerRunner,DelayScheduler}.cs (4) | 이미 명확한 시간 서비스 경계 |
| Characters/CharacterSpec.cs, Systems/Data/FixedVector2.cs (2) | 기존 분류가 충분하며 필수 이동 아님 |
| Moves/Generals.cs, Systems/Data/Interfaces.cs, Moves/ObjectEntity/EntitySettings.cs (3) | 여러 책임의 타입이 섞인 파일. B에서 추가 분리/이름 변경하지 않으며 별도 파일 분리 설계로 이관 |
| Systems/SubSystems/AttackScheduler.cs (1) | 주석 처리된 과거 구현. 삭제/아카이브 정책 미승인 |
| UIs/SpriteManager.cs (1) | 해당 역할의 세부 재분류보다 현재 위치 유지가 적절 |

현재 Scripts의 97개 C# 중 86개 이동, 11개 유지하는 안입니다. 목표 배치에 모든 파일을 강제로 맞추는 것이 목적은 아닙니다. 기존 폴더가 비어도 같은 단계에서 임의로 정리하지 않습니다. 폴더 meta 삭제는 폴더 자체 GUID 참조 유무와 숨은 파일까지 확인한 뒤 승인된 정리 범위에서만 수행합니다.

## 4. 이동 절차와 실패 처리

각 묶음은 B0 기준 기록 → 파일/meta 쌍 이동 → import → 정적/참조 검사 → 관련 회귀 → 완료 보고 순서로 진행합니다. Unity AssetDatabase를 활용하거나 비실행 상태에서 검증된 명세에 따라 이동하되, 실제 적용 전에 원본/목적지 절대 경로가 저장소 내부인지 확인합니다. 대상 파일이 이미 있으면 덮어쓰지 않습니다.

다음 조건에서는 해당 묶음의 다음 이동을 중지합니다: 새 컴파일 오류, GUID 변경/중복, Missing Script/관리 참조 증가, assembly 식별 변화, 예상 밖 애셋 수정 또는 회귀 실패. 승인된 이동만 기록된 원래 경로로 되돌릴 수 있도록 역매핑을 보관합니다. 작업 트리 전체 reset/clean이나 사용자 변경 제거는 복구 수단으로 사용하지 않습니다.

Git 커밋은 요청 범위에 따릅니다. 단계별 완료를 커밋 자동 승인으로 해석하지 않습니다. 변경을 분리해 검토할 수 있도록 묶음별 이동 명세와 해시 결과는 남깁니다.

## 5. B5 최종 통합과 완료 기준

1. 명세 86개 모두 원본 경로에서 사라지고 목적지에서 같은 `.cs`/`.meta` 해시로 존재해야 합니다. 유지 11개와 제외 디렉터리에는 변경이 없어야 합니다. 새 폴더 meta는 별도 목록으로 설명합니다.
2. 완전한 타입 이름/assembly/공개 API/직렬화 필드가 동일해야 합니다. MonoScript GUID→경로만 변경되며 기존 애셋의 GUID 참조 문자열은 유지합니다.
3. 검증 사본에서 컴파일, 기존 애셋/씬/프리팹 로드 및 누락 참조 baseline 비교를 수행합니다. 본 단계는 namespace 이관이 아니므로 대량 SerializeReference 재작성이나 원본 저장은 예상하지 않습니다.
4. 최종 상태에서 Cast EditMode, CastLifecycle PlayMode, Stack 통합, Travel 물리 및 위임 입력 검사를 실행합니다. 단계별 결과와 최종 결과를 구분하고 결함 fingerprint는 정상 통과에서 제외합니다. 현재 기준은 각각 874/68, Stack 248+기준 24(결함 1 포함), Travel 96, 입력 30입니다. 검사 수가 바뀌면 이유를 설명합니다.
5. 현행 문서 링크와 경로 안내를 갱신합니다. 과거 보고서의 코드 줄 번호와 당시 경로는 역사적 근거로 유지하거나 명시적 매핑 링크를 추가합니다.
6. B 완료 보고서에 실제 이동/유지 목록, GUID/해시 결과, 검사 결과, 새로 발견한 문제, C 이관 목록을 포함합니다. P4 사람 시험을 수행한 것처럼 보고하지 않습니다.

## 6. C와 최종 사람 플레이 시험

C 착수 전에는 최상위 namespace 명칭, 구 타입→새 타입 대응, SerializeReference 이관, 경로/namespace 문자열에 의존하는 도구, 검증·복구 방법을 별도 계획으로 승인받습니다. 새 assembly 분리는 C에도 자동 포함하지 않습니다. B에서 남긴 혼합 파일의 추가 분리를 C와 함께 할지는 별도 결정입니다.

최종 사람 시험은 실제 Battlefield에서 키보드/마우스 입력과 슬롯, 빈 슬롯, 조준·이동·공격 체감, Delay/CC 취소, AI 대상, Switch/Chain, CC·Buff·Trigger, 화면 효과/표시, 씬 전환·재시작을 확인합니다. 최종 배포 형태가 정해지면 해당 빌드에서도 입력/해상도/성능을 점검합니다. 이 시험은 자동 검증이 다루지 않는 실제 장치·시각 표현·프레임 타이밍을 보완합니다.

계획 작성 당시에는 기존 작업의 브랜치/커밋과 계획 작성만 수행했습니다. 이후 사용자 승인으로 B0~B2를 완료했습니다. B3는 이동·정적 검증을 완료했으며 Unity import·참조 로드·회귀 검증은 미실행입니다. B4도 이동·정적 검증을 완료했으며 실행 검증은 대기합니다. B5 통합 검증은 미착수입니다. 사용자의 Unity 비실행 계획에 맞춰 이번에는 검증 사본의 batchmode도 실행하지 않았으며, B3의 원래 완료 기준은 향후 실행 검증까지 유지합니다. 묶음별 내용·GUID 보존과 관련 검증 기준은 계속 유지합니다.
