# v0.2 최종 통합 플레이 테스트 가이드

작성일: 2026-11-25. 대상은 기술 파이프라인·디렉터리·namespace 정리 및 DR 수정이 반영된 현재 작업물입니다. 실제 플레이와 판정은 사용자가 수행합니다. 이 문서 작성이나 자동 검사 성공만으로 릴리스 승인 또는 Version Bump를 완료 처리하지 않습니다.

## 1. 공통 준비와 기록

1. Unity 6000.3.10f1에서 DR 수정 후 import·컴파일이 끝났는지 확인합니다. Console의 기존 기록과 이번 실행 기록을 구분합니다. 새 컴파일 오류, Missing Script/관리 참조 오류가 있으면 전투 시험을 중단하고 기록합니다.
2. 현재 사용하는 전투 씬과 시험용 CharacterSpec을 `Assets/PlayGroundAssets/ReleaseValidation/` 아래에 복제해 시험합니다. 하위 SkillData/Stack 정의를 바꾸려면 그 애셋도 복제하여 연결합니다. 씬만 복제하면 공유 ScriptableObject는 분리되지 않습니다. 기존 PlayGroundAssets 콘텐츠를 삭제하거나 정리할 필요는 없습니다.
3. 기존 TickerRunner·TargetResolver·CommandCollector·카메라·입력 구성을 유지합니다. 실제 Tick 속도를 기록하고 시간은 Tick 기준으로 판정합니다. 60 Tick/s일 때 120/180/300 Tick은 각각 약 2/3/5초입니다.
4. 기본 입력은 현재 InputSystem_Actions의 기본 공격 Enter/왼쪽 클릭, Skill1 Shift, Skill2 Space, Ultimate 오른쪽 클릭입니다. 이동은 현재 바인딩을 사용합니다. 포커스가 Game 뷰에 있고 Input System 설정이 정상인지 확인합니다. 공격은 버튼을 놓을 때 요청되므로 누른 시점과 해제 시점을 구분합니다.
5. 단독 시험에서는 Enemy 자동 AI와 임시 자기 피해 Trigger를 끕니다. AI/Trigger 전용 항목에서만 켭니다. 자연 HP/Mana 회복은 0으로 설정하여 비용·피해 관측을 방해하지 않게 합니다.
6. HP/Mana/공격력/방어력/속도는 기존 표시 또는 디버거의 `_statsBridge.Stats`로 확인합니다. DR은 목록 길이가 아니라 `TotalDamageReduction()` 또는 `StatsBridge.DR`을 확인합니다. 이는 감소율이 아닌 **잔여 피해 배율**입니다. Pending·Key 수량·CC 소유 제한은 디버거 또는 기존 AttackLog로 확인하고, 확인할 수 없으면 관측 불가로 기록합니다.
7. 적용자 이름은 시험 중 고정하고 두 시전자는 서로 다르게 지정합니다. 현재 Key는 정의와 적용자 이름으로 구분됩니다. 정의 복제에 따른 다른 Key와 동일 정의/다른 적용자에 따른 다른 Key를 구분합니다.

각 항목은 `ID / 실행 환경·씬·Spec / 실제 Tick 속도 / 시행 횟수 / 시작값 / 관측값 / PASS·FAIL·BLOCKED / 로그·영상 / 비고`를 기록합니다. 관측되지 않은 항목은 PASS로 추정하지 않습니다. 실패 시 마지막 입력부터 재현 순서와 변경한 애셋 설정을 함께 남깁니다.

## 2. 기존 환경과 추가 준비물

우선 [StackValidation 안내](../Assets/PlayGroundAssets/StackValidation/README.md)의 Characters/Skills/Stacks/Prefabs를 사용합니다. Test_DamageBoost, Test_ArmorBoost, Test_Haste, Test_Slow, Test_APBoost, Test_DRBoost, Test_Rounding_Base5, Test_CC_Self, Test_CC_Projectile, Test_Trigger_Success, Test_Trigger_PrepareFailure, Test_Variable가 제공됩니다. 기본 테스트 스킬은 Mana 0·CD 12 Tick이므로 유료 Cast 검증에는 그대로 사용하지 않습니다.

