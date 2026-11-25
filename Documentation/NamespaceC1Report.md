# C1 보고서 — 타입 매핑과 직렬화 이관 실험

작성일: 2026-11-24 · 상태: C1 완료, C2 이후 미착수 · 원본 namespace/애셋 이관 없음

## 1. 산출물과 다음 작업 단위

사용자가 확정한 작업 단위는 **C1 → C2 → C3+C4 → C5**입니다. 이번에는 C1만 수행했으며 커밋·push·기타 Git 작업을 하지 않았습니다.

[타입별 이관 manifest](NamespaceCTypeManifest.csv)에 실제 타입 197개를 기록했습니다. Unity 설치본의 Roslyn 구문 분석으로 선언 199개를 수집하고 Unity 6000.3.10f1의 Assembly-CSharp reflection 결과와 대조했습니다. StatsContainer·TargetResolver의 partial 중복 선언 2개를 합친 고유 타입 수가 197개입니다. 매핑 대상의 런타임 해석 누락과 목표 완전명 중복은 모두 0입니다.

| 다음 적용 단위 | 타입 수 | 선언 파일 수 | 분류 원칙 |
|---|---:|---:|---|
| C2 | 93 | 46 | 현재 관측한 관리 참조 식별자/Unity 객체 이관에서 분리할 수 있는 선언 |
| C3+C4 | 104 | 50 | Unity 객체, 관리 참조 구현 및 혼합 파일을 묶어 적용 |

파일을 추가 분리하지 않기 위해 같은 파일에 있는 선언은 더 늦은 단계로 묶었습니다. CommandCollector 등 MonoBehaviour는 이름이 실행 계층에 속해도 C3+C4 대상입니다. 전체 97개 C# 중 주석 전용 AttackScheduler는 활성 선언이 없으므로 96개 파일을 매핑합니다.

manifest에는 old/new 완전명, assembly, 선언 경로/GUID, 중첩·generic·Unity 객체 여부, 실제 관리 참조 사용 애셋, m_EditorClassIdentifier 사용 애셋, 소비 코드 후보를 기록했습니다. ConsumerCandidates는 이름 기반 탐색 결과이므로 주석·동명 타입이 포함될 수 있으며 정확한 semantic binding 목록으로 해석하지 않습니다. 실제 변경 시 컴파일로 참조를 확인합니다.

## 2. 대상과 예외

- 목표 namespace는 승인된 NoWebGame 접두사와 현재 책임별 폴더를 따릅니다. assembly는 유지합니다.
- Generals.cs, EntitySettings.cs, Interfaces.cs의 혼합 책임은 각각 NoWebGame.Moves, NoWebGame.Moves.ObjectEntity, NoWebGame.Systems.Data로 유지하는 과도기 규칙을 적용합니다. 추가 파일 분리·클래스명 변경은 제외합니다.
- private 중첩 타입과 SkillMechanismBase의 generic arity도 완전명에 기록했습니다. partial 선언은 동일 목표 namespace로 이동합니다.
- 런타임 assembly에서 추가 발견한 56개 타입은 TextMesh Pro 샘플, InputSystem_Actions 생성 코드와 중첩 타입, Unity/compiler 생성 타입입니다. 이들은 본 작업에서 재명명하지 않습니다. Editor/ExitLab 및 임시 검증 도구의 자체 namespace도 유지하며 필요한 참조만 갱신합니다.
- 애셋 176개를 정적으로 검색했습니다. 현재 관리 참조 식별자로 나타나는 대상은 12종이며, 관리 참조 또는 m_EditorClassIdentifier가 매핑 타입을 가리키는 파일 후보는 111개입니다. PlayGroundAssets도 후보에 포함됩니다. 이는 적용 승인 목록이나 전부 수정해야 한다는 뜻이 아닙니다. GUID만으로 연결되는 참조는 별도 Unity 로드 검사 대상입니다.

관측된 관리 참조 타입은 MechanismData, Area/Damage/Dash/Detect/Dummy/Melee/Projectile/Stack/SwitchMechanism, CircleArea, BoxArea입니다. 현재 저장 애셋에 없는 나머지 타입도 코드 매핑에서 제외하지 않습니다.

## 3. 대표 이관 실험

원본 Assets·Packages·ProjectSettings 1,077개 파일을 백업·해시 기록하고 검증 사본에서 실험했습니다. 원본 코드·애셋은 변경하지 않았습니다. 실험 대상은 Generals.cs, EntitySettings.cs, SkillData.cs와 Mechanism 13종, 총 16개 파일의 타입 29개입니다. 이들의 목표 이름은 최종 manifest와 동일합니다. 소비 코드의 참조 갱신은 사본에만 적용했습니다.

실험용 SkillData에는 ManaCost 73, Consume=false인 Switch, 중첩 Damage 값 123/AP 17, Area lifeTick 37, 같은 후속 객체를 두 번 가리키는 공유 참조 및 중간 null을 넣었습니다. 기존 SkillData·AreaShape 프리팹 등도 함께 로드했습니다.

