# C0 보고서 — B 최종 상태 실행 검증

작성일: 2026-11-24 · C0 실행 검증 완료 · C1 미착수

## 수행 범위

사용자가 승인한 C0 범위에서 현재 로컬 B 최종 상태를 검증했습니다. Git 작업은 제외하라는 최신 지시에 따라 commit/remote 확인도 실행하지 않았습니다. 원격 push 완료는 사용자 전달 사실이며 원격과 로컬의 동일성은 독립 검증하지 않았습니다.

원본 Assets·ProjectSettings·Packages 1,077개 파일의 해시를 기록하고 검증 사본과 모두 일치시킨 후 Unity 6000.3.10f1을 실행했습니다. 검증 사본에 남아 있던 B3/B4 이전 경로는 대응 파일/meta와 함께 새 경로로 옮겨 중복 타입을 방지했습니다. 검증 전용 DirectoryLayoutAudit를 전체 이동 86개 대상으로 확장했습니다. 원본 코드·애셋·namespace는 수정하지 않았습니다.

C1까지의 일괄 수행은 허용됐으나 C0 검증에 필요하지 않아 시작하지 않았습니다. 이번 결과로 B3/B4의 실행 검증과 B5의 전체 이동·통합 회귀 항목을 확인했습니다. C의 타입별 manifest 및 이관 실험은 다음 C1 범위입니다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| Unity import/컴파일 | 검증 사본에서 성공 |
| 전체 이동 스크립트 | 86개 목적지 로드 성공, 원본 경로 없음, 이동 GUID 모두 기존과 동일 |
| 유지 대상 | C# 11개 B0 기준 해시 동일 |
| 애셋 참조/타입 | 19개 대상 애셋·씬·프리팹에서 기록 178개, B2 로드 기록과 차이 0, 누락 이슈 0 |
| 선언 타입 | 컴파일러 내부 PrivateImplementationDetails를 제외한 완전명/assembly 목록 차이 0 |
| Cast 및 관련 EditMode | 874개 통과 |
| CastLifecycle PlayMode | 68개 통과 |
| Stack 주요 PlayMode | Lifecycle 23 + Trigger 46 + Buff 78 + Transition 101 = 248개 통과 |
| Stack 기준선 | 정상 기대값 23개 통과, 기존 DR 절삭 결함 재현 1개 별도 유지 |
| Travel 물리 | 96개 통과 |
| 위임 입력 | 30개 통과 |
| 실행 종료 | 5개 실행 모두 exit 0 |
| 이번 실행의 원본 변경 | 기록한 1,077개 파일의 실행 전후 변경 0 |

원본 PlayGroundAssets도 위 보존 확인에 포함됩니다. 기존 입력 검증 도구는 사본에서 사용했습니다. 사람의 직접 플레이를 수행한 결과는 아니며 최종 화면·실물 입력·체감 시험은 남습니다. 초기 캐시 경고와 예외 주입 검사를 포함한 로그 전체를 무오류라고 주장하지 않습니다.

## 과거 해시와 현재 기준의 차이

B0 기록에 대한 바이트 비교에서 이동 86쌍 중 79쌍은 코드/meta 모두 동일했고 7쌍에 차이가 있었습니다. 해당 7개 코드와 6개 meta는 B0 해시와 정확히 일치하는 과거 검증 사본을 찾아 비교했으며, CR 줄바꿈 문자와 파일 끝 공백을 제외한 내용이 모두 동일했습니다. GUID는 86개 모두 동일합니다. 원시 바이트 차이와 정규화 비교 결과를 각각 보관했습니다.

B4 당시 전체 파일 목록과의 비교에는 CastCommitChecks.cs 및 일부 폴더 meta 등의 추가 해시 차이도 있습니다. 사용자는 Unity import를 1회 수행했고 코드는 수정하지 않았다고 확인했습니다. 이 사실을 기록하되 변경 원인을 Git 작업으로 확인했다고 주장하지 않습니다. CastCommitChecks.cs의 당시 해시와 일치하는 별도 사본은 찾지 못했으므로 해당 파일까지 과거와 바이트/정규화 동일하다고 단정하지 않습니다. 현재 버전으로 CastCommit 57개 및 전체 회귀가 통과했습니다.

이번 검증 기준은 C0 시작 시 기록한 현재 로컬 상태입니다. 기존 차이를 덮어쓰거나 과거 포맷으로 되돌리지 않았습니다. 향후 Git 기준 확인이 허용될 때 원격 commit과 이 로컬 기준을 연결할 수 있습니다.

## 근거

작업 공간 audit-results/namespace-c0/에 다음 자료를 보관했습니다.

- source-baseline.json, source-changes-during-c0.json: 현재 원본 기준과 무변경 확인.
- all-moves-verification.json, retained-scripts-verification.json, all-guid-verification.json: 전체 이동/유지 대상 비교. 첫 파일의 바이트 불일치는 후속 포맷 분석과 함께 해석합니다.
- formatting-analysis.json, archived-content-comparison.json, since-b4-differences.json: 과거 기준과의 차이 분석.
- layout-c0.json, snapshot-comparison.json: 실제 Unity 로드 및 B2 기준 비교.
- edit.log, cast-play.log, stack.log, travel.log, input.log와 각 종료 코드 및 회귀 결과 txt.

이전 단계의 결과 txt는 백업 후 복원했으며 이번 결과는 C0 디렉터리에 따로 보관했습니다.

## 원본 Unity import 및 다음 단계

이번에는 검증 사본만 실행했습니다. 사용자가 이미 B 이후 원본에서 import를 수행했다고 확인했고, 이번 작업은 원본 코드·애셋을 변경하지 않았으므로 **C0 때문에 원본 Unity를 추가로 실행/import할 필요는 없습니다.** 검증용 Unity 실행도 모두 종료됐습니다.

C1에서는 현재 기준을 보존한 별도 사본에서 타입 manifest와 직렬화 이관/복구 실험을 진행할 수 있습니다. 아직 실제 namespace 이관은 시작하지 않았습니다. 이번 작업은 C0 완료 보고 후 중지합니다.
