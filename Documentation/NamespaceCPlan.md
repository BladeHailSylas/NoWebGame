# C 작업 계획 — namespace 정리와 직렬화 이관

작성일: 2026-11-24 · 상태: C0~C5 완료(C3+C4 통합), 최종 통합 검증 완료

진행 기록: [C0 보고서](NamespaceC0Report.md)의 현재 로컬 B 최종 상태 검증을 완료했습니다. Git 확인은 최신 사용자 지시에 따라 제외했습니다. 아래의 B3/B4 검증 대기 설명은 계획 작성 당시 상태이며 현재는 C0 결과로 대체합니다. 사용자가 후속 단위를 C1 → C2 → C3+C4 → C5로 확정했고 C1을 승인했습니다. [C1 보고서](NamespaceC1Report.md) 및 [타입 manifest](NamespaceCTypeManifest.csv)를 최신 적용 기준으로 사용합니다. 커밋은 명시적인 요청 전에는 수행하지 않습니다.

## 1. 결론과 확정 사항

프로젝트 소유 런타임 타입에 NoWebGame 접두사를 적용하고, B에서 정리한 책임별 디렉터리에 namespace를 맞춥니다. assembly 분리, 클래스명 변경, 추가 파일 분리, 실행 로직 개선은 포함하지 않습니다. namespace 개편을 의존성 구조 개선과 동일시하지 않습니다.

사용자가 확정한 정책:

- 최상위 namespace는 NoWebGame을 사용합니다.
- PlayGroundAssets는 필요한 using·타입 참조·직렬화 식별자 이관만 허용합니다. 테스트 동작·수치·파일 배치·GUID 변경과 정리는 제외합니다.
- B 작업물의 커밋 및 원격 push는 사용자가 완료했다고 전달했습니다. 이번에는 Git 조회를 하지 않았으므로 커밋 ID·원격 일치 여부를 독립 확인하지 않았습니다.

현재 B3/B4는 이동 및 정적 검증만 완료했고 Unity import·실행 검증은 대기합니다. 원격 백업은 복구 지점이지 실행 정상성을 증명하는 근거는 아닙니다. **C의 첫 단계는 B 최종 상태 검증이며, 이를 통과하기 전에 namespace를 바꾸지 않습니다.** Unity 실행은 별도의 명시적 실행 승인 후 진행합니다. 이번 계획 작성 중 Unity·Git·코드·애셋 변경은 하지 않았습니다.

## 2. 실제 코드에서 확인한 위험

| 근거 | 의미 및 대응 |
|---|---|
| [SkillData](../Assets/Scripts/Combat/Definitions/SkillData.cs)의 MechanismData, [Generals](../Assets/Scripts/Moves/Generals.cs)의 INewMechanism SerializeReference | using 수정만으로 저장된 타입 이름이 이관되지는 않음. MechanismData와 구체 Mechanism 전체를 매핑 |
| SkillData 1~5 및 기본 SkillData 애셋의 class/ns/asm 기록 | Moves.MechanismData, Moves.Mechanisms.*와 Assembly-CSharp가 저장됨. 중첩 Chain/Switch 데이터와 공유 참조까지 보존해야 함 |
| [AreaEntity](../Assets/Scripts/Combat/Effects/Runtime/AreaEntity.cs), AreaEntity/AreaEntityBox 프리팹 | CircleArea·BoxArea도 Moves.ObjectEntity 이름으로 저장됨. Mechanism만 검사하면 누락 |
| [EntitySettings](../Assets/Scripts/Moves/ObjectEntity/EntitySettings.cs) | ObjectPrefab·Shape 계약/구현이 한 파일에 혼재. C에서 분리하지 않되 파일 내 모든 타입을 목록화 |
| [TargetMaskMigration](../Assets/Editor/TargetMaskMigration.cs): Type.GetType 및 Moves.Mechanisms 문자열/정규식 | 새 타입뿐 아니라 옛 YAML 입력도 판별하도록 호환 해석 필요. namespace 이관과 TargetMask 정책 이관은 별도 실행 |
| [SerializeReferenceDrawerBase](../Assets/Editor/SerializeReferenceDrawerBase.cs), [Drawers](../Assets/Editor/SerializeReferenceDrawers.cs) | 타입 탐색·정렬·구체 Drawer 연결을 확인. 이름 변경 후 목록 중복/누락 검사 |
| 씬/프리팹/정의 애셋의 m_EditorClassIdentifier | GUID 보존 외에 저장된 완전명도 조사·비교 필요. 필드 의미를 추정해 일괄 치환하지 않음 |
| Context의 Logger/ILogger, BasePosition의 여러 값 타입, SwitchMechanism의 SwitchCase | 파일 하나가 타입 하나가 아님. 파일 매핑과 타입 매핑을 구분 |
| PlayGroundAssets/PipelineDelegatedValidation/Editor/DelegatedInputChecks.cs | 옛 namespace import가 실제 존재. 원본을 방치하면 프로젝트 컴파일도 막을 수 있음 |

