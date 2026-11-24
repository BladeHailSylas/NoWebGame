# B4 보고서 — Mechanism·효과·표시·공간·진단·입력 이동

작성일: 2026-11-24 · 상태: 이동 및 정적 검증 완료, Unity 실행 검증 대기

## 수행 범위

승인된 B4 C# 28개와 대응 meta 28개를 [B 이동 명세](DirectoryLayoutBManifest.csv)의 목적지로 이동했습니다. 코드 내용·파일명·namespace·클래스·직렬화 형식·assembly 경계를 변경하지 않았습니다. Git 작업은 수행하지 않았으며 사용자가 직접 검토 후 커밋할 예정입니다.

| 묶음 | 수 | 목적지 (Assets/Scripts 아래) |
|---|---:|---|
| Mechanism 13종 | 13 | Combat/Mechanisms |
| SkillData | 1 | Combat/Definitions |
| ProjectileEntity·AreaEntity | 2 | Combat/Effects/Runtime |
| AreaEffectEntity·MeleeEffectEntity·EffectDisplayer | 3 | Combat/Presentation |
| BasePosition·SpatialExecution | 2 | Combat/Spatial |
| AttackTrace·AttackTraceSettings·TargetAnchorDebug·MtvVisualizer·DebugOverlay·AimLine | 6 | Diagnostics/Runtime |
| CursorResolver | 1 | Input |

새 폴더와 meta 9쌍을 만들었습니다: Combat/Mechanisms, Combat/Definitions, Combat/Effects, Combat/Effects/Runtime, Combat/Presentation, Combat/Spatial, Diagnostics, Diagnostics/Runtime, Input. 기존 빈 폴더와 meta는 유지했습니다. 기존 런타임·Editor 경계 및 표시 클래스의 namespace도 유지합니다.

## 정적 검증 결과

| 검사 | 결과 |
|---|---|
| 이동 28쌍 | 목적지 존재, 원본 경로 제거, 코드/meta SHA-256 모두 일치 |
| 이동 GUID | Assets 전체에서 28개 모두 유일 |
| 기존 파일 보존 | Assets·ProjectSettings·Packages 1,077개 파일 기준선에서 이동 대상 외 변경 0 |
| 추가 파일 | 이동 목적지 및 새 폴더 meta 외 예상 밖 추가 0 |
| PlayGroundAssets | 기존 186개 파일 내용 보존, 이동·삭제·정리 없음 |
| 기존 전체 소스 경로 검색 | Assets(PlayGroundAssets 제외)·ProjectSettings·Packages의 C#·JSON·asmdef/asmref·asset·unity·prefab에서 일치 0 |
| 문서 탐색 링크 | B4 원본 경로를 직접 가리키는 상대 Markdown 링크 없음 |

경로 검색은 슬래시/역슬래시 형태의 전체 원본 경로를 대상으로 했습니다. 동적으로 조합한 경로의 부재까지 증명하지는 않습니다. 과거 보고서의 당시 경로와 이동 명세는 역사적 기록으로 유지합니다.

기존 애셋과 스크립트 meta의 바이트 보존으로 GUID 참조 문자열과 SerializeReference 저장 내용을 유지했습니다. 이는 Unity의 실제 import·타입 로드·Drawer 연결 성공을 확인한 결과와는 구분합니다.

## 미실행 검증

사용자의 Unity 비실행 계획을 유지하여 원본과 검증 사본 모두 Unity를 실행하지 않았습니다. 이동 전 Unity 프로세스가 없음을 확인했으며 기존 검증 사본은 갱신하지 않았습니다. B2의 이전 통과 결과를 이번 결과로 집계하지 않습니다.

B3/B4 실행 검증과 B5 최종 통합은 대기 상태입니다. 명시적인 실행 요청 후 최신 원본을 검증 사본에 반영하고 다음을 확인해야 합니다.

1. 컴파일/import 및 MonoScript GUID→새 경로/GetClass, 기존 씬·프리팹·기술 애셋의 Missing Script/관리 참조 기준선 비교.
2. SkillData와 Mechanism SerializeReference, Drawer·마이그레이션 도구, Projectile·Area·표시 컴포넌트 로드 및 공간 설정 연결.
3. 최종 상태의 Cast EditMode(기존 기준 874), CastLifecycle PlayMode(68), Stack 주요 회귀(248 및 별도 기준선), Travel(96), 입력 경로(30).
4. B5에서 전체 이동 86개와 유지 11개를 기준선에 대조하고 통합 완료 여부 판정.

기존 DR 절삭 문제는 별도 미해결 정책 항목입니다. 이번에 수정하거나 재검증하지 않았습니다. 사람의 최종 플레이 시험 역시 수행하지 않았습니다. 따라서 현재 상태를 B 전체 검증 완료로 해석하지 않습니다.

## 근거와 커밋 제안

작업 공간 audit-results/layout-b4/에 move-baseline.json, workspace-baseline.json, old-paths.txt, path-dependencies.json, new-folders.txt, move-verification.json, guid-uniqueness.json, unexpected-existing-changes.json, added-files.json, verification-summary.json, document-links.json을 보관했습니다.

현재 경로: [SkillData](../Assets/Scripts/Combat/Definitions/SkillData.cs), [ProjectileMechanism](../Assets/Scripts/Combat/Mechanisms/ProjectileMechanism.cs), [SpatialExecution](../Assets/Scripts/Combat/Spatial/SpatialExecution.cs), [EffectDisplayer](../Assets/Scripts/Combat/Presentation/EffectDisplayer.cs), [CursorResolver](../Assets/Scripts/Input/CursorResolver.cs).

아직 커밋하지 않은 B1~B4 물리 배치 변경과 관련 문서를 함께 검토하여 커밋할 때의 권장 메시지:

    refactor(layout): reorganize runtime scripts while preserving namespaces and GUIDs

Git 커밋·push는 실행하지 않았습니다. B4 이동과 정적 검증을 마친 상태에서 중지하며 B5 및 C는 시작하지 않습니다.
