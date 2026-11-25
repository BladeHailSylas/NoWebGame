# C2 완료 보고서 — 실행 타입 namespace 이관

작성일: 2026-11-24 · 상태: C2 완료, C3+C4 및 C5 미착수

## 1. 적용 내용

승인된 [C1 타입 manifest](NamespaceCTypeManifest.csv)의 C2 타입 93개, 선언 파일 46개를 NoWebGame 기반 목표 namespace로 이관했습니다. C3+C4 대상 104개 타입은 기존 완전명을 유지합니다. 이들 파일에서도 C2 타입을 사용하는 참조는 함께 갱신했으며, 소비 코드 수정과 타입 자체의 이관을 구분했습니다.

| 원본 변경 영역 | C# 파일 수 | 내용 |
|---|---:|---|
| Assets/Scripts | 77 | C2 선언 46개 파일 및 런타임 소비 코드 31개 파일 |
| Assets/Editor | 3 | DestinationSettingsDrawer·TargetMaskDrawers·TargetMaskMigration의 C2 타입 참조 |
| Assets/ExitLab | 26 | 기존 검증 코드의 using·완전명 참조 |
| Assets/PlayGroundAssets | 1 | DelegatedInputChecks의 필요한 타입 참조만 갱신 |
| 합계 | 107 | 코드 본문 실행 규칙·문자열·수치 변경 없음 |

PlayGroundAssets의 애셋·테스트 동작·배치는 변경하지 않았습니다. 물리 파일 이동, meta/GUID 변경, assembly 추가, 클래스명 변경, 직렬화 식별자 이관, Git 작업은 하지 않았습니다. TargetMaskMigration의 구 타입 문자열 해석 정책 개선은 C3+C4에 남겼으며 이번 수정은 C2 타입 참조 갱신입니다.

## 2. 변경 방법과 경계 보존

현재 Unity 컴파일 응답 파일의 참조·전처리 설정을 이용해 Roslyn semantic model에서 실제 타입 참조를 해석했습니다. 같은 옛 namespace를 쓰던 타입이 여러 새 namespace로 나뉘므로, 옛 namespace 문자열을 파일 전체에서 일괄 치환하지 않았습니다. 새 using은 해당 파일이 참조하는 타입에 따라 추가하고, 명시적 완전명은 global:: 형태로 갱신했습니다.

검증 사본에서 컴파일·회귀를 통과한 뒤, 원본의 시작 해시가 그대로인지 재확인하고 테스트한 코드와 바이트가 같은 107개 파일만 원본에 적용했습니다. 사본 전용 StackPlaygroundBuilder 1개도 검증을 위해 참조를 갱신했지만 원본에 새로 추가하지 않았습니다.

다음 충돌은 사본 컴파일에서 발견하여 해결했습니다.

- PlayerEntity: 기존에 같은 namespace에서 선택되던 Logger가 UnityEngine.Logger와 충돌하여 프로젝트 Logger를 가리키는 명시적 alias를 추가했습니다.
- TickerRunner: 자신의 기존 namespace Systems.Time과 이동한 Time 클래스의 이름 해석이 충돌하여 GameTime alias로 기존 정적 호출을 명확히 했습니다. TickerRunner 자체 namespace는 C3+C4 대상이므로 유지했습니다.

초기 컴파일 실패 2회의 로그를 보존했고, 최종 성공 결과와 구분했습니다. 원본에는 이 오류 상태를 적용하지 않았습니다.

## 3. 검증 결과

