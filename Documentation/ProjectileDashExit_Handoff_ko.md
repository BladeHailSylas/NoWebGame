# Projectile / Dash 최소 격리 시험 환경 인계

작성: 2026-10-11 (한국 시간). 기준 명세: `audit-results/Projectile_Dash_Exit_Test_Spec_2026-10-10.md`.

## 현재 완료 상태

별도 씬과 최소 Projectile 프리팹을 Unity에서 생성했습니다. 추가 C# 코드는 에디터 컴파일을 통과했고, 저장된 씬/프리팹 참조·목표 형태·레이어·투사체 물리 설정은 Play 없이 검사했습니다. **게임플레이 시험, Tick 실행, Physics2D.Simulate 실행, Mechanism 버그 수정은 하지 않았습니다. 아래 예상값은 실행 결과가 아닙니다.**

- 저장소: `BladeHailSylas/NoWebGame`, 로컬 `audit-NoWebGame`, `dev`, 기준 HEAD `6eace59aae8d3b9ba7e7df798a2c8a5f0504fd4b`.
- Unity: 설치된 `C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe` 사용.
- Test Framework: manifest 선언 `1.6.0`. 패키지 가져오기 및 C# 컴파일은 완료되었으나 Test Runner 시험은 실행하지 않았습니다.
- 원본 Battlefield, Mechanism/Motor/시간 코드, 기존 Target 표현, 프로젝트 설정 및 패키지 manifest는 최종 변경 범위에 포함하지 않았습니다.
- 새 Target 타입은 없습니다. `ExitTarget`은 시험 배치 선택용 enum이며 모든 실제 입력은 기존 `Transform`입니다.

## 변경 파일

| 경로 (저장소 기준) | 역할 |
|---|---|
| `Assets/ExitLab/Generated/ProjectileDashExit.unity` | 별도 빈 씬, 카메라, harness, 비활성 목표 3종 및 중간 적 |
| `Assets/ExitLab/Generated/MinimalProjectile.prefab` | 실제 ProjectileEntity + 같은 객체의 Rigidbody2D/CircleCollider2D, private rb/col 직렬화 참조 |
| `Assets/ExitLab/ExitLabHarness.cs` | 사례 선택, 명시적 시작, 시간 초기화, 실제 경로 호출, JSONL 관측 |
| `Assets/ExitLab/ExitLabEntity.cs` | 기존 Entity를 상속한 의존성 없는 시험용 개체 |
| `Assets/ExitLab/ExitLabLifecycle.cs` | 실제 Projectile OnDestroy 시점 관측 |
| `Assets/ExitLab/Editor/ExitLabBuilder.cs` | 별도 씬/프리팹 생성, Play 없는 구성 검증, 원본 씬 GUID 조회 |
| 위 파일 및 폴더의 `.meta` | Unity GUID/참조 유지. 함께 보관해야 합니다. |
| `Documentation/ExitLabDependencyAudit.md` | 원본 Battlefield의 누락 외부 GUID 목록 |
| `Documentation/ProjectileDashExit_Handoff_ko.md` | 이 인계 문서 |

저장소 밖 `audit-results/exit-fixture-build.log`, `exit-fixture-final-validation.log`, `exit-fixture-final-check.log`는 편집기 검증 증거입니다. 최초 `exit-fixture-baseline.log`는 중복 실행 시도와 첫 프로세스의 출력이 겹쳤으므로 최종 성공 판정 근거로 사용하지 않습니다. Git 커밋/원격 push는 하지 않았습니다.

## 검증 범위와 환경 장애

**확인됨:** 지정 Unity 버전 실행, C# 컴파일, Unity 씬/프리팹 저장, projectile rb/col 연결, Kinematic/중력 0/보간 None/반경 0.1/trigger/Default 레이어, Collider 없는 Transform, Entity와 Collider가 같은 객체인 목표, Entity 루트와 Collider 자식이 분리된 목표, Foe=7/Walls&Obstacles=3. 원본 씬을 열지 않고 AssetDatabase GUID 조회도 완료했습니다.

**미검증:** Play 진입과 버튼 UI, runtime 초기화 및 reflection 관측, 실제 CircleCast 결과, 모든 T01–T17 결과, 실제 이동/종료 시점, 후속 등록·실행 횟수, 재진입 시 static 정리, 시각 표시, Player 빌드. 컴파일 성공을 runtime 성공으로 간주하지 않습니다.