`PipelineDelegatedValidation/Editor/DelegatedInputChecks.cs`는 메모리 fixture를 만드는 자동 검사입니다. 사람이 선택해 실행할 영구 씬/Spec을 제공하지 않습니다. 자동 검사 결과는 [위임 검증 보고서](SkillPipelineDelegatedValidation.md)를 참고하되 실제 장치 시험을 대체하지 않습니다.

아래 추가 시험용 구성은 **이번 작업에서 생성하지 않았습니다**. 기존 애셋을 복제하여 PlayGroundAssets/ReleaseValidation 아래에서 준비합니다. 프로덕션 코드를 추가하지 않아도 됩니다. 설정이 어렵거나 필요한 관측 수단이 없으면 해당 항목을 BLOCKED로 남기고 필요한 구성만 별도로 요청하십시오.

| 구성 | 필요한 설정과 목적 |
|---|---|
| DR 관측용 Spec/자기 피해 Skill | HP 1000 이상, 공격력 100, 방어력 0, 재생 0. DamageMechanism Normal, damageValue=100, defaultAPRatio=0, defaultAmplitude=0, onHit/onExpire 없음, TargetMode=TowardsSelf, relative 관계 Self 허용, ManaCost=0. 기본 공격에 배치하고 나머지 슬롯에 DR +1/+2/Clear 배치. 공격 Buff와 동시 사용하지 않는 정량 기준 |
| 복수 DR 및 하한 Skill | 기존 DRBoost 정의/적용 스킬을 각각 복제. 한 쌍은 Value=20, 다른 쌍은 Value=25, maxStacks=1. 별도 Value=85 또는 100 정의도 maxStacks=1로 준비. 모두 유한 duration=300. 정의를 복제한 시험은 독립 기여 곱셈 검사이며 적용자 식별 검사와 구분 |
| 유료 지연 Cast Spec | Mana 100·재생 0, SkillData ManaCost=10, Mechanism delayTicks=120/cooldownTicks=180. 정상 스킬과 빈 슬롯, 지연 Projectile 또는 기존 공간 지원 Mechanism의 복제본을 배치. 입력/수락/실행 횟수를 기존 로그와 효과로 확인 |
| Switch 보존·중첩 변형 | Test_Variable의 Switch 스킬을 복제하여 Preserve Variable=true(Consume=false) 변형과 후속 Switch가 포함된 변형 준비. 원본 소비형과 구별되는 관측 효과를 사용. 하위 분기에도 별도 비용이 붙는다고 가정하지 않음 |
| Trigger 초과 요청 변형 | 성공형 Trigger의 +3 적용 Skill을 복제하고 StackMechanism.amount=5로 설정. 같은 Trigger 정의를 참조하여 한 요청의 임계값 초과를 시험 |
| 양쪽 진영 전투 씬 | 이름이 다른 시전자 2명과 피해 대상. 기존 소속/관계, Collider, 대상 허용 Mask를 확인. 적 피해를 허용할 대상에는 실제 이름이 AllowEnemyDamage인 Variable 정의를 CharacterVariables로 연결. displayName만 같은 정의는 부족함 |

기존 EnemyEntity의 Enable Triggerable Stack은 60 Tick마다 +1 수량/+1 payload, threshold 3, Fixed 자기 피해 예시입니다. 로그 `[TemporaryTrigger]`로 관측합니다. 이 예시는 DR의 영향을 받지 않으므로 DR 검증용으로 사용하지 않습니다.

## 3. 시험 항목

### T01 — 입력에서 실행까지, 빈 슬롯과 자원 거부

- 사전 조건: 유료 Cast Spec, Mana 100, 회복 0, 정상 슬롯과 빈 슬롯. 상태가 없는 새 Play 세션.
- 절차: 기본 공격 두 바인딩과 각 기술 슬롯을 누르고 놓아 실행합니다. 누른 채 잠시 유지해 해제 전후를 비교합니다. 빈 슬롯, CD 중 연타, Mana 10 미만일 때 요청도 시험합니다. 정상 요청은 CD를 기다린 뒤 반복합니다.
- 기대 결과: 유효 요청당 루트 비용 10과 실행 1회. 빈 설정/사전 거부에서는 비용·CD·Pending·후속 효과가 새로 생기지 않습니다. 재사용 가능 시 정상 요청은 다시 처리됩니다.
- 실패 판정: 입력 누락/중복 실행, 해제 전 의도하지 않은 실행, 거부 요청의 비용 소모, 후속 입력을 막는 Pending 잔류 또는 예외.

