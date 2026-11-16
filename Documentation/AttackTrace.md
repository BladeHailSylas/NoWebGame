# 공격 로그 사용 및 검증 가이드

## Enemy 공격 문제 재현

1. Play Mode에서 빈 GameObject에 `AttackTraceSettings`를 하나 추가합니다. Play Mode에서 추가하면 장면 에셋을 저장하지 않고 시험할 수 있습니다.
2. `Caster`에 조사할 Enemy의 Entity 루트 Transform을 지정합니다. 자식 Collider나 투사체가 아닙니다. 비워 두면 전체 시전자를 출력합니다.
3. `Trace Enabled`를 켜고 Console의 로그 표시를 켭니다. `Detailed`는 처음에는 끕니다.
4. Console에서 `[AttackTrace]`를 검색하고 문제를 재현합니다. `AIDecision` 또는 `Input`의 `attack=` 번호로 같은 공격을 모읍니다. 숫자 뒤 공백까지 검색하면 다른 번호와 혼동하지 않습니다.
5. 마지막 성공 단계와 이어지는 `Rejected`, `Dropped`, `Cancelled`, `Skipped`의 `reason`을 확인합니다. 예외가 발생하면 `ExecutionException` 다음 Unity 예외 스택도 함께 보관합니다.

추적 설정은 기본적으로 꺼져 있습니다. 설정 컴포넌트는 장면에 하나만 사용합니다. Caster 필터는 출력만 제한하며 전투 처리를 제한하지 않습니다. 이름 뒤 `#`는 런타임 인스턴스 번호입니다. 실행을 다시 시작한 뒤 번호를 재사용해서 비교하지 마세요.

## 단계와 해석

| 단계 | 확인할 내용 |
|---|---|
| AIDecision / Input / InputQueued | AI 선택과 실제 입력 전달. Dash 또는 행동 불가 거절 여부 |
| TargetValidation / CastRejected | 활성 주기, 대상 존재·활성·targetable, 관계 마스크, 최소·최대 거리, 슬롯·쿨다운·Collector |
| Enqueued / Scheduled / Dispatch | 명령 수집, 지연 틱, 실행 전달. 등록은 실행 성공이 아님 |
| TargetQuery / TargetResolved / ExecutionRejected | 커서 탐색 및 실행 직전 명시적 대상 재검증 |
| Execute / ExecuteReturned | Mechanism 호출. 반환은 비동기 효과 완료 또는 피해 성공을 뜻하지 않음 |
| MeleeQuery / HitscanQuery / RayQuery / AreaQuery | 실제 쿼리의 위치·방향·범위·레이어와 후보 수 또는 단일 명중 결과 |
| Hit / TravelHit | Collider와 부모 Entity, 실제 명중 또는 제외 사유 |
| TravelStarted / TravelEnded / AreaStarted / AreaEnded | 이동·영역 효과 수명. 종료 Kind, Cause, 위치 및 후속 발행 여부 |
| Chain / ChainSkipped | onHit/onExpire 등의 원인, 자식 종류, 재탐색, 대상 상속 |
| DamageDelivered / DamageReceiverReturned | 실제 IVulnerable 구현에 전달. 단순 호출 반환은 HP 감소의 증거가 아님 |
| DamagePolicy / DamageRejected | 기존 InteractionFilter 관계 및 허용 변수 정책. 판정은 한 번만 수행 |
| StatsReceived / DamageSkipped / DamageApplied | 죽음·0 이하 값, 계산 결과, 체력·일반 보호막·특수 보호막 전후 변화 |
| DamageMath | Detailed를 켰을 때 방어력·관통·증폭·감소 값 |

`attack`은 루트 입력, `command`는 개별 기술 명령, `parent`는 부모 명령, `effect`는 실행 효과, `damage`는 개별 피해 전달입니다. 자식 명령은 동일한 attack을 유지합니다. 명령 동등성/해시에는 진단 번호를 포함하지 않습니다. tick은 현재 게임 Ticker 값이며 순환할 수 있으므로 `seq`로 출력 순서도 확인합니다. 직접 호출 또는 외부 피해의 추적 값이 없으면 0으로 표시될 수 있습니다.

`selectionRelations`는 AI의 관계 마스크, `queryLayers`는 물리 검색 레이어, `blockerLayers`는 장애물 레이어, `damagePolicyRelation`은 기존 피해 허용 정책입니다. 서로 다른 책임이며 이번 변경은 이 규칙들을 통합하지 않습니다. 쿼리에서 제외된 Collider는 후보 로그에도 나오지 않으므로 쿼리 마스크와 Inspector의 Collider Layer를 비교하세요. 자식 Collider를 Entity로 변환하는 기존 경로별 차이도 그대로 유지합니다.

## 집중 확인 항목

- 정상 공격: 동일 attack 아래 입력→명령→명중→피해 흐름 확인. DamageDelivered의 receiver가 예상 구현인지 확인.
- 대기 중 대상 파괴, 비활성화, targetable 해제, 사거리 이탈, AI 비활성화: 실행 거절 이유 확인. 커서 대상으로 바뀌지 않아야 함.
- Foe의 You 공격: AI 선택 관계와 실제 명중 쿼리 레이어를 따로 비교. 선택 성공 후 명중 0건을 피해 정책 거절과 구분.
- 피해 허용 변수 없음: DamagePolicy allowed=False 뒤 DamageRejected, 체력 변화 없음.
- 보호막 흡수: DamageApplied에서 healthDelta=0이어도 보호막 감소가 보이면 피해 처리는 수행된 것임.
- 여러 Enemy 동시 공격: Caster 필터 및 attack/parent 연결이 섞이지 않는지 확인.
- Projectile/Dash 종료: ExecuteReturned 후 별도의 TravelEnded가 한 번 발생하고 원인이 정확한지 확인.
- 로그 끄기: 같은 전투 결과 유지. 일반 입력 거절은 오류 경고로 승격하지 않음.

자동 검사: 저장된 장면의 Edit Mode에서 `Tools > ExitLab > Check Attack Trace`. 기존 AI 검사가 미저장 장면을 보호하기 위해 실행을 거절할 수 있으므로 저장하거나 별도 빈 검증 프로젝트를 사용하세요. 출력은 프로젝트 상위의 `attack-trace-tests.txt`, `attack-trace-sample.log`입니다.

별도 런타임 파일 수집 기능은 추가하지 않았습니다. Console/Unity Editor 로그를 사용합니다. 로그 문자열 생성 및 상세 출력에는 비용이 있으므로 진단 후 끄세요. 기존 다른 시스템의 로그까지 일괄 제거하지 않습니다. 실전 Enemy 문제의 원인은 재현 로그를 수집한 뒤 판단해야 합니다.
