# v0.2 릴리스 전 DR 안정화 보고서

작성일: 2026-11-25. 상태: **DR 수정·자동 회귀 완료 / 사용자 최종 플레이 결과 대기**.

## 원인과 수정

`StatsContainer.TotalDamageReduction()`은 일반 DR 목록과 Stack 소유 기여를 곱하여 잔여 피해 배율을 계산한 뒤, `Math.Max(0.15, total)`을 int로 변환했습니다. 정상적인 0.8이나 하한 0.15가 0으로 잘려 Normal 피해가 사라지는 결함이었습니다.

반환 시 `(int)`만 제거하여 double 배율을 유지했습니다. 각 기여의 곱셈 합성과 잔여 배율 하한 0.15는 그대로입니다. 최종 정수 피해 변환은 기존 ApplyDamage 위치에 남겼습니다. 방어력·AP·증폭·Shield·Fixed 피해 우회 정책은 변경하지 않았습니다. 매우 작은 최종 피해가 기존 정수 변환으로 0이 되는 현상까지 새로운 최소 피해 정책으로 바꾸지 않았습니다.

변경한 기존 코드 파일은 세 개입니다.

- `Assets/Scripts/Combat/Stats/StatsContainer.cs`: 배율 반환과 설명 주석
- `Assets/ExitLab/Editor/StackBuffChecks.cs`: DR 계산·실제 HP·소유권·반복 수명 회귀 65개 추가
- `Assets/ExitLab/Editor/StackBaselineChecks.cs`: DR 결함 형태를 유지해야 통과하던 검사를 정상 배율 0.8 검사로 전환

## 재현 및 자동 검증

새 회귀를 수정 전 런타임에 먼저 적용했습니다. `single DR preserves fractional remaining factor`에서 실패했고 Unity 종료 코드는 1이었습니다. 수정 후 동일 테스트와 기존 시스템 회귀는 모두 통과했습니다.

| 검사 | 결과 |
|---|---|
| Buff/DR | 143 PASS, 기존 78 + 추가 65 |
| Stack 전이·Trigger·수명 | 101 + 46 + 23 PASS |
| Stack 기준선 | 24개 모두 정상 PASS, KNOWN_DEFECT 출력 0 |
| 기존 EditMode 통합 | 874 PASS |
| CastLifecycle PlayMode | 68 PASS |
| Travel | 96 PASS |
| 입력·AI·Switch·Trigger | 30 PASS |
| namespace/Editor 도구 | 229 PASS |

DR 전용 검사는 없음→1, 단일 20%→0.8, 독립 20%·25%→0.6, 85% 및 초과 합성→0.15, 100% 기여 해제 후 복귀를 포함합니다. 방어 0·공격 100·damageValue 100 조건에서 실제 HP 감소 100/80/60/15를 확인하고 Fixed 피해 100도 유지했습니다.

Stack 검사는 동일 Key의 +1/+1과 +2 동등성, 다른 Key의 곱셈 합성, 일반 DR과 Stack DR의 동시 합성, 상한 재적용, 중첩 감소, 정확한 소유 기여 제거, 자연 만료, 20주기 적용·만료 후 배율 1 및 소유 기여 0 복귀를 포함합니다. StatsBridge.DR의 공개 반환 경로도 확인했습니다.

Unity 6000.3.10f1 격리 사본에서 실행했으며 최종 stack/edit/cast-play/travel/input 종료 코드는 모두 0입니다. 원본 C# 180개와 검증 사본의 해시가 일치했습니다. 작업 시작 시점 대비 원본 Assets/Packages/ProjectSettings에서 위 세 코드 파일 외 기존 파일의 해시는 모두 동일합니다. PlayGroundAssets·기존 애셋·meta·설정을 수정하지 않았습니다.

기존 환경 초기화 로그와 의도적으로 주입하는 예외는 개별 회귀 실패와 구분했습니다. 수정 전 재현 로그도 보존했습니다. 조사·검사 범위에서 별도의 새 릴리스 차단 결함은 발견하지 못했으며, 이것이 실제 최종 플레이 완료를 의미하지는 않습니다.

## 플레이 가이드와 후속 상태

[최종 통합 플레이 테스트 가이드](V02FinalPlaytestGuide.md)에 T01~T13의 사전 조건·절차·기대값·실패 기준을 작성했습니다. 기존 StackValidation 세트를 우선 사용하며 유료 지연 Cast, DR 정량 자기 피해, 복수 DR/하한 및 Switch 변형 등 추가 준비가 필요한 임시 구성도 명시했습니다. 이번에는 추가 애셋을 생성하지 않았습니다.

과거 StackValidation README 및 단계별 보고서의 “DR 미해결” 문구는 당시 상태입니다. 현재 DR 판정은 이 보고서와 최종 가이드의 T05를 기준으로 합니다. Enemy 임시 Trigger가 Fixed 피해를 사용하는 기존 동작은 유지했으므로 해당 예시를 DR 시험으로 사용하면 안 됩니다.

원본 Unity는 실행하지 않았습니다. **사용자 플레이 전 이번 코드 변경의 import·컴파일이 필요합니다.** 실제 플레이는 사용자가 수행하며 그 결과를 검토하기 전 릴리스 가능 판정을 확정하지 않습니다. Version Bump·원격 push·추가 구조 개선은 하지 않았습니다. 선택적으로 허용된 커밋도 이번 작업에서는 수행하지 않았습니다.

## 근거 위치

저장소 상위 `audit-results/v02-dr-stabilization/`에 수정 전 코드 백업, `source-baseline.json`, `changed-files.json`, `source-verification.txt`, `before-fix-results.txt`, `before-fix.log`, 수정 후 다섯 실행 로그·종료 코드 및 `regression-results/`를 보관했습니다. 수정 전 실패 1과 수정 후 성공 0을 구분하십시오.