### T02 — 지연 중 이동, Target·Origin·Destination

- 사전 조건: delayTicks=120인 공간 지원 스킬. 고정된 명시 대상 시험은 AI TowardsEntity 구성을 사용하고, Player는 설정된 선택 모드를 기록합니다. Target, Origin, Destination을 구별하여 기록합니다.
- 절차: 요청 수락 후 시전자·마우스·대상을 이동시킵니다. 선택 당시 Target과 실행 위치를 로그/화면에서 비교합니다. 같은 시험을 10회 반복합니다.
- 기대 결과: 준비된 Target을 임의 재탐색하지 않습니다. 실행 시점 Origin과 설정된 Destination은 현재 지원 계약대로 계산됩니다. 지연 중 단순 이동/사거리 이탈 자체를 CC 취소와 혼동하지 않습니다. 실제 값은 [Cast 검증 계약](CastRefactorValidation.md)의 해당 스킬 설정과 대조합니다.
- 실패 판정: 다른 대상으로 몰래 전환, 오래된 위치/이전 요청 Context 재사용, 이동만으로 잘못된 중복 비용·실행. 지원하지 않는 Destination을 정상 스킬 기대값으로 사용하면 시험 설정 오류로 분류합니다.

### T03 — Pending 취소와 이미 실행된 효과의 수명

- 사전 조건: 유료 지연 스킬, 다른 시전자의 Suppressed/Stunned, 유한 수명의 Projectile 또는 Area.
- 절차: 비용 확정 후 실행 전 CC, 시전자 비활성화, 사망을 각각 별도 시행합니다. 다시 활성/재생성한 캐릭터로 새 Cast를 시도합니다. 별도 시행에서는 Projectile/Area가 실제 생성된 뒤 CC를 적용합니다.
- 기대 결과: 대기 Cast는 취소되어 나중에 실행되지 않으며 확정 Mana/CD는 환급하지 않습니다. Pending은 해제됩니다. 이미 실행된 효과는 해당 Mechanism의 독립 수명/종료 계약을 따릅니다.
- 실패 판정: 늦은 유령 실행, 비용 환급/중복 차감, 새 Cast 영구 불가, CC만으로 관련 없는 효과까지 삭제. 대상 무효화 전후는 T10에서 별도 판정합니다.

### T04 — Buff 총량·상한·반올림·해제

- 사전 조건: Test_DamageBoost/ArmorBoost/Haste/Slow/APBoost 및 Test_Rounding_Base5. 새 세션 또는 완전 만료 상태.
- 절차: +1 두 번과 +2 한 번을 비교하고, max=3 도달 후 10회 재적용합니다. -1, Clear, 자연 만료를 각각 수행합니다. 각 종류 20주기를 반복합니다.
- 기대 결과: 공격력 100 기준 120/상한130/해제100, 공격력5 기준 총량2에서6/해제5. 나머지는 정의의 Value×총량에 부합합니다. 갱신은 수량과 기간을 바꾸며 상한을 넘는 기여를 남기지 않습니다. AP는 총량10/20/30%이며 DR은 T05로 확인합니다.
- 실패 판정: 요청 배치에 따른 값 차이, 상한 추가 증가, 해제 후 잔여 수치, 관련 없는 Key의 기여 소실.

### T05 — DR 최종 배율과 실제 피해