| 검사 | 결과 |
|---|---|
| 실제 런타임 타입 매핑 | C2 93개는 새 이름으로만 존재, C3+C4 104개는 기존 이름으로만 존재 |
| 코드 구조 비교 | 사본 변경 C# 108개(원본 107 + 사본 도구 1) 모두 통과 |
| 문자열·수치·문자 리터럴 | 정규화 없이 비교하여 모두 동일 |
| 스크립트·참조 로드 | 이동 스크립트 86개, 기존 애셋 기록 178개, 누락 이슈 0 |
| 전후 로드 기록 | 승인된 타입명 매핑을 정규화한 뒤 script/asset 기록 차이 0 |
| 원본과 사본의 import 후 GUID | 원본 meta 575개 모두 동일 |
| Cast 및 관련 EditMode | 874개 통과 |
| CastLifecycle PlayMode | 68개 통과 |
| Stack 주요 PlayMode | Lifecycle 23 + Trigger 46 + Buff 78 + Transition 101 = 248개 통과 |
| Stack 기존 기준선 | 정상 기대값 23개 통과, 기존 DR 절삭 결함 재현 1개 별도 유지 |
| Travel 물리 | 96개 통과 |
| 위임 입력 | 30개 통과 |
| 최종 실행 종료 | 컴파일/EditMode·타입 검사·4개 PlayMode 실행 모두 exit 0 |
| 원본 적용 결과 | 시작 기준 1,077개 파일 중 예정된 C# 107개만 변경, 비 C# 변경 0 |

코드 구조 비교는 namespace 선언·using·승인된 타입 완전명과 Time alias 차이를 정규화하고 나머지 C# 토큰을 대조했습니다. UNITY_EDITOR 조건부 코드도 포함했습니다. 이 비교만으로 동작을 증명했다고 주장하지 않으며 실제 회귀 검증과 함께 판단했습니다. 기존 경고·예외 주입 로그와 전체 검사 성공은 구분합니다.

Cast의 Target/Origin/Destination, 루트 Mana/CD/Pending 및 취소·예외 처리, Stack의 Buff/CC/Trigger 수량·소비·재진입·수명, Variable/Switch 소비·보상 계약을 바꾸지 않았습니다. 새 기능이나 기존 DR 절삭 정책 교정은 포함하지 않았습니다. 사람의 직접 플레이와 C5 최종 전체 판정은 아직 수행하지 않았습니다.

## 4. 근거와 복구 자료

작업 공간 audit-results/namespace-c2/에 다음을 보관했습니다.

- baseline/, source-baseline.json, copy-tools/: 원본과 사본 전용 도구의 변경 전 복구 자료.
- C2Rewrite.cs, rewrite-log.tsv, staged/: 타입 해석 기반 변환과 최종 적용 코드.
- C2Structure.cs, structure-verification.json: namespace/참조 외 코드 구조 및 리터럴 보존.
- C2Verification.cs, c2-type-verification.txt: 93개 이관·104개 유지 확인.
- compile-regression*.log, types.log, cast-play.log, stack.log, travel.log, input.log 및 종료 코드/결과 txt.
- layout-c2.json, load-comparison.json, import-guid-verification.json: 로드 기록과 GUID 보존.
- applied-files.json, final-changed-paths.txt: 원본에 실제 반영한 파일 목록 및 테스트 사본과의 일치.

이전 단계 결과 txt는 별도 백업·복원했고 이번 결과는 C2 디렉터리에 보관했습니다. 원본 작업물을 reset/clean하거나 사용자 변경을 덮어쓰는 Git 작업은 하지 않았습니다.

## 5. Unity import와 다음 단계

원본 Unity는 실행하지 않았습니다. **원본 소스가 바뀌었으므로 다음 Unity 실행 시 import·스크립트 컴파일이 필요합니다.** 같은 파일 내용으로 검증 사본의 import와 회귀는 완료했습니다. 지금 별도 사람 플레이를 완료해야 C2가 끝나는 것은 아니며, 최종 직접 플레이는 기존 계획대로 남습니다.

C2를 완료하고 중지합니다. 다음 승인 단위는 C3+C4입니다. 이 단계에서는 남은 104개 타입과 직렬화 식별자, Drawer 및 구/신 타입 해석을 함께 이관하며 C5는 별도 단위로 유지합니다. 명시적 요청 전까지 커밋은 하지 않습니다.
