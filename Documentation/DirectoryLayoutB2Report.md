# B2 완료 보고서 — 공용 상태와 Stack 물리 이동

작성일: 2026-11-24 · 범위: B2만 완료, B3/B4 미착수

## 수행 내용

사용자의 B2 승인과 원본 Unity 종료 확인 후, [이동 명세](DirectoryLayoutBManifest.csv)의 B2 C# 19개 및 대응 meta 19개를 함께 이동했습니다. B3/B4를 함께 이동해야 하는 경로 의존성은 발견되지 않아 B2만 수행했습니다. 커밋을 포함한 Git 작업은 하지 않았습니다.

| 묶음 | 수 | 목적지 (Assets/Scripts 아래) |
|---|---:|---|
| Context | 1 | Combat/Runtime |
| InteractionFilter | 1 | Combat/Interaction |
| StatsBridge·StatsContainer 및 Buff partial | 3 | Combat/Stats |
| StackManager·StackTriggerExecutor·VariableStorage·VariableConsumption·StackKey | 5 | Combat/Stacks/Runtime |
| Stack 정의 5종·ExclusiveGroup | 6 | Combat/Stacks/Definitions |
| StackStorage·StackRegistry·StackRegistryObject | 3 | Combat/Stacks/Registry |

새 폴더 7개와 대응 meta를 만들었습니다. 기존 코드와 meta는 바이트 단위로 보존했으며 파일명·namespace·assembly·직렬화 계약을 변경하지 않았습니다. 기존 빈 폴더와 meta는 유지했습니다. 원본 PlayGroundAssets를 수정·삭제·이동하지 않았고 씬·프리팹을 재저장하지 않았습니다. 조사 문서의 현재 경로 링크 2개와 계획/설계 진행 기록을 갱신했습니다.

## 검증 결과

검증 사본에서 Unity 6000.3.10f1로 실행했습니다. 원본 Scripts/Editor/ExitLab C# 142개와 사본의 해시가 모두 일치합니다.

| 검사 | 결과 |
|---|---|
| 이동 19쌍 | 원본 경로 제거, 목적지 존재, 코드/meta 해시 모두 동일 |
| 이동 GUID | 19개 모두 유일, Unity 스크립트 GUID/GetClass 연결 차이 0 |
| 그 외 기존 파일 | 기록한 소스·애셋·설정 범위에서 예상 밖 변경 0 |
| 선언 타입/assembly | 전후 차이 0 |
| 애셋 로드·직렬화 참조 | 기록 178개 전후 동일, 누락 참조 전후 0 |
| Cast 및 관련 EditMode | 정상 기대값 874개 통과, exit 0 |
| Stack PlayMode | Lifecycle 23, Trigger 46, Buff 78, Transition 101: 합계 248개 통과, exit 0 |
| Stack 기존 기준선 | 정상 기대값 23개 통과, 기존 DR 절삭 결함 fingerprint 1개 재현 |
| 실제 입력 경로 PlayMode | 정상/빈 슬롯·이동·CC 취소·AI·Switch·Buff·Trigger 30개 통과, exit 0 |

Stack 기준선의 DR-separate-truncation은 기존 결함 관찰 검사이며 해결된 정상 동작으로 집계하지 않았습니다. 이번 물리 이동에서 수치 정책은 변경하지 않습니다.

전체 타입 목록에서는 컴파일러 생성 <PrivateImplementationDetails>+__StaticArrayInitTypeSize가 8545에서 8554로 달라졌습니다. 원시 비교 결과를 보관했으며 해당 컴파일러 내부 타입만 제외한 선언 타입 비교는 차이 0입니다. 이동 코드의 동일성은 별도 파일 해시로 확인했습니다.

Unity 캐시/설정의 기존 경고와 예외 주입 테스트 로그가 포함되므로 전체 로그가 무오류라고 주장하지 않습니다. 개별 검사와 종료 코드를 확인했습니다. 사람의 직접 플레이는 하지 않았으며 합의대로 B/C 이후 v0.2 최종 확인으로 남깁니다.

## 근거

작업 공간의 audit-results/layout-b2/에 보관했습니다.

- move-baseline.json, workspace-baseline.json: 이동 전 기준.
- move-verification.json, guid-uniqueness.json, unexpected-existing-changes.json, source-copy-sha256.json: 내용 보존 및 검증 사본 일치.
- layout-before.json, layout-after.json, snapshot-comparison.json: Unity 로드·타입·참조 비교.
- before-audit.log, after-regression.log, stack.log, input.log 및 각 종료 코드.
- 새 실행에서 생성된 회귀 결과 txt. 이전 단계의 결과 파일은 백업 후 원래 위치에 복원했습니다.

현재 진입점: [Context](../Assets/Scripts/Combat/Runtime/Context.cs), [StatsContainer](../Assets/Scripts/Combat/Stats/StatsContainer.cs), [StackManager](../Assets/Scripts/Combat/Stacks/Runtime/StackManager.cs), [StackTriggerExecutor](../Assets/Scripts/Combat/Stacks/Runtime/StackTriggerExecutor.cs).

## 다음 범위

B2를 완료하고 중지합니다. B3/B4는 이번 실행에 포함하지 않았습니다. namespace 개편 C, Stack 정책 변경 및 추가 리팩터링도 수행하지 않았습니다. 다음 진행 요청을 기다립니다.
