# C5 완료 보고서 — namespace 최종 통합 검증

작성일: 2026-11-25. C5를 완료했으며 C0~C5 namespace 정리 작업을 종료합니다. 이번 단계에서 추가 코드·애셋 수정은 필요하지 않았습니다. 변경 산출물은 이 보고서와 계획서 상태 갱신이며 Git 작업은 수행하지 않았습니다.

## 원본 import 이후 상태 확인

사용자는 C3+C4 확인 과정에서 원본 Unity의 import·컴파일을 한 차례 수행했다고 전달했습니다. 이는 사용자 보고로 기록하며 해당 Editor 세션의 로그를 직접 확인한 것으로 간주하지 않습니다.

C3+C4 반영 계획과 파일 해시를 대조한 결과, 현재 원본 Assets/Packages/ProjectSettings의 1,081개 파일은 완료 당시 상태와 모두 일치했습니다. 추가·삭제·변경 파일은 없었습니다. C5 종료 시에도 같은 파일 집합과 해시를 확인했습니다. 기존 미커밋 작업을 보존했으며 원본 Unity를 새로 실행하지 않았습니다.

## 최종 검증 방법

Unity 6000.3.10f1 검증 사본에서 현재 원본과 파일별 내용을 대조한 후 검사를 실행했습니다. 이전 테스트가 사본에 남긴 Physics2DSettings 차이는 백업 후 원본 내용으로 맞췄고, 중복 생성된 NestedExitContract 1.asset 및 meta는 사본 밖으로 격리했습니다. 원본에는 이 조치를 적용하지 않았습니다.

사본 전용 fixture·검사 도구와 과거 이동 경로의 폴더 meta는 별도 목록으로 기록했습니다. 이전 단계의 결과 파일은 백업하고 C5 결과를 별도 보관한 뒤 복원하여 이전 근거를 덮어쓰지 않았습니다.

## 실행 결과

| 검사 | 최종 결과 |
|---|---|
| namespace·Editor 도구 검사 | 229 PASS; 대상 197개 타입의 새 이름 실재 및 구 이름 부재 확인 |
| 기존 EditMode 회귀 | 874 PASS |
| CastLifecycle PlayMode | 68 PASS |
| Stack 주요 회귀 | 248 PASS |
| Stack 기준선 | 정상 23 + 기존 DR 절삭 현상 확인 1, 예상 결과 유지 |
| Travel | 96 PASS |
| 입력·Switch·Trigger 위임 검증 | 30 PASS |
| 직렬화 값 대조 | 12,386개 기록, C3+C4 재로드 결과와 차이 0 |
| 애셋 로드 및 참조 점검 | 550개 기록, 문제 0; 이전 결과와 차이 0 |
| 이동 스크립트 GUID/GetClass 결과 | 이전 결과와 차이 0 |
| 원본 Assets meta | 577개, 중복 GUID 0, import 사본과 GUID 차이 0 |
| 원본 파일 보존 | 1,081개 모두 C5 시작·종료 해시 및 파일 집합 동일 |

EditMode 세부 결과는 CastLifecycle 53, CastCommit 57, CastPreparation 38, Time 43, BasePosition 106, RelativeTarget 77, EntityRelation 407, TemporaryEnemy 40, OriginCastPosition 21, AttackTrace 19, TargetContract 13입니다. 다섯 Unity 실행(edit/cast-play/stack/travel/input)의 종료 코드는 모두 0입니다.

현재 값 대조는 C3+C4에서 확정한 정규화 기준을 사용했습니다. 세션 종속 m_FileID 하위 값과 타입명 문자열의 문자 배열 표현은 제외하며 부모 GUID/fileID 및 정규화한 타입 문자열은 비교합니다. 중첩·공유·null 관리 참조와 대표 Switch/Damage 수치도 검사했습니다.

