# P4 위임 입력·전투 검증 보고서

작성일: 2026-11-24 · 사용자 요청: 미실시 수동 확인 항목의 검증 위임

## 검증 방식과 판정 범위

Unity 6000.3.10f1 PlayMode에서 가상 키보드·마우스의 상태 이벤트를 실제 Input System에 전달했습니다. 기존 InputSystem_Actions 바인딩을 유지하고 PlayerEntity → ActBridge → Attacker → Collector → Mechanism 경로를 검사했습니다. 테스트가 Player 공격 콜백이나 Attacker.EnqueueCast를 직접 호출하지 않습니다.

이는 **위임된 기능 검증을 입력 자동화로 수행한 결과**입니다. 사람이 화면을 보며 조작한 수동 플레이가 아니며, 실제 장치·화면·원본 Battlefield의 배치를 확인한 결과로 표현하지 않습니다. Tick과 Physics2D.Simulate를 직접 진행하므로 프레임 간 체감 지연과 화면 표현도 판정 범위 밖입니다.

검증은 기존 격리 사본의 빈 임시 씬에서 실행했습니다. CharacterSpec/SkillData/Stack 정의를 메모리에서 생성해 실제 PlayerEntity와 EnemyEntity에 연결했습니다. Enemy는 동기 fixture에서 Start 초기화를 한 번 호출했으며, 이후 AI 판단과 명시 대상 요청은 실제 Tick 경로를 사용했습니다. 기존 원본 씬이나 PlayGroundAssets의 다른 콘텐츠는 수정하지 않았습니다.

## 임시 도구

`Assets/PlayGroundAssets/PipelineDelegatedValidation/Editor/DelegatedInputChecks.cs`와 같은 폴더의 README를 추가했습니다. 필요한 정의를 실행 중 생성하므로 별도의 영구 .asset 또는 씬 애셋은 만들지 않았습니다. 이 도구는 batchmode와 검증 사본 marker가 있는 환경에서만 실행할 수 있습니다.

DamageProbe는 실제 DamageMechanism 본문을 호출하면서 횟수와 실행 Context만 기록합니다. Trigger 주기는 횟수를 기록하는 최소 Mechanism으로 검증합니다. 가상 장치만 검증 Player에 연결하며 batch 환경의 포커스/입력 업데이트 설정은 종료 시 복원합니다. 임시 코드 외 프로덕션 코드는 변경하지 않았습니다.

## 결과

최종 실행 `input-complete.log`에서 **30개 확인 항목이 모두 통과했고 exit 0**으로 종료했습니다. 검사 중 수집한 예기치 않은 Error/Exception/Assert는 0개였습니다. 앞선 fixture 실패와 Unity 시작 로그까지 모두 없었다는 뜻은 아닙니다.

대표 관측값: 정상 5개 입력 후 실행 5회·Mana 100→50, 지연 CC 취소 후 Mana/CD 무환급 및 재사용 가능, AI 피해는 실행 1회·Player HP 1000→900, 중첩 Switch 후 새 Variable 2개 보존·루트 Mana 10만 지출, Trigger 두 주기 각각 1회 실행. 임시 도구와 검증 사본의 최종 소스 해시도 일치합니다.

