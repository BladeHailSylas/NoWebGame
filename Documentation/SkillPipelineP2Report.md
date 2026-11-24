# 기술 파이프라인 P2 작업 보고서

작성일: 2026-11-23 · 범위: Attacker 루트 수락 절차 정리

## 변경 내용

Attacker.TryCast를 요청 검사 → Variable 캡처 → 공통 명령 생성 → Target 준비 → 준비 후 수락의 순서로 읽을 수 있도록 정리했습니다. 추가 Manager나 공용 인터페이스, 장기 상태 객체는 만들지 않았습니다.

| private 메서드 | 책임 |
|---|---|
| TryValidateRootRequest | 기존 순서의 수명/CC/CD/Pending/설정/비용/Tick/Collector 검사 및 binding·비용·CD 값 반환 |
| TryCaptureRootVariable | Switch cases의 기존 순서대로 Variable 선택·캡처. 일반 기술은 default 반환. 소비하지 않음 |
| CreateRootCommand | 일반/Switch 공통 SkillCommand 생성. Variable만 분기 결과를 사용 |
| TryAcceptPreparedRoot | Target 준비 후 수명·Mana·Switch 재확인, Commit 구성, 기존 자원 적용/보상과 Collector 수락 |
| RejectCast | 기존 공통 CastRejected 로그 형식 유지. MissingBinding의 기존 짧은 로그는 별도로 유지 |

명시 Target의 mode 검사는 기존과 같이 Collector 가용성 검사 다음, Variable 캡처 전에 TryCast에서 수행합니다. `TryAcceptPreparedRoot`는 결과를 호출자가 재사용하지 않는 기존 흐름에 맞춰 void로 두고 실패를 동일한 로그로 기록합니다.

## 보존한 계약

- 기존 검사와 거절 우선순위를 삭제하거나 재배치하지 않았습니다. 준비 전후 및 dispatch 수명 검사는 유지합니다.
- Mana/CD는 Target 준비 전의 기존 시점에 읽고 준비 후에는 캡처한 값을 전달합니다. DamageData는 Variable 선택 후 명령 생성 시점에 캡처합니다.
- rootMode, CastPosition, Target, ExplicitTarget, Mask, Trace, Source 구성은 기존 일반/Switch 분기와 같습니다. Mechanism 정의를 깊은 복사하거나 새로 동결하지 않습니다.
- Context.VariableStorage는 생성자에서 캐시하지 않습니다. Summon의 지연 등록 및 Switch cases의 첫 일치 선택을 유지합니다.
- Target 준비 후 수명·Mana·Variable 수락 검사를 유지합니다. ApplyResources와 RollbackResources는 같은 메서드의 지역 함수로 함께 남기고 본문을 그대로 옮겼습니다.
- CD 예약 → 소비 시작 → Mana 지출 → CD/Pending/마지막 Tick → 소비 Complete → Collector 공개 순서를 유지합니다. 공개 전 실패만 보상하며 공개 후 무환급을 유지합니다.
- Pending 해제 콜백의 동일 Commit 확인, Chain의 루트 비용 독립성, Stack Trigger 직접 실행을 변경하지 않았습니다.

`resource-body-equality.json`에서 자원 적용/보상 지역 함수 본문의 변경 전후 동일성을 확인했습니다. 검사와 명령 생성의 실제 동작은 기존 회귀 검사로 확인하며, 메서드 분리 모양만을 위한 새 테스트는 추가하지 않았습니다.

## 검증

Unity 6000.3.10f1의 검증 사본에서 다음을 실행했습니다. 두 실행 모두 exit 0입니다.

| 실행 | 결과 |
|---|---|
| CastLifecycleChecks.RunWithRegression (EditMode) | 874개 정상 기대값 통과 |
| CastLifecycleChecks.RunPlayMode | 68개 정상 기대값 통과 |

EditMode 구성은 CastLifecycle 53, CastCommit 57, CastPreparation 38, TimeSystem 43, BasePosition 106, RelativeTargetPipeline 77, EntityRelation 407, TemporaryEnemy 40, OriginCastPosition 21, AttackTrace 19, TargetContract 13입니다. P1의 null 구성 정상 반환 검사도 포함합니다. PlayMode는 실제 Player/Summon 수명, 취소, 재진입 및 실행된 효과의 수명 계약을 확인합니다. 의도적인 예외 격리 로그는 포함되며 로그 전체가 무오류라는 의미는 아닙니다.

Scripts/Editor/ExitLab C# 142개의 원본/사본 해시가 모두 일치했습니다. TryCast 이전의 큐·Tick·수명 코드와 P1 이후 영역이 원본 백업과 동일함을 확인했습니다. 실제 사용자 전투 플레이는 이번 단계에서 수행하지 않았습니다. 이 검증은 P2 변경의 회귀 확인이며 P4 전체 통합 완료를 대신하지 않습니다.

근거는 작업 공간 `audit-results/pipeline-p2/`의 `p2-edit.log`, `p2-edit-exit.txt`, `p2-play.log`, `p2-play-exit.txt`, 검사 결과 txt, `source-copy-sha256.json`, `resource-body-equality.json`, `scope-check.json`입니다. `Attacker.before.cs.txt`에는 변경 전 파일을 보관했습니다. 기존 A1/P1 로그는 유지했습니다.

## 범위 준수

이번 런타임 변경 파일은 Attacker.cs 하나입니다. P1의 호환 진입점/null 방어와 A1 분리 파일을 유지했습니다. Git 명령·커밋·push·브랜치 작업은 하지 않았습니다. PlayGroundAssets와 원본 플레이 씬을 수정하거나 저장하지 않았습니다. P3, P4, 물리 이동(B), namespace 개편(C)은 이번 작업 범위에 추가하지 않습니다.

P2를 완료하고 여기서 중지합니다. 설계상 P3는 제외되어 있으므로 다음 예정 단계는 P4이며 별도 승인을 기다립니다.
