# B0·B1 일괄 작업 보고서

작성일: 2026-11-24 · 범위: B0 기준 고정 + B1 Casting/Targeting 이동

## 수행 범위

사용자가 B0 및 필요 시 후속 단계의 일괄 작업을 허용하여 B1까지 진행했습니다. B2~B5, namespace 개편 C는 진행하지 않았습니다. 이번 작업 중 Git 명령·커밋·브랜치 변경·push·merge는 수행하지 않았습니다. 사용자가 원본 Unity를 닫아 두었다고 확인한 뒤 이동했습니다.

현재 작업 트리를 기준으로 했으며 P4에서 기록한 프로덕션/기존 검사 C# 142개 해시와 모두 일치했습니다. 기존 변경을 되돌리거나 사용자 파일을 덮어쓰지 않았습니다.

## B0 확인

- 전체 이동 명세 86개에서 원본·meta 존재, 목적지 충돌 없음, GUID 유일성을 확인했습니다. GUID 검색 범위는 PlayGroundAssets를 제외한 Assets입니다.
- 원본 .cs/.meta 해시, namespace, GUID를 `move-baseline.json`에 기록했습니다. 소스·Editor·검사·ObjectInstances·Prefabs·Scenes·ProjectSettings·Packages의 파일 해시도 보관했습니다.
- Scripts 아래 asmdef/asmref가 없음을 확인했습니다. 새 assembly 경계는 만들지 않았습니다.
- Editor/ExitLab·설정·패키지 검색에서 이동 대상의 기존 소스 경로를 요구하는 문자열 의존은 발견되지 않았습니다. 과거 문서의 당시 경로와 이동 명세는 기록으로 유지합니다.
- 원본 ObjectInstances/Prefabs/Scenes의 대상 19개를 사본에 반영하여 Unity에서 로드했습니다. 씬은 preview scene으로 열고 저장하지 않았습니다. 기존 unresolved GUID, Missing Script/Object reference, 누락 관리 참조 검사에서 발견된 항목은 0개였습니다. 이 결과는 해당 19개 범위이며 모든 프로젝트/외부 자산에 대한 보증은 아닙니다.

## B1 이동 결과

`DirectoryLayoutBManifest.csv`의 B1 19행 그대로 .cs와 대응 .meta를 이동했습니다. 모든 파일명·코드 내용·meta 내용·GUID를 보존했습니다.

| 새 위치 (`Assets/Scripts/` 기준) | 파일 |
|---|---|
| Combat/Casting (10) | Attacker, CastCommit, CastPreparation, CommandCollector, CommandBridge, SkillRunner, SwitchSelection, CastContext, SkillCommand, SkillUtils |
| Combat/Targeting (7) | TargetResolver, RelativeTargetResolver, ExplicitAttackTarget, SkillTarget, TargetPolicy, RelativeHitQueries, TargetMaskVersionAttribute |
| Combat/Targeting/Anchors (2) | SkillAnchor, AnchorRegistry |

신규 폴더는 Combat, Combat/Casting, Combat/Targeting, Combat/Targeting/Anchors 4개이며 각각 새 폴더 meta를 생성했습니다. 기존 상위 폴더나 그 meta는 삭제하지 않았습니다. B2 이후의 67개 예정 파일과 유지 대상 11개는 이동하지 않았습니다.

namespace는 `PlayerScripts.Acts`, `PlayerScripts.Skills`, `Moves`, `Systems.Anchor`를 그대로 유지합니다. 폴더명과 namespace의 불일치는 B 설계의 의도된 과도기 상태입니다. 추가 타입 분리, 생성자/메서드 수정, GUID 재발급, 애셋 재저장은 없습니다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| 이동 파일/메타 | 19쌍 모두 원본 경로 제거, 목적지 존재, 코드·meta 해시 동일 |
| 그 외 기존 파일 | 기록한 소스/애셋/설정 범위의 예상 밖 변경 0 |
| 스크립트 GUID/Unity GetClass 연결 | 이동 전후 차이 0. Unity 클래스가 아닌 타입의 기존 null 해석도 유지 |
| assembly 타입 비교 | 선언 타입의 완전명/assembly 차이 0 |
| 애셋 로드/관리 참조 기록 | 178개 고유 기록 전후 동일 |
| 누락 참조 | 대상 범위 전후 0개 |
| 컴파일 및 Cast EditMode 회귀 | 874개 정상 기대값 통과, exit 0 |
| 실제 입력 PlayMode 회귀 | 정상/빈 슬롯·이동·CC·AI·Switch·Stack 30개 통과, exit 0 |
| 검증 사본 일치 | 원본 Scripts/Editor/ExitLab C# 142개와 사본 해시 모두 동일 |

전체 assembly reflection 목록은 전후 339개입니다. 그중 컴파일러 생성 `<PrivateImplementationDetails>.__StaticArrayInitTypeSize`의 크기 표기가 8506에서 8545로 변경됐습니다. 원시 목록 차이를 숨기지 않고 보관했으며, 이 내부 컴파일 데이터 타입만 제외한 선언 타입 비교는 차이 0입니다. 이는 C# 본문/직렬화 타입 변경을 허용한 것이 아닙니다. 이동 대상 본문과 namespace는 바이트 해시로 별도 검증했습니다.

Unity 캐시 초기화의 기존 YamlDotNet 경로 예외 및 의도적인 예외 격리 검사 로그는 전체 로그에 포함될 수 있습니다. 실행 종료 코드와 개별 검사 결과를 구분하며 전체 로그 무오류라고 표현하지 않습니다. 원본 씬의 사람 플레이는 하지 않았고, 합의대로 v0.2 최종 시험으로 남깁니다.

## 근거와 현재 경로 안내

작업 공간 `audit-results/layout-b0-b1/`에 다음을 보관했습니다.

- `move-baseline.json`, `workspace-baseline.json`, `guid-uniqueness.json`: 이동 전 기준.
- `layout-before.json`, `layout-after.json`, `unity-snapshot-comparison.json`, `declared-type-comparison.json`: Unity 로드/타입/참조 전후 비교. 원시 타입 차이와 선언 타입 비교를 구분.
- `move-verification.json`, `unexpected-existing-changes.json`, `source-copy-sha256.json`: 이동·내용 보존 및 사본 일치.
- `b0-audit.log`, `b1-audit-regression.log` 및 종료 코드, 관련 검사 결과 txt.
- `b1-input.log`, `b1-input-exit.txt`, `b1-input-results.txt`: 입력 PlayMode 검사.

과거 보고서의 파일 경로/줄 번호는 당시 기록입니다. 현재 탐색은 [B 이동 명세](DirectoryLayoutBManifest.csv)의 B1 목적지를 사용합니다. 주요 진입점은 [Attacker](../Assets/Scripts/Combat/Casting/Attacker.cs), [CastContext](../Assets/Scripts/Combat/Casting/CastContext.cs), [SkillCommand](../Assets/Scripts/Combat/Casting/SkillCommand.cs), [TargetResolver](../Assets/Scripts/Combat/Targeting/TargetResolver.cs)입니다. B2 이후 행의 목적지는 아직 적용되지 않았습니다.

## 다음 범위

B0/B1을 완료하고 중지합니다. 다음 후보는 B2의 공용 Context·효과 수신·Stats·Stack 19개 이동입니다. 완료된 작업을 검토한 뒤 진행 범위를 정할 수 있도록 B2를 자동으로 시작하지 않습니다. PlayGroundAssets는 기존 위임 검증 도구를 읽어 사용했을 뿐 이동·삭제·수정하지 않았습니다.