| 수동 확인 계획 항목 | 위임된 자동 확인 내용 |
|---|---|
| 정상 입력 | Enter performed에서 비용·예약 없음, release에서 수집 후 Tick 실행, 실제 HP 감소와 Pending 해제. 왼쪽 클릭도 별도 확인 |
| 기술 슬롯 | Shift/Space/오른쪽 클릭으로 Skill1/Skill2/Ultimate의 기존 canceled 경로 실행, 루트 Mana 한 번씩 지불 |
| 빈 설정 | SkillData/MechanismData/Mechanism 각각 없음. 네 슬롯 입력에서 예외·명령·비용·예약 없음 |
| 지연 중 이동 | D 입력으로 실제 물리 이동. 선택 대상 유지 및 실행 시점 Origin 확인 |
| 지연 중 CC | 실제 Player의 큐 기반 Stunned 적용·반복 갱신. Pending 취소, 이동/공격 제한, Mana/CD 무환급, 만료 복구와 새 입력 실행 |
| AI 명시 대상 | 실제 Enemy Tick이 Player를 선택하고 피해 적용. 대상 비활성화 시 dispatch 거부, 재탐색 없음, 비용 무환급 및 Pending 해제 |
| Switch/Chain | 입력으로 루트 Switch 수락 시 정확한 Variable 소비. 소비 후 새로 누적한 수량을 중첩 Switch 후속이 재소비하지 않으며 Damage까지 도달 |
| Stack 연동 | 실제 Player 큐에서 Buff 상한 재적용/만료 복귀, Trigger의 두 독립 누적 주기, 루트 Mana/Pending 독립 |

## 실패 이력과 해석

1. 최초 시도에서는 화면 없는 batch 실행의 입력 포커스 정책 때문에 performed 이벤트가 발생하지 않았습니다. 검증 도구에 게임 입력 전달 정책과 초기 InputSystem.Update를 추가했습니다. 프로덕션 입력 설정 파일은 변경하지 않았습니다.
2. 다음 실행은 입력·CC·Switch 16개 확인까지 통과했으나 AI 기대 대상에서 멈췄습니다. 자기 피해 시험용 공통 Damage 설정이 Self도 허용하여 AI가 자신을 선택할 수 있었기 때문입니다. AI fixture의 Mechanism 관계를 Enemy로 한정했습니다. 실제 AI 선택 정책을 바꾸지 않았습니다.
3. 이어진 실행은 AI의 Player 선택·Commit까지 통과했으나 HP 감소 기대값에서 멈췄습니다. 검증 Player의 피해 수신 허용 Variable인 `AllowEnemyDamage`가 없었습니다. InteractionFilter의 기존 적 피해 거부 계약을 확인하고 해당 Variable을 fixture에 추가했습니다. 피해 필터를 우회하거나 프로덕션 정책을 변경하지 않았습니다.
4. 표시 이름만 설정한 재시도에서도 같은 기대값이 실패했습니다. `VariableStorage.HasNameOf`는 `displayName`이 아니라 `StackDefinition.name`을 조회하므로 fixture의 실제 객체 이름까지 설정했습니다. 기존 이름 기반 정책은 그대로 유지했습니다.

초기 실패 로그는 보존합니다. 이 실패들은 검증 환경/fixture 보완 사항이며 파이프라인 코드 결함 수정으로 보고하지 않습니다. Unity 캐시 초기화의 기존 YamlDotNet 경로 예외 등 시작 로그와, 검증 중 예기치 않은 오류 수집 결과도 구분합니다.

## 근거와 후속 상태

작업 공간 `audit-results/pipeline-delegated/`에 초기/포커스 보완/최종 로그, 종료 코드, 확인 결과를 보관합니다. `production-source-preservation.json`은 P4 기준 프로덕션/기존 검사 C# 142개가 그대로임을 확인합니다. 별도로 생성된 InputSystem_Actions.cs도 원본과 사본 해시가 같음을 확인했습니다.

이 결과는 앞선 P4의 Cast·Stack·Travel 회귀를 보완합니다. 원본 씬의 시각적 표현, 실물 장치 입력, 실제 프레임 지연은 미검증으로 남습니다. 별도 DR 절삭 과제는 이번 검증으로 해결되지 않습니다. Git 작업과 B/C 구조 변경은 수행하지 않습니다.

위임받은 P4 기능 검증을 완료했습니다. 사람의 수동 플레이 완료로 기록하지 않고 **입력 자동화에 의한 위임 검증 완료**로 기록합니다. B/C는 자동으로 착수하지 않으며 후속 지시를 기다립니다.
