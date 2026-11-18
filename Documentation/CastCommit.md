# Cast Commit (Phase 3)

## 비용과 공개 경계

루트 입력은 기존 CC/binding 검사, 시전자 활성·생존, Pending/CD/같은 Tick 중복 검사, Target/Switch 준비, 자원 검사 후 Commit합니다. PrepareCast는 여전히 선택·소비·예약을 수행하지 않습니다.

`SkillData.ManaCost`는 현재 Mana와 같은 내부 단위의 int입니다. 기본 0, 음수는 런타임 거부합니다. Inspector에는 Min(0)을 적용했습니다. `TrySpendMana`는 부족하거나 음수면 false를 반환하고 값을 바꾸지 않습니다. 기존 ApplyManaCost는 호환용 포화 차감 API로 남았지만 음수는 거부하며, Commit은 안전한 TrySpendMana를 사용합니다. 자연 회복·표시 배율은 변경하지 않았습니다.

Collector의 내부 루트 수락 절차는 Delay 예약 및 비공개 수집 등록 → CD 예약 → Variable 변경 → Mana 차감 → Pending/같은 Tick 기록 → 소비 확정 → Committed 공개 순서입니다. 중간 실패 시 원래 Mana·Variable·충전 예약·슬롯 상태를 복구하고 새 예약을 제거합니다. 이 복구는 **공개 전 실패 보상**이며 Commit 후 환급 기능이 아닙니다. Unity 단일 스레드 계약입니다.

CommandEnqueued 관측자는 완료된 Commit만 봅니다. 관측자 예외는 별도로 기록하고 다음 관측자에게도 전달합니다. 관측 실패 때문에 이미 수락한 공격을 실패로 보고하거나 다시 비용을 내게 하지 않습니다.

## 시간과 슬롯

- CD는 기존 Mechanism.CooldownTicks를 슬롯별로 CommitTick에 등록합니다. 0은 예약 없음, 음수는 거부합니다.
- 루트 DueTick은 CommitTick + DelayTicks입니다. Collector가 늦게 수집해도 Delay를 재시작하지 않습니다.
- Pending은 CD와 별개입니다. Delay가 진행 중이면 CD가 먼저 끝나도 같은 슬롯을 재발동할 수 없습니다.
- Pending은 dispatch 직전에 해제합니다. 이후 효과/투사체 전체의 수명까지 슬롯을 막지 않습니다. 실행 거부·예외·명시적 전체 큐 취소에서도 지불한 비용은 돌아오지 않습니다.
- 같은 슬롯은 같은 Tick에 최대 한 번 Commit합니다. Delay/CD가 0이고 같은 Tick에 실행을 마쳤어도 이 제한은 유지됩니다.
- 0 Delay는 다음 실행 가능한 Collector 순회에서 실행합니다. 입력 구독이 Collector보다 먼저면 같은 Tick, 뒤면 다음 Tick일 수 있습니다. 입력 함수에서 즉시 효과를 실행하지 않습니다.
- Chain은 기존 이중 버퍼를 유지하고 다음 Collector 순회에서 수집합니다. 루트의 Commit 객체와 Mana/CD를 상속하지 않습니다.

`CastCommit.State`는 Prepared → Committed → Executing → ExecutionReturned 또는 Cancelled/ExecutionException/DispatchAborted를 표현합니다. ExecutionReturned는 실행 함수가 반환했다는 뜻으로 효과 완료나 명중 성공을 보장하지 않습니다. Runner가 Target 유효성 때문에 실행을 거부하면 DispatchAborted와 사유를 남기며 비용은 유지됩니다.

## Switch

코드의 `Consume` 기본값은 true입니다. 저장 필드는 반대 의미의 `preserveVariable`입니다. 옛 직렬화에 필드가 없으면 0(false)이므로 Consume=true가 됩니다. Inspector에서 **Preserve Variable을 켜면 Consume=false**입니다. 기존 에셋을 일괄 재저장하거나 옛 슬롯 값으로 덮어쓰지 않습니다.

루트는 case 순서대로 공개 Variable을 선택하고, 동일 종류에 여러 StackKey가 있으면 적용자 이름 순으로 선택합니다. 선택한 정확한 Key와 현재 Amount 전체를 증표에 보관합니다. ID만 일치하는 다른 적용자의 스택을 제거하지 않습니다. 공개 상태 및 실제 수량을 소비 직전에 재검증합니다.

- Consume=true: 루트 Commit에서 소비합니다.
- Consume=false: 분기 자료와 미소비 증표를 유지합니다.
- Variable 없는 기본 분기: 소비하지 않습니다.
- 선택한 분기 MechanismData와 소비 정책은 수락 시 복사하여 Delay 중 재선택하지 않습니다. Var 자료는 소비 후에도 자식에게 전달됩니다.
- 증표를 상속한 Consume=true 자식은 아직 미소비일 때에만 큐 수락 중 소비합니다. 이미 지불한 증표를 가진 자식은 새로 획득한 스택을 다시 차감하지 않습니다.
- 서로 다른 증표가 동일한 스택을 소비하려 하면 남은 실제 자원을 재검증합니다. 없으면 후속 명령을 수락하지 않습니다.
- Collector의 기존 실행 후 무조건 TryRemoveStack 호출은 제거했습니다.

StackManager는 실패 보상을 위해 기존 충전 핸들을 성공 확정 전까지 보존합니다. 성공 시에만 교체된 옛 핸들을 제거합니다. 원래 진행 중인 충전은 계속 진행하고, 최대 스택에서 소비하여 충전을 시작해야 할 때는 소비 Tick을 기준으로 예약합니다.

