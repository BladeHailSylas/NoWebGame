# 임시 적 AI 사용 및 테스트 가이드

## 구현 범위

임시 AI는 실제 수신한 60틱마다 가까운 적을 선택하여 일반 Attack 입력을 요청합니다. 적이 없으면 정규화된 8방향 중 하나로 다음 판단까지 이동합니다. 첫 판단도 활성화 후 60틱입니다. 경로 탐색·추적·벽 시야 검사·쿨다운 시스템 보완은 포함하지 않습니다.

AI가 선택한 Target은 요청별로 보관하며 ActBridge → Attacker → CommandCollector → SkillRunner → Mechanism 경로를 통과합니다. 명시적 AI 루트 공격은 요청 시점과 실행 직전에 존재·활성·targetable·사거리·관계를 검사합니다. 실패하면 취소하며 사용자 커서로 재탐색하지 않습니다. AI 비활성화는 해당 활성 주기의 대기 중 루트 공격도 취소합니다.

기존 플레이어의 슬롯 전용 입력은 그대로입니다. EnemyEntity의 AI는 기본적으로 꺼져 있어 기존 Enemy가 자동으로 공격을 시작하지 않습니다.

**아직 상대적 Target Mask의 2차 전체 전환은 아닙니다.** `AI Target Relations`는 임시 선택 조건입니다. 기존 SkillData의 LayerMask 및 Melee/Area/Ray/Projectile 명중 처리는 변환하지 않았습니다. AI 선택 성공만으로 모든 명중 필터가 상대화되었다고 판정하면 안 됩니다.

## 가장 간단한 수동 확인

1. Unity 6000.3.10f1에서 컴파일 완료를 기다립니다.
2. `Tools > ExitLab > Open Temporary AI Playground`를 선택합니다. 현재 Scene의 저장 여부를 묻는 창이 나오면 보존할 편집을 저장하거나 취소합니다. 메뉴는 확인 후 **새로운 미저장 빈 Scene**을 엽니다. 기존 Battlefield를 덮어쓰지 않습니다.
3. Play를 누릅니다. 서비스·적·표적·카메라·Attack 정의가 메모리에 생성됩니다. 새 SkillData 에셋 파일은 생성되지 않습니다.
4. Game 화면의 `Decisions`, `Target`, `Hits` 표시를 확인합니다.

색상: 빨강=AI Caster, 하늘색=You, 파랑=Ally, 노랑=Foe, 초록=명시적 Neutral.

초기 Caster는 Foe이고 Enemy 선택 조건을 사용합니다. 가장 가까운 You 표적을 약 60틱마다 공격해야 합니다. 하늘색 표적의 Hits만 증가하며 실제 실행 지연 2틱과 큐 처리 시간이 있으므로 결정 순간 바로 Hits가 증가해야 하는 것은 아닙니다. 표적은 일반 피해 시스템 전체 대신 실제 DamageMechanism 호출 횟수를 세는 검증용 Entity입니다.

## 수동 시나리오

| 조작 | 기대 결과 |
|---|---|
| 마우스를 표적 반대쪽/빈 공간으로 이동 | 공격 대상에 영향 없음 |
| 초기 상태 유지 | You Hits 증가. Foe/Neutral Hits는 증가하지 않음 |
| `Toggle You targetable` | 다음 판단부터 Ally로 전환. 이미 지연 중인 You 공격은 실행 전 검사에서 거부 |
| `Move all targets far / return near` | 표적을 멀리 보내면 다음 판단부터 배회. 다시 누르면 현재 AI 위치 근처에 돌아옴 |
| 배회 중 몇 틱 관찰 | 다음 판단까지 같은 방향으로 이동. 벽 회피나 추적을 기대하지 않음 |
| `Disable AI` | 이동 입력과 새 판단 중단. 대기 중 AI 루트 공격 취소 |
| `Enable AI` | 새 60틱 주기부터 판단 재개. 중복 입력 없음 |
| 표적을 가까이 둔 채 `Toggle caster Foe / You` | 같은 Attack 정의를 유지한 채 적합한 Foe 표적(노랑)으로 선택 반전 |
| Hierarchy에서 표적 Entity 또는 GameObject 비활성화 | 후보에서 제외되고 지연 중 요청도 취소 |
| Play 종료 | 런타임 생성 오브젝트·메모리 설정 종료. 원본 Scene/SkillData 변경 없음 |

검증 Scene에는 벽이 없습니다. 기존 벽을 사용한 추가 테스트는 별도 테스트 Scene에 Walls&Obstacles Layer와 Collider2D를 가진 벽을 배치해 확인할 수 있습니다. 현재 직접 대상 Damage 공격은 물리 발사체가 아니므로 벽 차단 테스트용이 아닙니다.

실시간 정확히 1초가 아니라 **Ticker 이벤트 60회** 기준입니다. Console 로그와 화면의 Decision 수로 비교하십시오. 로그는 `[TemporaryAI]` 판단, 기존 Command 등록, `[TemporaryAI Hit]` 실제 직접 공격 적용으로 구분됩니다.

## 기존 검증용 Enemy에 연결