**환경 주의사항:**

1. Unity 어셈블리 검사에서 `Library/PackageCache/com.unity.collections@aea9d3bd5e19/Unity.Collections.Tests/System.Runtime.CompilerServices.Unsafe/System.Runtime.CompilerServices.Unsafe.dll` 경로에 `DirectoryNotFoundException`이 기록되었습니다. 해당 실행에서도 C# 컴파일과 별도 씬 검증은 완료되고 Unity는 0으로 종료했습니다. 패키지 환경이 완전히 깨끗하다는 뜻은 아닙니다. 패키지 삭제/재설치나 DLL 복사는 하지 않았습니다. 다음 편집기 실행에서 이 예외가 재발하거나 Play를 막으면 먼저 환경 장애로 기록하고 게임 동작 판정을 보류하세요.
2. 원본 Battlefield에서 경로를 찾지 못한 외부 GUID 10종: CharacterSpec 2종, Stack 정의 6종, Sprite 1종, Font asset 1종. 상세 GUID와 행은 `ExitLabDependencyAudit.md`에 있습니다. 초기 checkout의 원본 prefab은 0개였습니다. 별도 fixture는 이 콘텐츠를 사용하지 않습니다. 사용자의 다른 원본 프로젝트에도 파일이 없다는 의미는 아닙니다.
3. 기존 코드의 CS0108, CS8618 경고는 수정하지 않았습니다. harness는 명세의 AutoSyncTransforms=false 조건과 종료 시 복원을 위해 deprecated 속성을 사용하므로 CS0618 경고가 있습니다. 매 Tick SyncTransforms로 대체하면 관찰하려는 순서가 달라지므로 임의 변경하지 않았습니다.
4. GUI·그래픽 장치 시험 대신 `-batchmode -nographics` 에디터 검증만 했습니다. Unity가 최초 import 중일 때 두 번째 실행은 잠금으로 종료 코드 1을 반환했으며, 이후 단일 실행에서는 정상 종료했습니다.

## 구성과 의도적인 경계

- 시작 전에는 Ticker/Collector/Projectile/Dash가 생성되지 않습니다. **Play 버튼만 눌러도 시험이 자동 시작되지 않습니다.** Game 뷰의 별도 시작 버튼이 실제 실행을 시작합니다.
- 한 번의 Play에서 한 사례/한 actor만 실행합니다. Reload Domain을 켜야 하며 다른 씬을 additive로 함께 열면 시작을 거부합니다. static Ticker/기존 Collector/Runner가 있으면 시작을 거부합니다.
- Projectile은 실제 `ProjectileMechanism.Execute → Instantiate → ProjectileEntity.Init`을 사용합니다.
- Dash는 실제 `Mover.StartDash → DashTick → FixedMotor.TryDash → EndDash`를 사용합니다. **PlayerEntity/ActBridge/DashMechanism.Execute 통합 시험은 아닙니다.** Mover 생성자의 Context/Stats는 이 Dash 경로에서 사용하지 않아 null입니다. 일반 이동/Knockback은 호출하지 않습니다. EndTick은 시작 Tick+120인 절대값으로 직접 지정합니다.
- 각 onHit/onExpire 목록에 관측용 후속 하나를 넣습니다. 실제 `SkillUtils.ActivateChain → CommandBridge → CommandCollector → SkillRunner → Marker.Execute`를 거칩니다. 피해는 없습니다. requireRetarget=false, 유효한 명시적 Transform을 사용해 커서/Root 탐색을 우회합니다. 빈 목록/Anchor 반환 수명은 이번 fixture의 시험 대상이 아닙니다.
- 목표 Entity는 스탯/입력 없는 기존 Entity의 단순 파생형입니다. Entity.targetable 정책이나 피해 적용까지 검증하지 않습니다. 자식 Collider에는 Entity를 붙이지 않습니다.
- 모든 시험 Collider는 trigger입니다. 모터의 명시적 cast로 종료를 관찰하고 물리 접촉 반응을 최소화하려는 **fixture 선택**이며 원본 콘텐츠의 물리 구성을 복제한 것이 아닙니다.
- 원본 Build Settings에 시험 씬을 추가하지 않습니다. harness/Entity/관측 코드는 `UNITY_EDITOR` 전용입니다. Player 빌드용 테스트 씬이 아닙니다.