- 사전 조건: DR 관측용 Spec/자기 피해 Skill. 공격100·방어0·AP0·증폭0·Shield0·재생0. 다른 Buff와 공격 효과를 끕니다. 매 단계 HP를 충분히 확보하거나 새 세션으로 시작합니다.
- 절차: DR 없음, 단일20%, 독립20%+25%, 단일85%, 독립85%+50% 또는100%를 각각 적용하고 동일 Normal 피해를 1회 가합니다. 10% Stack의 +1 두 번/+2 한 번, 상한 재적용, 일부 감소, 한 Key 제거, 전부 만료 후도 비교합니다.
- 기대 결과: 잔여 배율은 순서대로 1/0.8/0.6/0.15/0.15, HP 감소는 100/80/60/15/15입니다. 같은 Key 10%×2는0.8, 서로 다른 10% Key 둘은0.81입니다. 해제 후 남은 기여만 반영하고 전부 종료하면1입니다. Fixed 피해는 DR을 우회하는 현행 계약을 유지합니다.
- 실패 판정: Normal 피해가 DR만으로0이 됨, 독립 기여를 덧셈 합성함,0.15 하한 미유지, 제거/만료 후 배율 잔류. 실제 최종 정수 피해는 기존 ApplyDamage의 정수 변환을 따르므로 별도 수치에서 1 미만 피해가0이 되는 현상을 이번 배율 결함과 혼동하지 않습니다.

### T06 — CC 갱신과 서로 다른 Key의 독립 해제

- 사전 조건: Test_CC_Self, Test_CC_Projectile, 이름이 다른 두 시전자. 다른 이동/공격 제한과 AI는 끕니다.
- 절차: Rooted/Suppressed/Stunned/Tumbled 각각의 제한을 확인합니다. 동일 시전자의 CC Projectile을 동일 대상에 10회 명중시켜 갱신하고 마지막 명중 후180 Tick을 기다립니다. 두 시전자 CC는 간격을 두어 적용하여 먼저 적용한 것만 만료되는 구간을 관찰합니다. 20주기 반복합니다.
- 기대 결과: Rooted는 이동, Suppressed는 공격, Stunned/Tumbled은 둘 다 제한합니다. 같은 Key는 제한 기여1개이며 다른 Key가 남으면 해당 제한을 유지합니다. 모든 관련 Key 종료 후 이동·공격이 모두 복구됩니다.
- 실패 판정: 반복 횟수만큼 제한이 잔류, 한 Key 종료로 다른 Key 제한까지 해제, CC 종료 후 입력 영구 거부. 공격 제한 때문에 Clear 스킬을 못 쓰는 것은 예외가 아니므로 만료로도 검사합니다.

### T07 — Trigger 임계값·초과·새 주기

- 사전 조건: Test_Trigger_Success, 관측용 DamageBoost를 만료시킨 상태. 별도 Enemy 자기 피해 예시는 다른 시행으로 분리합니다.
- 절차: +1 두 번, 세 번째+1, 새 주기의+1을 차례로 적용합니다. +3 한 번과 임계값 초과 요청도 비교하고 20주기 반복합니다. Enemy 예시는 Enable Triggerable Stack을 켜고 payload 로그를 10회 확인합니다.
- 기대 결과: threshold 미만은 미발동, 도달 시1회 실행·전체 소비, 초과 이월 없음. 새+1은 새 주기의1이며 즉시 재발동하지 않습니다. Dummy 후속 Buff가 관측 수단입니다. Enemy 예시는 payload3당 Fixed 자기 피해3을 기본 기대값으로 사용합니다(Shield 등 없는 조건).
- 실패 판정: 동일 요청의 다중 발동, 이전 payload 재사용, 초과 이월, 직접 실행에 추가 루트 Mana/CD/Pending 부과. 관측 Buff 상한 때문에 변화가 안 보이면 먼저 Buff를 정리하고 재시험합니다.

### T08 — Trigger 준비 실패·0 증가 재시도·만료

- 사전 조건: 복제한 Test_Trigger_PrepareFailure 정의. PlayMode 애셋 변경이 저장될 수 있으므로 복제본만 편집합니다.
- 절차: +3으로 준비 실패를 만든 뒤 상태가 남는지 확인합니다. Destination을 Disabled로 고치고 새 요청 없이 대기한 후 Skill2(0 재시도)를 누릅니다. 별도 시행은 실패 상태로300 Tick을 넘겨 만료시킨 뒤 설정 수정·0 재시도합니다. 종료 후 원 설정으로 복원합니다.
- 기대 결과: 준비 실패는 수량/payload 미소비, 새 요청 전 자동 발동 없음,0 증가 요청으로 준비 성공 시1회 소비·실행. 만료 후 빈 상태의0 요청은 발동하지 않습니다.
- 실패 판정: 준비 실패 소비, Tick마다 자동 재발동, 만료 payload 재사용. 소비 확정 후 본문 예외의 무복구·재진입 큐는 자동 회귀 근거로 확인하고 수동 예외 주입을 위해 코드를 바꾸지 않습니다.