| 실험 | 관측 결과 |
|---|---|
| 설치 API 확인 | MovedFrom의 namespace/assembly/class 지정 생성자와 namespace 생성자 확인 |
| 호환 속성 + 구 이름 애셋 로드 | 선택한 클래스/구조체에 MovedFrom 적용 후 SkillData·관리 참조 복원 성공 |
| 저장 후 Unity 재시작 | 실험용 SkillData 저장 후 다시 실행하여 값·공유/null 참조 보존 확인 |
| 명시적 저장 식별자 이관 | 최종 정리된 사본에서 기존 애셋 9개 + 실험용 1개, 총 10개에 class/ns/asm 및 대상 m_EditorClassIdentifier만 변경 |
| 명시적 이관 후 재시작 | 다시 실행하여 동일 데이터와 참조 로드 확인 |
| 변환의 보수성 | 두 번째 적용 무변경, 미매핑 타입·미지원 형식 중단, 역식별자 변환 시 원문 바이트 복원 확인 |
| 데이터 비교 | 정규화한 직렬화 관측 기록 3,711개가 호환 로드·호환 재로드·명시적 이관·명시적 재로드에서 모두 동일 |
| 최종 복구 | 코드·애셋·사본 도구 복구 후 Unity 재로드 성공, 동일한 3,711개 기록 차이 0 |
| 최종 실행 상태 | 정리된 사본의 기준·호환·호환 재로드·명시적·명시적 재로드·복구 6회 모두 exit 0, 매 실행 원본 전체 meta GUID 보존 |

3,711개 비교에는 정수·실수·문자열·enum·배열 크기, 관리 참조 ID/타입, Unity 객체의 GUID/fileID가 포함됩니다. 세션마다 달라지는 SerializedProperty 내부 m_FileID 자식 값은 제외하고 부모 ObjectReference의 GUID/fileID로 비교했습니다. m_EditorClassIdentifier의 문자 배열 자식은 제외하되 부모 문자열을 타입 매핑으로 정규화하여 비교했습니다. 이 정규화는 실제 데이터 손실을 무시하기 위한 것이 아니며 원시 비교 자료도 보관했습니다. 기존 씬은 별도 DirectoryLayoutAudit로 로드해 참조/타입 이슈를 검사합니다.

이 결과는 선택한 29개 타입의 대표 실험입니다. 197개 전체 이관, 모든 legacy 버전, generic/중첩 타입의 모든 직렬화 형태, Inspector GUI 상호작용, 캐시 디렉터리를 제거한 완전 신규 import까지 통과했다고 주장하지 않습니다. Unity 프로세스는 매 검사마다 재시작했지만 검증 사본의 Library는 재사용했습니다.

## 4. 중요한 발견: Unity 호환과 도구의 타입 해석은 다름

이관 후 Unity는 옛 이름의 관리 참조를 복원했지만 Type.GetType("Moves.Mechanisms.DamageMechanism, Assembly-CSharp")은 null을 반환했습니다. 같은 조건에서 기존 TargetMaskMigration이 실험용 구 데이터의 3개 항목을 REVIEW로 남겼습니다.

따라서 C3+C4에서는 MovedFrom만 추가하고 도구의 using만 바꾸는 것으로 끝낼 수 없습니다. TargetMaskMigration은 manifest 기반 old/new 타입 식별자 해석을 추가하고, 구·신·혼합·미지원 데이터를 각각 검사해야 합니다. 원래 TargetMask 정책 변환은 별도이며 이번 namespace 이관과 함께 자동 실행하지 않습니다. Drawer의 후보 탐색·선택·신규 인스턴스 생성 검사도 C3+C4 완료 기준에 유지합니다.

권장 실제 적용은 타입별 호환 정보를 유지하면서 대상 애셋의 식별자를 명시적 dry-run으로 이관하는 방식입니다. 무차별 전체 문자열 치환이나 프로젝트 전체 ForceReserialize는 사용하지 않습니다. 호환 속성은 과거 애셋 복원 수단으로 유지하고, 저장 식별자 이관은 현재 자산을 새 이름으로 명확히 고정하는 역할로 구분합니다.

기존 SwitchMechanism·CircleArea·BoxArea 등에는 현재 Unity가 SerializeReference 사용 시 Serializable 속성 누락 경고를 출력합니다. 현재 버전에서 복원되는 사실과 경고가 없는 상태를 구분합니다. 이번 실험은 원본에 속성을 추가하지 않았으며, 해당 경고의 교정은 C3+C4 착수 시 namespace 변경 외 변경으로 별도 명시할 항목입니다.

## 5. 실패 관측과 복구 절차 보완

초기 reflection 검사는 외부 샘플과 자동 생성 타입까지 프로젝트 누락으로 간주해 중단됐습니다. 실제 목록을 확인하여 명시적 제외 범주를 추가하고 재실행했습니다. 이를 프로젝트 타입 누락으로 보고하지 않습니다.

