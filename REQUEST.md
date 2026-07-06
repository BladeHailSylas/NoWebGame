이번 작업의 목표는 전역 객체 직접 접근을 한 번에 제거하는 것이 아니라, 우선 `CommandCollector.Instance`에 대한 공식 접근 경로를 만드는 것입니다.

다음 범위 안에서만 작업해 주세요.

## 목표

* `CommandCollector.Instance`를 직접 호출하는 구조를 줄이기 위한 얇은 `CommandBridge` 또는 `CommandPort` 계층을 추가합니다.
* 이번 단계에서는 기존 실행 동작을 바꾸지 않습니다.
* 새 Bridge는 내부적으로 기존 `CommandCollector`에 위임해도 됩니다.
* 우선 `SkillUtils.ActivateChain` 및 follow-up 생성의 중심 경로부터 Bridge를 사용하도록 변경합니다.
* 변경 후에도 일반 스킬 실행, delay 실행, follow-up 실행이 기존과 동일하게 동작해야 합니다.

## 작업 범위

1. `CommandCollector.Instance` 직접 접근 위치를 조사하고 목록화해 주세요.
2. `CommandBridge` 또는 `CommandPort`라는 얇은 접근 계층을 추가해 주세요.
3. Bridge는 우선 기존 `CommandCollector.EnqueueCommand`에 단순 위임하는 형태로 구현해 주세요.
4. `SkillUtils.ActivateChain`이 가능하면 이 Bridge를 통해 command를 enqueue하도록 변경해 주세요.
5. 안전하다고 판단되는 follow-up 호출부만 Bridge 경유로 바꿔 주세요.
6. 변경한 파일 목록과 아직 남아 있는 `CommandCollector.Instance` 직접 접근 위치를 보고해 주세요.

## 제한 사항

* `CommandCollector`의 내부 실행 로직은 변경하지 마세요.
* `DelayScheduler`의 동작은 변경하지 마세요.
* `SkillRunner`의 `INewMechanism.Execute(CastContext)` 호출 방식은 변경하지 마세요.
* `StackManager.ResolveTrigger`의 직접 `CastContext` 생성 및 `Mechanism.Execute` 호출은 이번 작업에서 수정하지 말고, 위치와 위험성만 보고해 주세요.
* `TheWorld`, 직접 물리, Lockstep, Rollback, 새로운 대규모 실행 시스템은 추가하지 마세요.
* `MechanismExecutionRequest` 같은 새 실행 요청 구조는 이번 작업에서 도입하지 마세요.
* 전체 아키텍처 재설계는 하지 마세요.
* 기존 ScriptableObject 기반 Mechanism 구조와 `SkillCommand -> CommandCollector -> SkillRunner -> INewMechanism.Execute` 흐름은 유지해 주세요.

## 기대 결과

이번 작업의 성공 기준은 다음입니다.

* `CommandBridge` 또는 `CommandPort`가 추가되어 있음
* 최소한 `SkillUtils.ActivateChain` 경로가 Bridge를 사용할 수 있음
* 기존 일반 스킬 실행이 깨지지 않음
* 기존 follow-up 실행이 깨지지 않음
* 남은 `CommandCollector.Instance` 직접 접근 위치가 보고서로 정리됨
* `StackManager.ResolveTrigger`는 다음 단계 과제로 남겨짐

작업 후에는 다음 형식으로 보고해 주세요.

1. 추가한 클래스/파일
2. 변경한 호출부
3. 남아 있는 `CommandCollector.Instance` 직접 접근 위치
4. 빌드 또는 컴파일 결과
5. 수동 테스트가 필요한 시나리오
6. 다음 단계에서 다뤄야 할 위험 지점