현재 ProjectVersion은 Unity 6000.3.10f1입니다. Assets 아래 asmdef/asmref는 정적 검색에서 발견하지 못했습니다. 패키지 assembly까지 없다는 뜻은 아닙니다. 이 계획은 Assembly-CSharp / 기존 Editor assembly 경계를 유지합니다.

## 3. 목표 namespace

아래 표는 파일별 목표 규칙입니다. 같은 옛 namespace가 여러 목적지로 갈라지므로 namespace 문자열 전체 치환은 금지합니다. C1에서 실제 타입별 완전명 매핑을 작성합니다.

| 현재 물리 위치 (Assets/Scripts 아래) | 목표 namespace |
|---|---|
| Combat/Casting | NoWebGame.Combat.Casting |
| Combat/Targeting, Targeting/Anchors | NoWebGame.Combat.Targeting, NoWebGame.Combat.Targeting.Anchors |
| Combat/Spatial, Mechanisms, Definitions | NoWebGame.Combat.Spatial, NoWebGame.Combat.Mechanisms, NoWebGame.Combat.Definitions |
| Combat/Runtime, Actions, Interaction, Stats | NoWebGame.Combat.Runtime, NoWebGame.Combat.Actions, NoWebGame.Combat.Interaction, NoWebGame.Combat.Stats |
| Combat/Stacks/Runtime, Definitions, Registry | NoWebGame.Combat.Stacks.Runtime, NoWebGame.Combat.Stacks.Definitions, NoWebGame.Combat.Stacks.Registry |
| Combat/Effects/Runtime, Presentation | NoWebGame.Combat.Effects.Runtime, NoWebGame.Combat.Presentation |
| Movement, Movement/Physics, Movement/Travel | NoWebGame.Movement, NoWebGame.Movement.Physics, NoWebGame.Movement.Travel |
| Entities/Core, Player, Enemy, Summon, Relations | NoWebGame.Entities.Core, NoWebGame.Entities.Player, NoWebGame.Entities.Enemy, NoWebGame.Entities.Summon, NoWebGame.Entities.Relations |
| Prototypes/Enemy | NoWebGame.Prototypes.Enemy (현재 전역 타입인 EnemyDummy 포함) |
| Input, Diagnostics/Runtime | NoWebGame.Input, NoWebGame.Diagnostics.Runtime |
| Systems/Time, Systems/Data/FixedVector2.cs | NoWebGame.Systems.Time, NoWebGame.Systems.Data |
| Characters, UIs/SpriteManager.cs | NoWebGame.Characters, NoWebGame.UIs |

혼합 파일은 별도 예외로 고정합니다. Moves/Generals.cs는 모든 현행 타입을 NoWebGame.Moves, Moves/ObjectEntity/EntitySettings.cs는 NoWebGame.Moves.ObjectEntity, Systems/Data/Interfaces.cs는 NoWebGame.Systems.Data로 옮깁니다. 이는 혼합 책임을 승인하는 최종 설계가 아니라 추가 분리를 C와 결합하지 않기 위한 과도기 규칙입니다. MechanismData/NewMechanism 이름도 유지합니다. 후속 분리는 별도 승인 대상으로 기록합니다.