## 시간·물리·레이어

| 항목 | 구성 |
|---|---|
| 공통 | z=0, 시작 (0,0), life/duration=120, timeScale=1 |
| 기본 속도 | 6; T02만 60, T14/TargetShape는 Inspector 값 |
| self | Default(0), 벽/적 query mask에서 제외 |
| 적/벽 | Foe(7), Walls&Obstacles(3). 벽 오브젝트는 기본 우선 사례에서 사용하지 않음 |
| 크기 | Projectile Collider 반경 0.1, Dash Collider 0.5; Dash cast는 원본 고정 정책 0.51 |
| 목표 | Entity Collider 반경 0.2, trigger=true; 자식 Collider도 Foe |
| 공통 물리 | queriesHitTriggers=true, queriesStartInColliders=true, autoSyncTransforms=false, 보간 None |
| 초기 배치 반영 | 시험 생성/배치 후 SyncTransforms 1회. 매 Tick 강제 동기화 없음 |
| ManualTickThenPhysics | 원본 Ticker.Step 1회 → Physics2D.Simulate(1/60) 1회. 자동 물리 OFF, TickerRunner 없음 |
| ProductionCoroutine | 원본 TickerRunner의 WaitForSecondsRealtime(1/60), 자동 FixedUpdate 물리 0.02. 수동 Step/Simulate 없음 |

위 설정은 시작할 때 runtime에서 적용하고 Play 종료 시 이전 값을 복원합니다. 프로젝트 설정 파일은 바꾸지 않습니다. 원본 MaxTranslationSpeed는 100이며 runtime 로그에도 실제 값을 남깁니다. Production에서는 TickerRunner.OnEnable이 fixture보다 먼저 빈 Tick 1회를 실행하므로 Manual과 절대 Tick 번호가 다를 수 있습니다. `ready.tick`을 기준으로 상대 Tick을 계산하세요.

## 다음에 돌아와서 시작하는 절차

1. Unity Hub에서 **`audit-NoWebGame` 폴더**를 Unity **6000.3.10f1**로 여세요. 원래 작업 중인 다른 NoWebGame 폴더와 혼동하지 마세요.
2. 패키지 import/컴파일 완료를 기다리세요. Console의 오류를 확인하세요. 위 DLL 예외 또는 새 오류가 Play를 막으면 먼저 환경 장애로 기록하고 시험을 중단합니다. Safe Mode를 무시해 실행하지 마세요.
3. Project에서 `Assets/ExitLab/Generated/ProjectileDashExit.unity`를 단독으로 여세요. Battlefield는 열 필요가 없습니다. 생성된 파일이 없을 때만 `Tools > Exit Lab > Create isolated scene (once)`를 사용하세요. 생성기는 기존 시험 씬을 덮어쓰지 않습니다.
4. `Tools > Exit Lab > Validate saved scene (no gameplay)`를 실행하세요. Console에서 `EXIT_LAB_EDITMODE_VALIDATION_OK`를 확인합니다. 이는 구성 검사 성공이며 게임플레이 시험 합격이 아닙니다.
5. Project Settings의 Enter Play Mode 설정에서 **Reload Domain과 Reload Scene을 활성화**하거나 해당 최적화 옵션을 끄세요. fixture는 Domain Reload 비활성 시 시작을 거부합니다. Console의 Collapse는 꺼 두세요.
6. Hierarchy의 `ExitLab - Select case here`를 선택합니다. 아래 표에 따라 Test Id/Actor/Clock을 설정하세요. T01/T02/T06/T07은 내부 preset이 위치/속도/관통/목표 형태를 지정하므로 해당 Inspector 필드를 따로 바꾸지 않아도 됩니다.
7. Play에 진입한 다음 Game 뷰의 **선택한 사례 시작**을 누릅니다. 이 순간부터 실제 시험입니다. 이번 준비 작업에서는 이 버튼을 누르지 않았습니다. 시작 후 Inspector 값을 변경하지 마세요.
8. Manual 모드에서는 **Tick 1회 → 물리 1회**를 클릭합니다. 반복 클릭할 때마다 다음 Tick으로 진행됩니다. 오브젝트 파괴는 프레임 끝에 처리되므로 첫 클릭 후 다음 프레임까지 기다리세요. Scene 뷰의 Gizmos를 켜면 현재 body(청록), 좌표(녹색), Entity(노랑) 반경을 볼 수 있습니다. 별도 Sprite는 없습니다.
9. 시험 후속은 다음 Tick에서 실행됩니다. 종료가 보이면 최소 2 Tick을 더 진행해 등록/실행 횟수와 구독/계약 상태가 안정적인지 확인하세요. Production 모드는 자동으로 진행하므로 약 3초 뒤 Play를 종료하고 로그를 살펴보세요.
10. Console에 표시된 `Logs/ExitLab/<시간>_<ID>_<Actor>_<Clock>.jsonl`을 보관합니다. 로그는 한 줄에 한 JSON 레코드이며 저장소 기본 ignore 대상입니다. 첫 오류가 있으면 그 이후 결과는 정상 진행으로 해석하지 마세요.
11. **매 사례 또는 Actor/Clock 변경마다 Play를 종료하고 다시 진입**합니다. 한 실행의 설정을 바꾸어 재사용하지 마세요. Console을 지우기 전에 로그 경로와 오류를 기록하세요.