별도 Scene/Prefab 사본에서만 시작하십시오.

1. TickerRunner 및 공용 CommandCollector를 먼저 구성합니다. 적마다 새 Collector를 추가하여 전역 Instance를 덮어쓰지 마십시오.
2. EnemyEntity와 Rigidbody2D, Collider2D, 유효한 CharacterSpec을 연결합니다. Caster의 Entity Layer는 Foe, Relation Profile은 LegacyLayer로 설정합니다.
3. CharacterSpec.attack의 바인딩 Mode를 TowardsEntity로 지정하고 일반 공격 SkillData를 연결합니다. 루트의 MinRange/MaxRange는 0 이상이며 MaxRange는 양수여야 합니다.
4. EnemyEntity의 Command Collector에 공용 Collector를 지정하고 Temporary AI Enabled를 켭니다. Decision Interval=60, Random Seed는 적별로 다르게, 필요하면 Log AI Decisions를 켭니다.
5. AI Target Relations=Enemy로 시작합니다. 이 필드는 기존 Mechanism Mask를 자동 변환하지 않습니다.

지원되는 Chain 노드는 Damage, Stack, Dummy, Melee, Hitscan, Ray, Area, Projectile입니다. Switch/Detect/Dash/Teleport/Summon, 순환 Chain, requireRetarget 자식은 임시 AI에서 거부하고 경고를 남깁니다. AI의 대상 획득 방식을 자식 Chain에 전달하는 기능은 아직 없습니다.

실행 중 Attack 그래프와 설정을 바꿔 시험하려면 AI를 껐다 켜십시오. 초기화 이후 Collector 교체는 지원하지 않습니다. 소스에서 구성할 때에는 비활성 오브젝트에 InstallSpec 및 ConfigureTemporaryAI를 호출한 뒤 활성화할 수 있습니다.

일반 공격 입력 주기는 60틱이지만 기존 Attacker에는 쿨다운 신규 등록이 완성되어 있지 않습니다. 임시 AI의 주기를 Skill 쿨다운 검증으로 해석하지 마십시오. 이미 실행되어 생성된 Projectile/Area의 수명이나 후속 Chain 취소는 AI 루트 요청 취소와 별개이며 이번 작업은 이를 바꾸지 않습니다.

## 자동 집중 검증

2026-11-13 검증 결과: 별도 사본의 Unity 6000.3.10f1에서 AI 39개, 실제 Play Mode 5개, 기존 관계 판정 407개, 시전 원점 회귀 21개가 통과했습니다. Play Mode 검증은 화면 없는 실행으로 공격·물리 이동·정지를 확인한 것이며 Game 화면의 시각적 배치는 수동 확인 대상입니다.

Edit Mode에서 `Tools > ExitLab > Check Temporary Enemy AI`를 실행합니다. 이름 없는 미저장 Scene이 열려 있다면 먼저 사용자가 원하는 위치에 저장하거나 원래 Scene으로 돌아오십시오. 검증은 기존 Scene을 자동 저장하지 않고 별도 임시 Scene과 메모리 스킬을 만들어 검증한 뒤 닫습니다. 결과는 프로젝트 상위 `audit-results/temporary-ai-tests.txt`에 기록됩니다.

검증 항목: 60/120틱 판단, 최근접 Enemy 선택, 부모 Entity/자식 Collider, 중복 Collider, 실제 명령 지연 후 Damage 실행, Caster 진영 반전, AI 중지·재활성화, 이동 입력 유지, 실행 시 사거리/관계/targetable/활성/파괴 재검증, 입력별 Target 분리, 기존 플레이어 입력 보존, 지원하지 않는 Chain 거부.

배치 재현 진입점은 `ExitLab.TemporaryEnemyChecks.Run`입니다. 별도 검증 사본에서 실제 Play Mode와 Rigidbody2D 이동을 확인하는 진입점은 `ExitLab.TemporaryAIPlayModeChecks.Begin`입니다. 후자는 완료 시 Unity 프로세스를 종료하므로 **batch 전용**이며 `-quit`를 붙이지 않습니다. 사용 중인 원본 프로젝트와 동시에 같은 경로로 batch 실행하지 마십시오.

## 실패 기록

Scene/브랜치, Caster Layer·프로필, Attack 이름·바인딩 Mode·사거리, AI 관계 조건, 결정 틱, 선택 Target, 명령 등록 여부, 실제 명중/피해 결과, Console 첫 오류를 기록하십시오.

- 후보가 없으면 Entity 활성/targetable/프로필/사거리/Collider를 확인합니다.
- 선택되지만 요청이 없으면 CC·Dash 입력 제한, Attack 구성, Collector를 확인합니다.
- 요청됐지만 직접 공격이 취소되면 실행 전 조건 변화나 AI 비활성화를 확인합니다.
- Melee/Projectile/Area의 잘못된 명중은 기존 물리 Mask 제한일 수 있습니다. 2차 전환 전에는 AI 입력 결함과 구분해야 합니다.

2차의 공통 명중 필터 전환과 3차 SkillData 마이그레이션은 이 기능의 구현에 포함되지 않습니다.