주석 처리된 AttackScheduler는 활성 타입 이관 대상으로 세지 않고 내용도 유지합니다. Assets/Editor의 Editor namespace와 ExitLab/검증 전용 namespace 자체는 유지하되 런타임 타입 참조만 갱신합니다. 프로젝트 전체에 접두사를 강제하기 위해 검증 도구까지 재명명하지 않습니다.

부분 클래스 StatsContainer 및 TargetResolver는 각 선언을 같은 namespace로 함께 옮깁니다. 중첩 타입은 소유 타입을 따라가며 매핑에 기록합니다. NoWebGame.Input / Systems.Time / Logger 등 이름 해석 충돌은 UnityEngine.Input, UnityEngine.Time, 명시적 alias 또는 global:: 완전명으로 해소합니다. 충돌을 이유로 타입 이름이나 동작을 바꾸지 않습니다.

## 4. 이관 방법과 데이터 보존

권장 방식은 **타입별 호환 매핑 + 검증된 저장 식별자 이관**입니다. 구체 API 사용법과 지원 범위는 C1에서 설치된 Unity 버전의 API/문서 및 사본 실험으로 확인합니다. MovedFrom은 후보 수단이며 속성 하나로 모든 참조가 복원된다고 가정하지 않습니다. FormerlySerializedAs는 이번 namespace 이관의 대체 수단으로 취급하지 않습니다.

1. 타입 manifest에는 파일/GUID, old/new 완전명, assembly, 중첩·generic 여부, UnityEngine.Object 여부, SerializeReference 실사용, 이관 방식과 단계, 소비 도구를 기록합니다. null/미지원/미해결 타입을 숨기지 않습니다.
2. 타입 변경 전에 기존 애셋을 로드하여 기준 스냅샷과 원문 백업을 확보합니다. YAML의 class/ns/asm 외에 rid·참조 그래프·null·리스트 순서·필드값·Unity GUID/fileID를 기록합니다.
3. 별도 검증 사본에서 대표 객체/중첩 Chain·Switch·AreaShape를 먼저 이관합니다. Unity 객체의 GUID 연결과 관리 참조 복원은 따로 검사합니다. 구 이름을 쓰는 새 테스트 fixture로 과거 자산 복원도 확인합니다.
4. 자동 호환으로 충분한 범위와 명시적 저장 식별자 갱신이 필요한 범위를 실험 후 확정합니다. 필요한 YAML 갱신은 승인 manifest에 있는 class/ns/asm 또는 확인된 m_EditorClassIdentifier만 대상으로 dry-run합니다. 전역 문자열 치환, 전체 자산 강제 재저장, TargetMask 수치 변환은 금지합니다.
5. 파일별 변경 전 해시를 재확인한 뒤 적용합니다. 미지원 형식·중복 키·미매핑 타입·예상 밖 값 변경이 하나라도 있으면 해당 적용 단위를 중단합니다. 저장 후 재시작·재로드까지 성공해야 완료입니다.
6. 원래 누락 타입과 이번에 생긴 누락을 구분합니다. 누락 관리 참조를 null로 저장하거나 삭제해서 통과시키지 않습니다. 참조 ID가 도구에 의해 재배정된다면 그래프 동등성을 입증하고 이유를 보고하며, 기본은 ID 보존입니다.

TargetMaskMigration은 구/신 타입 식별자를 명시적 허용 매핑으로 해석하도록 갱신합니다. 새 namespace만 인식하도록 옛 문자열을 지우면 과거 입력 검사 기능을 잃으므로, 기존 입력·새 입력·혼합·미지원 입력을 모두 시험합니다. 이 도구의 실제 TargetMask 적용 작업은 C에 포함하지 않습니다.

PlayGroundAssets에도 같은 dry-run·해시·값 보존 규칙을 적용합니다. 필요한 참조 이관 외 수정은 하지 않습니다. Git에서 제외된 파일은 원격 백업에 포함됐다고 가정하지 않으며 로컬 백업과 별도 변경 목록을 유지합니다. 강제 추가하지 않습니다.

## 5. 단계별 실행 계획과 종료 기준