## 우선 시험: 관찰 예측과 개선 기대를 분리

| 순서 / ID | 선택과 조작 | 현행 예상 (미실행) | 개선 기대 / 판정 주의 |
|---|---|---|---|
| 1 / T01 | Projectile+Manual, 시작 후 1 Tick, 다음 프레임에서 2 Tick 추가. 다음 Play는 Dash+Manual로 반복 | Projectile 목표 x=0.05: 이동 요청 뒤 현재 거리 검사로 종료. Dash 목표=(0,0): zero delta 종료. onHit 등록/실행 0, Expire 후속 1 예상 | 가짜 명중 없이 종료 1회. Projectile의 종료 전 이동 요청을 이미 수정한 것으로 간주하지 않음 |
| 2 / T02 | Projectile+Manual, 10 Tick 정도 궤적 기록. 새 Play에서 Dash로 반복 | speed=60, 목표 x=0.4. 통과 자체로 종료하지 않음. Dash 왕복 가능, Projectile은 캐시/물리 시차 관찰 필요 | 통과 종료/클램프는 아직 개선안. 현행을 개선 기준으로 Fail 처리하거나 버그 수정하지 않음 |
| 3 / T06 | Projectile+Manual, 1 Tick 및 다음 프레임에서 2 Tick 추가 | 목표 Entity x=0.05, 반경 0.2, 시작 내부 조회. 충돌과 거리 조건 동시 충족 시 Hit 등록 1/Expire 등록 2 가능; 다음 Tick 실행 수와 비교 | 개선 기대는 종결 및 종료 후속 1회. `query-preview`에 실제 Collider가 있어야 재현 조건 충족. 미검출이면 재현 실패가 아니라 조건 미충족 |
| 4 / T07 | Dash+Manual, 시작 후 3–5 Tick | 목표 x=3, 중간 적 x=0.7, 관통=true. cast 반경 0.51+적 반경 0.2로 첫 Tick 검출 예상. 같은 적 후속 반복 등록, 위치/최신 요청값 정체 가능 | 개선 기대는 같은 Entity 1회 명중 및 이동 지속. 아직 원본 Add 누락/조기 return은 그대로 있음 |
| 5 / T14 | T14+Projectile, TargetShape=TransformOnly, speed=60, TargetPosition=(0.4,0), Penetrative=false. Manual 10 Tick 기록. 새 Play에서 같은 값으로 Production. 이후 Dash도 각각 반복 | 스케줄 차이에 따른 위치/캐시/종료 시점 차이 가능 | `ready.tick` 대비 상대 Tick 1–10을 비교. 자동 물리 0.02와 수동 1/60 차이도 비교 조건의 일부. 동일 결과를 가정하지 않음 |

자식 Collider 보조 확인: TestId=`TargetShape`, speed=6, TargetPosition=(3,0), TargetShape=`EntityAndCollider`로 한 번, 다음 Play에서 `ChildCollider`로 한 번 실행합니다. 관통 여부를 동일하게 유지하세요. Target은 두 경우 모두 Entity 루트 Transform입니다. 자식 Collider의 GetComponent<Entity>가 실패하는 기존 의미를 자동 보정하지 않습니다. 이는 T08 관련 보조 환경이며 이번 단계에서 실행하지 않았습니다.

## 관측값 해석과 한계