명시적 이관 도구의 Python 실행 경로가 사용할 수 없어 실제 변환은 PowerShell 구현으로 수행했습니다. 변환이 적용되지 않은 선행 실행 로그는 따로 보관하며 성공 근거에서 제외했습니다.

최초 복구 실행은 원본 1,077개 파일을 되돌렸지만, 원본에 없는 검증 사본 전용 DirectoryLayoutAudit·StackPlaygroundBuilder의 새 using이 남아 컴파일에 실패했습니다. 이 두 도구의 참조도 되돌려 복구를 재검증했습니다. **향후 복구 목록은 원본 파일뿐 아니라 실험 중 변경하는 모든 검증 사본 전용 파일까지 포함해야 합니다.** 소스·애셋·도구를 같은 단위로 복원해야 한다는 계획을 실제 실패로 확인한 것입니다.

원시 실패 로그를 보존했습니다. 초기 실패를 삭제하거나 전체 실행이 모두 성공했다고 보고하지 않습니다. 원본에는 영향이 없었습니다.

또한 초기 복구의 값 비교에서 누적 검증 사본에만 남은 AreaEntityBase.prefab이 원본 AreaEntityBox.prefab과 같은 GUID를 보유한 사실을 확인했습니다. Unity가 사본의 GUID를 재발급하여 이전 실험의 자기참조 GUID가 달라졌습니다. 원본에는 이 오래된 파일이 없으며 원본 파일은 변경되지 않았습니다. 누적 사본의 오래된 프리팹과 이전 생성 회귀 애셋을 격리하고, 원본을 다시 복사한 후 전체 C1 실험을 재실행했습니다. 최종 근거는 clean/ 아래 로그와 비교 결과이며 초기 최상위 로그는 탐색 기록입니다. 최종 각 실행은 원본 전체 meta GUID를 import 후 다시 비교합니다. 이로써 C0의 제한된 애셋 로드 검사만으로 사본 전체의 GUID 충돌 부재까지 주장할 수 없다는 한계도 확인했습니다.

## 6. C2 이후의 적용·검증 경계

1. C2는 manifest의 93개 타입/46개 파일과 그 소비 코드의 참조를 적용 단위로 삼습니다. 선언만 바꾸고 호출부를 다음 단계로 미루지 않습니다. 같은 파일의 타입/partial은 함께 처리합니다.
   과도기에는 NoWebGame.Systems와 기존 Systems 등이 공존하므로 필요한 참조에 global:: 또는 명시적 alias를 사용합니다. 파일별 목표가 갈라지는 Moves/PlayerScripts 전체 문자열을 한 번에 치환하지 않습니다. 실험 사본의 일괄 import 추가는 대표 복원 실험을 위한 편의 조치이며 C2의 실제 수정 방식으로 채택하지 않습니다.
2. C2에서 타입 문자열이 필요한 새 저장 위치나 동적 조회를 발견하면 해당 항목을 C3+C4로 옮기거나 별도 검토하며, 관측 목록에 없다는 이유로 안전하다고 단정하지 않습니다.
3. C3+C4는 104개 타입/50개 파일과 직렬화 후보 목록을 바탕으로 실제 변경 dry-run을 만듭니다. 여기서 확인된 PowerShell 실험 도구는 지원 형식이 좁은 검증용이며, 그대로 범용 production 이관기로 승인된 것은 아닙니다. 부분 적용 실패의 자동 복구·동시 파일 변경 감지·중복/비정상 레코드 거부 범위를 보강한 뒤 사용합니다.
4. C5는 전체 197개 매핑, 참조/직렬화 검사와 기존 Cast·Stack·Travel·입력 전체 회귀를 별도 수행합니다. 이번 C1에서 기존 전체 회귀를 다시 수행한 것으로 집계하지 않습니다.
5. 각 작업 단위 종료 시 보고하고 대기합니다. 커밋은 명시적으로 요청될 때만 수행합니다.

## 7. 근거 위치

작업 공간 audit-results/namespace-c1/에 syntax-types.json, c1-inventory.json, type-manifest.json, excluded-types.txt, serialization-candidate-paths.txt, string-dependencies.txt, 실험 코드/실행 스크립트, baseline 백업, 단계별 Unity 로그·종료 코드, 직렬화 원시/정규화 비교와 복구 결과를 보관했습니다. 이 디렉터리의 baseline은 PlayGroundAssets를 포함한 로컬 복구 자료이며 원격 백업을 대체하지 않습니다.

원본 런타임 파일을 변경하지 않았으므로 C1 때문에 원본 Unity를 추가 import할 필요는 없습니다. C2 및 C3+C4 실제 적용은 아직 시작하지 않았습니다.

최종 원본 1,077개 파일의 해시는 C1 시작 시점과 모두 동일합니다. 최종 6회 로드 검사에서 각각 기존 대상 기록 178개·이동 스크립트 86개를 확인했고 누락 이슈는 0입니다. C1을 완료하고 중지합니다. namespace 변경은 검증 사본에서도 원래 상태로 복구했으며 실험용 fixture/검증 도구만 사본에 남겨 재현 자료로 보존했습니다.