| 단계 | 작업 | 완료 기준 / 정지 지점 |
|---|---|---|
| C0 — B 기준 확정 | 승인된 읽기 전용 Git 확인으로 사용자가 올린 기준 commit/remote 기록, 미커밋 변경 분리. Unity 실행 승인 후 B3/B4·B5 통합 검증 | B 최종 상태의 컴파일·참조·회귀 통과. 실패는 B 문제로 분리하고 C 코드 변경 금지 |
| C1 — 타입 manifest와 이관 실험 | 모든 활성 타입/호출부/문자열/자산 참조 목록, 목표 완전명 고정. 검증 사본에서 호환 속성·명시적 식별자 이관 및 되돌리기 실험 | 구→신 매핑 충돌 0, 대표 자산 재로드 및 역복구 성공. 실제 적용 파일과 diff를 보고한 후 정지 |
| C2 — 비직렬화 실행 타입 우선 | C1에서 비직렬화로 확인한 Cast·Context·Stack runtime·Stats 등과 모든 소비 코드 import/완전명 수정. partial 동시 변경 | 컴파일 및 관련 Cast/Stack 회귀, 허용하지 않은 필드/API/실행문 변경 0. 런타임 타입이라는 이유만으로 자산 미사용을 추정하지 않음 |
| C3+C4 — Unity 객체·관리 참조 통합 | C1 manifest의 104개 타입/50개 파일, 해당 자산 식별자와 Drawer·TargetMaskMigration을 함께 이관 | GUID/GetClass, 관리 참조 그래프·수치·공유/null, 구/신 fixture와 저장·재시작·재로드, 관련 회귀 통과 |

| C5 — 최종 통합 | 전체 변경 및 잔존 구 이름 허용 목록 확인, 최종 회귀·보고 | 매핑 누락 0, 허용 외 변경 0, 아래 전체 검증 통과. 사람 시험은 별도 결과로 기록 |

위 묶음은 C1의 실제 타입 의존 결과에 따라 최소한으로 조정할 수 있습니다. 서로 순환 참조하는 타입의 선언을 반드시 동시에 옮겨야 하는 것은 아니며 소비 using을 함께 수정해 각 단계가 컴파일되도록 합니다. C3와 C4는 사용자 결정에 따라 하나의 적용 단위로 묶습니다. C5는 별도 단위로 유지합니다. 승인받은 단계 이후로 자동 진행하지 않습니다. 단계 완료 시 보고하고 대기하며 Git 커밋·push는 별도 요청 범위만 따릅니다.

## 6. 보존 계약과 검증 기준

- Cast: Target/Origin/Destination의 의미, 입력 시점 캡처, 루트 Mana/CD 확정·보상, Pending 취소·파괴·예외 경계 유지.
- Stack: Key별 Buff 총량 기여, CC 활성 제한, Trigger 준비 실패 보존/재시도·전체 소비·재진입, payload 수명 유지. 직접 실행에 루트 비용/Pending을 도입하지 않음.
- Variable/Switch: 선택·소비 receipt·실패 보상·후속 Chain·Consume=false 및 기본값 유지.
- 수치·enum 값·필드명·직렬화 기본값·private reflection 대상·공개 API 타입의 구조 유지. 기존 DR 절삭·영구 시간 sentinel·비활성 적용·적용자 식별 정책은 변경하지 않음.

실행 검증의 기존 기준은 Cast EditMode 874, CastLifecycle PlayMode 68, Stack 주요 248 및 별도 기준선(정상 23 + 결함 재현 1), Travel 96, 입력 30입니다. 새 이관 검사는 별도 집계하며 기존 수가 달라지면 이유를 보고합니다. 전체 로그 무오류와 개별 검사의 성공을 혼동하지 않습니다.

추가 이관 검사: 모든 타입 매핑의 실재성/유일성, 새 이름에서 Drawer 선택과 인스턴스 생성, SerializeReference 구 이름 복원, 중첩/공유/null 그래프 왕복, 원본에서 사본으로의 재현, Editor 재시작 후 캐시 없는 타입 로드, 적용 2회차 무변경, 미지원 입력 중단, 일부 적용 실패의 백업 복원. 자동 검증은 실물 장치·화면·체감 검증을 대신하지 않습니다.