### T09 — Variable·Switch 선택·소비·Chain

- 사전 조건: Test_Variable, 소비형/보존형/중첩 Switch 변형. max3·period120을 기록합니다. 충전 Tick과 입력 시점을 구분합니다.
- 절차: +1 후 충전, +3 후 상한, 소비형 Switch,0 감소 후 충전 재개를 확인합니다. 보존형은 같은 상태에서 반복합니다. 중첩형은 루트 수락 후 새 Variable을 누적하고 후속 Switch 실행 전후 수량을 비교합니다. 해당 Variable이 없는 기본 분기도 시험합니다.
- 기대 결과: 선택한 분기와 후속 효과가1회 실행됩니다. 소비형은 수락한 정확한 Key의 캡처 수량만 소비하고, 보존형은 소비하지 않습니다. 새로 누적한 수량을 중첩 후속이 재소비하지 않습니다. 비용은 루트에서 한 번만 지출합니다.0으로 만든 Variable도 현행 주기 충전을 재개합니다.
- 실패 판정: 다른 Key 소비, 이중 소비, 기본 분기와 선택 분기 동시 실행, 늦은 보상이 이미 끝난 상태를 되살림. 주기 충전으로 증가한 수량을 소비 실패로 오판하지 않습니다.

### T10 — AI 명시 대상과 피해 허용 정책

- 사전 조건: AI TowardsEntity 스킬과 유효 적 대상, 적 피해 허용 Variable. 같은 거리의 다른 대상을 준비합니다.
- 절차: AI 활성 후 선택·Cast·실제 피해를 확인합니다. 지연 중 선택 대상만 비활성화/파괴하고 다른 대상은 남깁니다. 피해 허용 Variable을 뺀 별도 설정에서도 실행합니다.
- 기대 결과: 선택한 명시 대상에만 실행하며 무효 대상에서 임의 재탐색하지 않습니다. 확정 비용은 유지되고 Pending은 정리됩니다. 허용되지 않은 피해는 수신 정책에서 거부됩니다.
- 실패 판정: 다른 대상에 피해 전이, 유효 대상에도 설명 없는 무피해, 거부 대상 피해, 영구 Pending. 무피해를 DR 문제로 단정하기 전 관계 Mask와 AllowEnemyDamage의 실제 객체 이름을 확인합니다.

### T11 — Mechanism 후속 연동과 전투 순서

- 사전 조건: 기존 Projectile→onHit→Stack, Trigger→Dummy→Buff, Variable→Switch→후속 구성. 유한 수명 효과와 수신 가능한 대상을 사용합니다.
- 절차: 실제 명중으로 CC를 갱신하면서 다른 슬롯으로 Buff와 Switch를 사용합니다. 명중/미명중·효과 만료·대상 파괴를 별도 시행하고 실행 후 시전자에게 CC를 적용합니다.
- 기대 결과: 각 Mechanism의 기존 onHit/onExpire 계약에 따라 후속이 실행되고 다른 요청 Context가 섞이지 않습니다. StackMechanism.onExpire를 Stack 기간 만료 알림으로 가정하지 않습니다. 독립 효과의 종료와 후속 실행을 루트 Pending 취소와 구분합니다.
- 실패 판정: 중복 후속, 잘못된 적용자/Target 전달, 명중하지 않은 대상에 Stack 적용, 루트 비용 추가 청구, 이미 종료된 효과 재실행.

### T12 — 복합 전투 반복과 상태 누수

