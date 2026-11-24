# B3 보고서 — 행동·이동·Entity 물리 이동

작성일: 2026-11-24 · 상태: 이동 및 정적 검증 완료, Unity 실행 검증 대기

## 범위와 결과

승인된 B3의 C# 20개와 대응 meta 20개를 [B 이동 명세](DirectoryLayoutBManifest.csv)의 목적지로 이동했습니다. B4와 namespace 개편 C는 수행하지 않았습니다. 커밋은 이번 이동에 필요하지 않아 Git 작업을 하지 않았습니다.

| 묶음 | 파일 수 | 목적지 (Assets/Scripts 아래) |
|---|---:|---|
| ActBridge | 1 | Combat/Actions |
| Mover | 1 | Movement |
| FixedMotor·ThinMotor·Teleporter | 3 | Movement/Physics |
| TravelMotion·TravelPhysicsObserver·TravelExit | 3 | Movement/Travel |
| Entity | 1 | Entities/Core |
| EnemyEntity | 1 | Entities/Enemy |
| PlayerEntity·PlayerInfo | 2 | Entities/Player |
| SummonEntity·SummonEntityBase | 2 | Entities/Summon |
| EntityRelation·EntityRelationResolver·AllyEnemyChecker | 3 | Entities/Relations |
| TemporaryEnemyBrain·TemporaryAttackSupport·EnemyDummy | 3 | Prototypes/Enemy |

새 폴더 12개와 각 meta를 생성했습니다: Combat/Actions, Movement, Movement/Physics, Movement/Travel, Entities, Entities/Core, Entities/Enemy, Entities/Player, Entities/Summon, Entities/Relations, Prototypes, Prototypes/Enemy. 기존 빈 폴더와 meta는 유지했습니다.

코드 본문·파일명·namespace·클래스·assembly 경계는 변경하지 않았습니다. EnemyEntity의 임시 Trigger 예시와 Prototypes 아래 코드의 런타임 역할도 유지합니다. 기존 이동 명세의 원본 경로는 역사적 대응표로 보존하고 조사 문서의 탐색 링크 2개만 새 경로로 갱신했습니다.

## 정적 검증

| 검사 | 결과 |
|---|---|
| 이동 20쌍 | 목적지 존재, 원본 경로 제거, 코드/meta SHA-256 모두 일치 |
| 이동 GUID | Assets 전체에서 20개 모두 유일 |
| 기존 파일 보존 | Assets·ProjectSettings·Packages 1,065개 파일 기준선에서 이동 대상 외 변경 0 |
| 추가 파일 | 이동 목적지 및 새 폴더 meta 외 예상 밖 추가 0 |
| PlayGroundAssets | 기존 186개 파일 내용 보존, 이동·삭제·정리 없음 |
| 경로 의존 검색 | Assets(PlayGroundAssets 제외)·ProjectSettings·Packages의 C#·JSON·asmdef/asmref·asset·unity·prefab에서 기존 전체 소스 경로 문자열 일치 0 |

참조를 가진 기존 애셋과 스크립트 meta가 동일하므로 GUID 참조 문자열과 식별자는 보존됐습니다. 이는 Unity가 실제로 import하거나 MonoScript/GetClass 및 직렬화 참조를 로드한 검증과는 구분합니다. 경로 검색은 전체 소스 경로의 슬래시/역슬래시 형태를 대상으로 했으며 동적으로 조합한 경로까지 증명하지 않습니다.

## 실행하지 않은 검증과 후속 확인

사용자의 Unity 에디터 비실행 계획에 맞춰 원본과 검증 사본 모두 Unity를 실행하지 않았습니다. 이동 전 Unity 프로세스가 없음을 확인했습니다. 이전 B2 검증 사본도 이번에는 변경하지 않았습니다.

B3의 원래 완료 기준은 아래 실행 검증까지이며, 현재는 이동과 정적 검증만 완료했습니다. 이전 B2의 통과 결과를 B3 결과로 재사용하지 않습니다. 명시적인 실행 요청이나 플레이 테스트 검증 시 최신 원본을 검증 사본에 반영한 뒤 다음을 확인해야 합니다.

1. 컴파일/import, MonoScript GUID→새 경로/GetClass 연결, 기존 씬·프리팹의 Missing Script 및 관리 참조 기준선 비교.
2. Cast 및 관련 EditMode(기존 기준 874), CastLifecycle PlayMode(68): Entity 수명과 대상·취소 계약.
3. Travel 물리 회귀(96): Projectile/Dash 이동·충돌·Chain·씬 unload.
4. Stack 주요 회귀(248)와 기준선, 입력 경로(30): CC에 의한 이동·공격 제한, Enemy/AI와 Switch·후속 실행.

기존 DR 절삭 결함은 별도 정책 항목이며 이번 작업에서 수정하거나 재검증하지 않았습니다. 사람의 직접 플레이는 합의한 v0.2 최종 확인 항목으로 남습니다.

## 근거와 다음 단계

작업 공간 audit-results/layout-b3/에 move-baseline.json, workspace-baseline.json, old-paths.txt, path-dependencies.json, new-folders.txt, move-verification.json, guid-uniqueness.json, unexpected-existing-changes.json, added-files.json, verification-summary.json을 보관했습니다. 원본/목적지 대응과 내용 해시는 이동을 추적하는 기준입니다.

현재 경로: [ActBridge](../Assets/Scripts/Combat/Actions/ActBridge.cs), [Mover](../Assets/Scripts/Movement/Mover.cs), [EnemyEntity](../Assets/Scripts/Entities/Enemy/EnemyEntity.cs), [TravelMotion](../Assets/Scripts/Movement/Travel/TravelMotion.cs).

이번 작업은 여기서 중지합니다. B4는 착수하지 않았으며, B3 실행 검증은 대기 상태로 남깁니다.