## 7. 실패 처리와 복구

원격 B 커밋 외에 무시된 PlayGroundAssets와 미커밋 사용자 변경까지 포함하는 로컬 파일별 백업을 먼저 확보합니다. C 변경 대상만 전후 해시·역매핑으로 추적합니다. 새 컴파일 오류, 타입 누락, 참조 그래프/값 차이, GUID 변경, 예상 밖 재저장이 발생하면 다음 단계와 원본 저장을 중단합니다.

소스 namespace만 되돌리고 신 이름 자산을 남겨 두지 않습니다. 해당 단위의 코드·도구·애셋을 한 세트로 복구하고 재로드 검증합니다. 복구 전에 현재 해시를 확인하여 사용자 후속 편집이 있으면 덮어쓰지 않고 충돌로 보고합니다. 작업 트리 reset/clean이나 원격 강제 push를 복구 수단으로 사용하지 않습니다. 호환 속성/구 이름 매핑 제거는 v0.2 완료 시 자동 수행하지 않으며 이후 별도 정책으로 결정합니다.

## 8. 승인 시 확인할 범위

NoWebGame 접두사와 PlayGroundAssets의 필요한 참조 이관은 확정됐습니다. 나머지 권장안은 이 계획의 구현 승인 대상입니다: 혼합 파일의 과도기 namespace, Editor/ExitLab namespace 유지, assembly·파일 분리 제외, 명시적 자산 식별자 이관과 구 이름 호환 유지.

C0~C2를 완료했습니다. C1에서는 타입 manifest와 사본 이관·재로드·복구를 검증했고, C2에서는 타입 93개와 소비 참조를 적용했습니다. 다음 작업 단위는 C3+C4이며 별도 승인 후 착수합니다. Git 작업은 최신 지시에 따라 제외합니다. 원본 자산 이관은 C1 실험 결과와 변경 명세를 확인한 후 C2 이후 승인 범위에 따라 적용합니다. 이번 계획만으로 코드·애셋 변경, Unity 실행, branch 생성 또는 Git 쓰기를 시작하지 않습니다.

C1에서 보완한 검증 절차: 사본에는 원본 파일의 내용뿐 아니라 파일 집합도 대조합니다. 누적 검증 사본의 오래된 애셋을 원본 복사만으로 남겨 두지 않고 격리하며, 원본 전체 meta GUID를 import 이후에도 비교합니다. 사본 전용 도구 역시 변경·복구 manifest에 포함합니다.

C2 진행 기록(2026-11-24): [C2 완료 보고서](NamespaceC2Report.md)에 따라 93개 타입을 이관하고 104개 타입의 기존 이름 유지를 검증했습니다. 원본 C# 107개를 수정했으며 애셋/meta/Git은 변경하지 않았습니다. 다음 원본 Unity 실행 시 import·컴파일이 필요합니다.

C3+C4 진행 기록(2026-11-24): [C3+C4 완료 보고서](NamespaceC34Report.md)에 따라 나머지 104개 타입 및 저장 식별자 이관을 통합 완료했습니다. 사용자가 허용한 Serializable 추가는 구체 Mechanism 7종과 Shape 3종에 적용했습니다. 검증된 기존 코드 124개·애셋 111개 및 새 코드/meta 4개를 원본에 반영했습니다. 앞선 C3+C4 승인 대기 문구는 역사적 기록이며 이 완료 기록으로 대체합니다. C5는 미착수이며 별도 승인을 기다립니다. Git 작업과 원본 Unity 실행은 하지 않았습니다.

C5 완료 기록(2026-11-25): [C5 최종 보고서](NamespaceC5Report.md)에 따라 현재 원본과 C3+C4 결과의 일치, 잔존 구 이름 허용 목록, GUID·직렬화·최종 회귀 검증을 완료했습니다. 사용자의 원본 import·컴파일 1회 수행 보고를 기록했으며 C5에서는 코드·애셋·Git을 변경하지 않았습니다. 앞선 C5 미착수·승인 대기 설명은 역사적 기록이며 본 완료 기록으로 대체합니다. C0~C5 작업을 종료합니다.