- 사전 조건: 이름이 다른 두 시전자와 대상, DR/공격 Buff·CC Projectile·Variable/Switch·Trigger를 배치한 복제 Spec. 각 기능의 단독 항목이 먼저 PASS여야 합니다.
- 절차: Buff 중첩→CC 갱신→지연 Cast 취소→Variable 소비→Trigger 발동을 한 주기로 하여30회 반복합니다. 중간에 대상 사망/재생성을 포함하고, 마지막에는 입력·AI·자동 Trigger를 모두 멈춘 뒤 가장 긴 유한 효과 기간과2 Tick 이상 기다립니다.
- 기대 결과: 최종 능력치·DR은 기준으로 복귀하고 CC 제한·Pending·일회성 Trigger 상태가 남지 않습니다. Variable의 의도된 주기 충전, 합의된 영구 상태, 다른 활성 효과는 별도로 기록합니다. 동일 조건에서 HP/Mana 변화와 발동 횟수가 재현됩니다.
- 실패 판정: 주기마다 누적되는 잔여 기여/예약/상태, 다른 Entity의 효과 소실, 새 Cast 불가, 중복 피해/소비, 새 예외. 총 메모리 사용량 증가만으로 누수를 확정하지 말고 살아 있는 게임 상태와 재현 조건을 함께 확인합니다.

### T13 — 비활성·파괴·재진입 Play 세션

- 사전 조건: 활성 Buff/CC/미발동 Trigger 및 Pending을 가진 시험 캐릭터. 다른 캐릭터에도 독립 효과를 적용합니다.
- 절차: 비활성화 후 기간을 넘겨 활성화하는 시행과 파괴하는 시행을 분리합니다. Play 종료·재시작을3회 하고 T01/T05/T06/T07의 대표 사례를 다시 실행합니다. 실제 출시용 씬에서도 기본 전투를10분 이상 시행합니다.
- 기대 결과: 비활성은 파괴와 달리 기존 Stack 수명/큐 정책을 유지하고 재활성 후 Tick에서 정리됩니다. 파괴된 소유자는 새 실행/보상으로 되살아나지 않으며 다른 소유자의 효과는 보존됩니다. 새 세션에 이전 수량·비용·제한이 넘어오지 않습니다.
- 실패 판정: 파괴 객체의 유령 실행, 이전 세션 상태 재사용, 이름/참조 충돌, 재활성 후 제한 잔류, 출시용 씬의 누락 참조. disable 중 새 요청 정책이나 영구 sentinel을 새 정책으로 바꾸어 판정하지 않습니다.

## 4. 릴리스 판단 기준

- DR 자동 회귀와 기존 Cast/Stack/Variable/Switch/입력/이동 자동 검사가 최신 소스에서 통과해야 합니다. 이번 수정 결과는 별도 안정화 보고서에 기록합니다.
- T01~T13에 대해 PASS 또는 구체적인 미실시/제외 사유가 기록되어야 합니다. 필수 시험이 BLOCKED이거나 관측 불가이면 아직 릴리스 가능 판정을 하지 않습니다. 수동으로 만들지 않은 예외·재진입 사례는 명시한 자동 검사 근거로 보완할 수 있습니다.
- 재현 가능한 새 컴파일/누락 참조/예기치 않은 예외, 정상 입력 불가, 비용·소비 중복, HP 계산 오류, 영구 CC/Pending, 종료 후 상태 누수는 릴리스 차단으로 보고합니다. 추가 결함은 자동으로 수정 범위를 넓히지 않습니다.
- 모든 관련 유한 효과가 종료된 복합30주기·Play 재시작3회·실제 씬10분 시험에서 설명할 수 없는 잔여 상태가 없어야 합니다. 실패를 수정했다면 해당 사례와 연결된 회귀를 다시 수행합니다.
- 최종 상태는 `자동 검증 완료 / 사용자 플레이 결과 대기`, `차단 결함 있음`, `테스트 완료·릴리스 승인 대기` 중 하나로 기록합니다. 사용자 결과 검토와 별도 승인 전에는 Version Bump·원격 push·릴리스 배포를 하지 않습니다.

결과 제출 시 실패/차단 항목을 먼저 적고, 시험한 씬·Spec·반복 횟수 및 첨부 로그를 함께 알려주십시오. 기존 Stack 테스트의 과거 성공은 이번 최종 통합 시험을 수행한 것으로 대체하지 않습니다.