직접 `SwitchMechanism.Execute` 호출은 큐 수락 경계가 없습니다. Var 없는 기본 분기와 Consume=false는 기존처럼 실행할 수 있지만, 미지불 Var를 제공한 Consume=true 직접 실행은 명시적으로 거부합니다. 현재 Stack 트리거 직접 실행은 Var 없이 호출합니다. 소비가 필요한 새 외부 호출은 StackManager에서 증표를 확보하여 Collector를 통해 수락시켜야 합니다.

## Phase 4 — 취소·수명·예외

Suppressed/Stunned/Tumbled 적용, 사망 확정, 시전자 비활성화/파괴, PreventActivation Dash 시작은 해당 시전자의 미처리 입력과 미실행 루트를 취소합니다. Rooted는 공격을 금지하지 않으므로 이동만 제한합니다. 중첩 CC 카운터는 int를 사용하여 256개에서 0으로 돌아가지 않습니다. 사망은 다음 Entity Tick을 기다리지 않고 Stats의 사망 전이에서 알립니다.

취소는 Delay 핸들과 Pending만 해제합니다. Mana·소비 Variable·Cooldown은 환급하지 않습니다. disable/enable에는 CD가 유지되며, 영구 파괴 시에는 더 이상 조회되지 않을 CD 핸들을 정리합니다. 입력은 소유자와 Collector의 세대 번호를 기록하므로 취소/서비스 재시작 이전 입력이 나중에 살아나지 않습니다. Player·Enemy·Summon은 재활성화 시 새 입력과 Tick 구독을 재개합니다.

실행 직전에는 시전자의 활성/생존/공격 가능 상태 및 Collector 유효성을 확인하고 기존 Target 검증을 수행합니다. 사거리는 재검사하지 않습니다. 실행에 들어간 루트는 Pending에서 제거되어 자신이 시작한 Dash나 재진입 CC로 취소되지 않습니다. 이미 생성된 Projectile/Area 및 Chain에는 루트의 취소 상태를 상속하지 않습니다. 효과는 기존 Target 없음, 충돌, 자체 시간 만료 등에 따라 끝납니다. Dash의 기존 이동 불가·소유자 비활성화 종료 조건도 유지합니다. 시전자가 파괴되어 Caster 참조 자체가 사라지면 기존 효과/Chain의 참조 검증에 따라 종료 또는 거부될 수 있습니다.

Collector 자체의 disable/destroy와 CeaseCommand는 **전체 대기 큐(후속 명령 포함)**를 정리합니다. 활성 효과를 찾아 파괴하지는 않지만, 서비스가 없는 동안 제출된 후속 명령은 거부하며 재시작 후 재생하지 않습니다. 이는 시전자별 루트 취소와 다른 서비스 수명 경계입니다. TickerRunner만 일시 정지하는 경우에는 기존 시간·예약 보존 계약을 유지합니다.

입력, 예약, 실행의 예외를 항목별로 격리하여 다음 항목을 계속 처리합니다. 항목은 외부 콜백 전에 큐에서 분리하고 종료 상태/정리는 한 번만 기록합니다. 실행 후 표시 실패는 DisplayException으로 기록하며 실행 성공 상태를 유지합니다. 관측자 예외도 비용·실행 결과를 되돌리지 않습니다. Collector 순회 재진입은 무시하고 새 후속 명령은 다음 정상 순회에서 처리합니다. Commit 로그에는 ID, 슬롯, Mana 비용, Commit/Due Tick, 상태와 사유를 남깁니다.

## 자동 검증

별도 검증 사본에서 `ExitLab.CastCommitChecks.RunWithRegression` 및 `RunPlayMode`를 실행합니다. 신규 검사는 Mana 부족/음수, 같은 슬롯·다른 슬롯 경합, CD와 Delay의 길이 차이, 구독 순서, 0 Delay, 예약 실패 및 공개 전 예외 주입, 관측자 예외, Switch 키·스냅샷·단일 소비, 실제 중첩 SerializeReference 저장/재로드를 다룹니다. 실제 에셋 파일 재로드 검사는 EditMode에서만 수행합니다.

Phase 4는 `ExitLab.CastLifecycleChecks.RunWithRegression` 및 `RunPlayMode`로 위 회귀와 취소·실패 주입·실제 Entity 콜백·효과 수명 검사를 수행합니다. 프로젝트 원본이 아닌 marker가 있는 batch 검증 복사본에서만 실행합니다. 수동 전투 검증과 전체 Phase 5 완료 여부는 별도 보고서에서 관리합니다.

## Phase 4 마감 및 알려진 결함

사용자가 Delay 중 CC/시전자 비활성화에 의한 미실행 Root 취소와, 이미 실행된 효과의 후속 Chain 유지를 확인했습니다. Delay 중 사망과 Collector 재시작은 수동 미실행이며 자동 검사 근거만 유지합니다. 이 구분을 보존한 상태에서 Phase 4 종료·커밋 및 Phase 5 진행을 승인받았습니다.

Enemy AI Projectile의 Chain StackMechanism을 통한 중복 CC 부여 시 행동 제한 카운터가 잔류할 수 있습니다. 미해결 후속 과제 **STACK-CC-01**로 등록했으며 자세한 재현 경로·조사 범위·완료 기준은 `Documentation/StackSystemFollowUps.md`를 따릅니다. Phase 4의 직접 카운터 증감 검사를 실제 Stack 수명 검증으로 확대 해석하지 않습니다.