- `before-tick`: Collector 다음, 실제 이동 callback 전 위치·캐시·구독/계약 상태입니다.
- `after-tick-before-physics`: 이번 Tick 이동 처리 직후. MovePosition 반영 완료를 뜻하지 않습니다.
- `after-manual-physics`: 수동 Simulate 반환 후 위치. `fixed-callback-before-physics`, `after-fixed-yield`, `late-update`는 실제 모드의 구간 관측입니다. 후자의 세 구간은 개별 물리 내부 함수의 정밀 프로파일러 trace는 아닙니다.
- `rb`, `transform`, `cache`는 각각 Rigidbody2D 위치, Transform 위치, Projectile `_location`입니다.
- `latestRequested`는 Motor `_pos`의 읽기 값입니다. 이전 요청/초기값이 남을 수 있으므로 **이번 Tick MovePosition 호출 여부나 호출 횟수의 증거로 단독 사용하면 안 됩니다.**
- `query-input-derived`는 기존 계산식으로 산출한 예상 입력이고 `query-preview`는 관측 장치가 별도로 실행한 cast 결과입니다. 원본 Motor 내부의 실제 hit 반환값/분기와 동일한 직접 계측은 아닙니다. Projectile의 벽 우선 정책도 preview 순서로 대체하지 않습니다.
- `enqueue-onHit/onExpire`는 이동 뒤 실제 Collector `_collecting`에서 marker 참조를 센 결과입니다. `execute-onHit/onExpire`는 marker의 실제 Execute입니다. Collector→사전 관측→이동→사후 관측 순서와 유효 명시적 Target을 전제로 합니다. 첫 오류 뒤 누락된 callback/후속 결과는 합격 근거가 아닙니다.
- `projectileSubscribed`는 Ticker delegate에 해당 Projectile TickHandler가 남아 있는지 검사합니다. `subscriptions` 총수에는 시스템/Collector/harness도 포함됩니다. `dashContract`는 실제 Mover 계약 보유 상태입니다.
- `projectile-OnDestroy`는 실제 파괴 callback입니다. Play 종료도 OnDestroy를 부르므로, `harness-destroy` 근처 기록을 자연 종료로 오인하지 마세요.
- Expire/EndDash **함수 진입 자체의 정확한 횟수**, Motor 내부 hit, 모든 MovePosition 호출 스택은 직접 계측하지 않습니다. 필요하면 IDE에서 원본 함수를 수정하지 않고 `ProjectileEntity.Expire`, `Mover.EndDash`, `SkillUtils.ActivateChain`, `ThinMotor.TryMove`, `FixedMotor.TryDash`에 breakpoint/tracepoint를 설정하세요. 중단점은 실시간 스케줄을 교란하므로 T14의 무중단 로그와 별도 실행으로 구분하세요.
- JSONL 파일 쓰기와 별도 cast에도 관측 비용이 있습니다. 이 결과는 가벼운 계측이 붙은 원본 스케줄이며 성능 벤치마크가 아닙니다.

## 범위 밖 문제: 기록만 유지

Projectile 충돌 종료 후 거리 종료 중복, null Target 종료 경로, Dash 명중 HashSet.Add 누락/이동 전 return, 도달·통과 정책, ExpireWhenUnexpected 미사용, Root/Anchor/Player 통합, 원본 콘텐츠 복구는 수정하지 않았습니다. 새 문제를 만나면 첫 오류·Tick·fixture 설정·로그를 먼저 남기고 이 단계에서 게임 코드의 의미를 바꾸지 마세요.

## 실행 결과를 채울 표

| ID / Actor / Clock | 날짜·로그 파일 | 관찰값 | 재현 조건 충족 | 첫 오류 / 후속 작업 |
|---|---|---|---|---|
| T01 / Projectile / Manual | 미실행 | 미측정 | 미확인 | |
| T01 / Dash / Manual | 미실행 | 미측정 | 미확인 | |
| T02 / 양쪽 별도 실행 | 미실행 | 미측정 | 미확인 | |
| T06 / Projectile / Manual | 미실행 | 미측정 | 미확인 | |
| T07 / Dash / Manual | 미실행 | 미측정 | 미확인 | |
| T14 / 각 Actor×Clock | 미실행 | 미측정 | 미확인 | |

현행 관찰값과 개선 요구안은 각각 기록하세요. 개선 정책이 미확정인 항목을 자동 합격/실패로 판정하지 않습니다.