C3+C4에서 완료한 구 이름 호환 로드, 명시적 식별자 이관 후 재시작, 이관 재적용 무변경, 일부 실패 복구·동시 변경 거부 시험은 해당 단계 근거를 유지합니다. 원본 코드·데이터가 동일하므로 이번에는 실제 이관이나 복구를 다시 적용하지 않았습니다. 최종 사본에서 구/신/혼합/미지원 YAML 검사와 현재 데이터 로드를 다시 수행했습니다.

## 잔존 구 이름의 허용 범위

Assets의 C#·애셋·프리팹·씬 등에서 manifest 기반 구 완전명과 namespace를 검색하고 각 결과를 분류했습니다.

| 남기는 위치 | 이유 |
|---|---|
| NamespaceTypeMap | 197개 구→신 완전명의 명시적 호환 사전 |
| MovedFrom 속성 | Unity에 이전 타입 식별자를 알려주는 호환 메타데이터 |
| TargetMaskMigration, NamespaceMigrationChecks | 과거 YAML 지원과 검증 입력 |
| AttackScheduler.cs | 전체가 주석 처리된 비활성 코드; C1에서 제외한 범위 유지 |
| CreateAssetMenu 경로·설명 주석·TMP 예제 표시 문자열 | 타입 참조가 아닌 문구; 변경 대상 아님 |
| EnemyDummy의 단순 클래스명 | 예전 전역 완전명과 문자열이 같지만 현재는 새 namespace 안의 선언이며 클래스명 보존 대상 |

이관 누락으로 판단되는 활성 참조 또는 저장 타입 식별자는 발견하지 못했습니다. Editor/ExitLab의 자체 namespace 및 외부·생성 코드는 기존 승인 범위대로 유지합니다. 과거 보고서와 manifest의 구 이름은 이관 이력으로 보존합니다. 호환 사전이나 MovedFrom을 이번 완료와 함께 제거하지 않습니다.

## 계약과 남은 범위

Cast의 Target/Origin/Destination, 루트 Mana/CD/Pending, 취소·예외 경계와 Stack/Variable/Switch의 소비·보상·직접 실행 계약은 기존 회귀 결과를 유지했습니다. 파일 이동·assembly 분리·추가 API 재설계·TargetMask 정책 변환은 수행하지 않았습니다. PlayGroundAssets도 C5에서 수정하지 않았습니다.

자동 검증 통과는 인간 플레이의 화면·조작감 확인을 대신하지 않습니다. 이전 Stack 수동 테스트 결과는 그대로 별도 이력으로 유지하며, 이번 C5의 인간 플레이 결과로 재분류하지 않습니다. v0.2 최종 플레이 테스트와 이후 구조 개선은 별도 작업입니다. 알려진 DR 절삭 정책 및 이전 단계에 명시한 환경 로그도 이번에 해결했다고 보고하지 않습니다.

사용자가 원본 import·컴파일을 이미 수행했고 C5에서 코드·애셋을 추가 수정하지 않았으므로 **이번 단계 때문에 원본 Unity를 다시 실행하거나 import할 필요는 없습니다.** C 작업은 완료 상태로 정지하며 커밋·push는 요청받지 않아 수행하지 않습니다.

## 검증 근거

저장소 상위 `audit-results/namespace-c5/`에 보관했습니다.

- `source-baseline.json`, `post-import-differences.json`, `original-preservation.txt`: 원본 기준과 보존 검사
- `old-fullname-occurrences.csv`, `old-namespace-occurrences.csv`, `old-name-allowlist.csv`: 잔존 구 이름의 위치·분류
- `validation-extra-files.txt`, `quarantine/`: 사본 전용 파일 목록과 격리 자료(목록은 정리 전 기준)
- `guid-verification.json`, `layout-comparison.json`, `value-comparison.json`: GUID·로드·직렬화 대조
- `namespace-migration-results.txt`, `regression-results/`: 개별 검사 결과
- `edit.log`, `cast-play.log`, `stack.log`, `travel.log`, `input.log` 및 각각의 종료 코드: 최종 실행 근거

기존 상세 구현과 애셋별 변경 내역은 [C3+C4 보고서](NamespaceC34Report.md)와 [애셋 변경 명세](NamespaceC34AssetMigration.csv)를 참조하십시오.
