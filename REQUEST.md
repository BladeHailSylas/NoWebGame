현재 Unity 6 기반 2D 전투/스킬 시스템 프로젝트의 개발을 재개하려고 합니다.

최근 전체 구조 진단에서는 다음과 같은 방향이 확인되었습니다.

* ScriptableObject 기반 스킬/메커니즘 조합 구조는 유지할 가치가 있음
* `INewMechanism.Execute(CastContext)` 중심의 실행 구조는 프로젝트의 핵심 뼈대임
* tick 기반 처리 모델도 유지하는 것이 좋음
* 다만 `StackManager`, `CommandCollector`, 전역 `Instance` 접근, follow-up 실행 경로, `CastContext` 생성 위치 등에 구조적 정리가 필요함
* 특히 `StackManager`가 스택 저장, 수명 관리, periodic 처리, Buff/CC 적용, TriggerableStack 실행까지 너무 많은 책임을 가지고 있을 가능성이 큼
* 과거 임시 구현에서는 TriggerableStack의 threshold 도달 시 `StackManager`가 직접 `CastContext`를 만들고 `Mechanism`을 직접 실행하는 식의 구조가 있었던 것으로 기억함
* 이 방식은 TriggerableStack의 가능성을 검증하는 프로토타입으로는 유효했지만, 장기 구조로는 `StackManager`가 Skill/Mechanism 실행 파이프라인을 너무 많이 알게 되는 문제가 있음

이번에는 **Mechanism 실행 파이프라인 자체의 현황과 개선 방향**을 먼저 진단해 주세요.

## 목표

TriggerableStack을 정식 구조로 고치기 전에, 먼저 현재 프로젝트의 Mechanism 실행 파이프라인이 어떻게 구성되어 있는지 파악하고, 이를 안정적인 공식 실행 경로로 정리하기 위한 보고서를 받고 싶습니다.

특히 다음 질문에 답해 주세요.

---

## 1. 현재 Mechanism 실행 파이프라인 현황 분석

현재 코드 기준으로 다음 흐름이 실제로 어떻게 이어지는지 조사해 주세요.

* 입력 또는 행동 요청이 어디서 시작되는가?
* `Attacker`, `ActBridge`, `SkillCommand`, `CommandCollector`, `SkillRunner`, `TargetResolver`, `CastContext`, `INewMechanism.Execute(CastContext)`는 각각 어떤 역할을 맡고 있는가?
* 일반 스킬 실행 경로는 어떤 순서로 진행되는가?
* delay가 있는 스킬/메커니즘은 어디에서 예약되고 어디에서 실행되는가?
* follow-up skill 또는 후속 Mechanism 실행은 현재 어떤 경로를 타는가?
* `CommandCollector`가 실제로 맡고 있는 책임은 어디까지인가?
* `SkillRunner`가 실제로 맡고 있는 책임은 어디까지인가?
* `CastContext`는 현재 어디에서 생성되며, 어떤 정보들을 담고 있는가?
* Target, Anchor, Direction, Source, Owner, Caster, Tick 정보는 각각 어디에서 결정되는가?

가능하다면 실제 파일과 클래스명을 기준으로 흐름을 정리해 주세요.

---

## 2. 현재 구조의 문제점 진단

다음 관점에서 구조적 문제를 찾아 주세요.

### A. 책임 분리 문제

* `CommandCollector`가 명령 수집, delay scheduling, 실행, 후처리를 과하게 맡고 있지는 않은가?
* `SkillRunner`와 `CommandCollector`의 책임 경계가 명확한가?
* `TargetResolver`가 타겟 계산 외에 `Input.mousePosition`, `Camera.main`, Anchor 생성 등 다른 책임까지 맡고 있지는 않은가?
* `CastContext` 생성 책임이 특정 클래스에 과도하게 흩어져 있지는 않은가?
* Mechanism이 직접 전역 객체나 특정 Manager에 접근하는 사례가 있는가?

### B. 전역 의존성 문제

다음과 같은 접근이 있는지 확인해 주세요.

* `CommandCollector.Instance`
* `Time.Ticker`
* `Time.DelayScheduler`
* `AnchorRegistry.Instance`
* 기타 static/global access

이들이 Mechanism 실행 파이프라인 안에서 어떤 문제를 만들 수 있는지 분석해 주세요.

### C. 실행 경로 중복 문제

다음 실행 경로들이 서로 다른 방식으로 `Mechanism`을 실행하고 있지는 않은지 확인해 주세요.

* 일반 스킬 입력 실행
* delayed command 실행
* follow-up skill 실행
* TriggerableStack threshold 도달 시 실행
* projectile / summon / area / stack trigger에 의한 후속 실행

`INewMechanism.Execute(CastContext)`가 여러 곳에서 직접 호출되고 있다면, 각 호출 위치와 위험도를 정리해 주세요.

### D. TriggerableStack과의 연결 문제

과거 또는 현재 구현에서 `StackManager`가 threshold 도달 시 직접 `CastContext`를 만들고 `Mechanism`을 실행하는 구조가 있다면, 그 위치를 찾아 주세요.

그 구조가 있다면 다음을 분석해 주세요.

* 왜 임시 구현으로는 유효했는가?
* 왜 장기 구조로는 위험한가?
* `StackManager`가 어디까지 알고 있어야 하고, 어디부터는 몰라야 하는가?
* TriggerableStack은 앞으로 직접 실행자가 되어야 하는가, 아니면 실행 요청 생성자가 되어야 하는가?

---

## 3. 권장하는 Mechanism 공식 실행 경로 제안

현재 구조를 크게 갈아엎지 않고, 기존 핵심 구조를 유지하면서 개선하는 방향을 제안해 주세요.

특히 다음 원칙을 기준으로 봐 주세요.

* `INewMechanism.Execute(CastContext)` 중심 구조는 유지한다.
* ScriptableObject 기반 Mechanism 구조는 유지한다.
* tick 기반 delay/command 처리 구조는 유지한다.
* TriggerableStack, follow-up skill, delayed skill도 가능하면 하나의 공식 실행 경로를 타게 만든다.
* `StackManager`는 `CastContext`를 직접 만들거나 `Mechanism`을 직접 실행하지 않도록 한다.
* 외부 시스템은 직접 `Mechanism.Execute`를 호출하지 않고, 공식 실행 요청 객체 또는 Bridge를 통해 실행을 요청하게 한다.
* `CastContext` 생성은 가능한 한 일관된 위치에서 처리한다.

다음 중 어떤 구조가 가장 적합한지도 판단해 주세요.

### 후보 A: 기존 `SkillCommand` 중심으로 통합

TriggerableStack, follow-up skill, delayed mechanism 모두 결국 `SkillCommand` 또는 그 변형을 만들어 `CommandCollector` / `SkillRunner` 경로로 실행한다.

### 후보 B: `MechanismExecutionRequest` 또는 `TriggeredExecutionRequest` 같은 더 일반적인 실행 요청 객체를 둔다

스킬 전체가 아니라 특정 Mechanism 또는 Mechanism 묶음을 실행해야 하는 경우를 위해, `SkillCommand`보다 작은 실행 요청 객체를 만들고, `MechanismRunner` 또는 `SkillRunner`가 이를 처리한다.

### 후보 C: `CommandBridge` / `MechanismBridge` 계층을 둔다

외부 시스템은 `CommandCollector.Instance`나 `SkillRunner`에 직접 접근하지 않고, `Context.Commands` 또는 `Context.Mechanisms` 같은 Bridge를 통해 실행을 요청한다.

각 후보의 장단점과, 현재 프로젝트에 가장 적합한 점진적 개선안을 제안해 주세요.

---

## 4. TriggerableStack을 안전하게 연결하기 위한 권장 구조

TriggerableStack은 현재 또는 과거에 threshold 도달 시 직접 Mechanism을 실행하는 임시 구조였을 가능성이 있습니다.

앞으로는 다음과 같은 방향이 적절한지 검토해 주세요.

* `StackManager`는 threshold 도달을 감지하거나 상태 변화 이벤트를 생성한다.
* `TriggerableActivator` 또는 `StackTriggerDispatcher` 같은 별도 모듈이 TriggerableStack의 조건과 실행 데이터를 해석한다.
* 이 모듈은 직접 `Mechanism.Execute`를 호출하지 않는다.
* 대신 `SkillCommand`, `FollowUpCommand`, `MechanismExecutionRequest`, `TriggeredExecutionRequest` 중 적절한 공식 실행 요청을 생성한다.
* 생성된 요청은 `CommandBridge`, `CommandCollector`, `SkillRunner`, `MechanismRunner` 등 공식 실행 파이프라인으로 전달된다.
* `CastContext`는 Stack 계층이 아니라 실행 파이프라인 쪽에서 생성된다.

이 방향이 적절한지 평가하고, 더 나은 대안이 있다면 제안해 주세요.

---

## 5. 점진적 리팩터링 순서 제안

현재 프로젝트는 작동 중인 프로토타입이므로, 대규모 재설계보다는 점진적 리팩터링이 필요합니다.

다음 순서가 적절한지 평가하고, 더 좋은 순서가 있다면 제안해 주세요.

1. 현재 일반 스킬 실행 파이프라인을 정확히 문서화한다.
2. `INewMechanism.Execute(CastContext)` 직접 호출 위치를 모두 찾는다.
3. `CastContext` 생성 위치와 필요한 입력 정보를 정리한다.
4. `CommandCollector`, `SkillRunner`, `TargetResolver`의 책임 경계를 정리한다.
5. follow-up skill 실행 경로를 공식 파이프라인으로 통합한다.
6. `CommandCollector.Instance` 등 전역 접근을 `CommandBridge` 또는 Runtime Context 경유로 줄인다.
7. TriggerableStack이 직접 실행하지 않고 공식 실행 요청을 만들도록 변경한다.
8. 마지막으로 `StackManager`에서 `CastContext` 생성과 `Mechanism` 직접 실행 코드를 제거한다.

각 단계별로 예상 위험도, 테스트 포인트, 우선순위를 함께 제안해 주세요.

---

## 6. 보고서 형식

보고서는 다음 형식으로 작성해 주세요.

1. 한 줄 결론
2. 현재 Mechanism 실행 파이프라인 요약
3. 관련 주요 클래스/파일 목록
4. 현재 실행 흐름 상세
5. 직접 `INewMechanism.Execute(CastContext)` 호출 위치 목록
6. `CastContext` 생성 위치와 문제점
7. `CommandCollector` / `SkillRunner` / `TargetResolver` 책임 분석
8. follow-up / delayed / trigger 실행 경로 분석
9. TriggerableStack 관련 임시 구현 또는 위험 구현 여부
10. 가장 큰 구조적 위험 5개
11. 권장 개선 방향
12. 추천 아키텍처 후보 비교
13. 점진적 리팩터링 계획
14. 테스트해야 할 시나리오
15. 당장 건드리지 않아도 되는 부분
16. 최종 결론

가능하면 실제 코드 파일과 클래스명을 인용해 주세요.

단, 전체 아키텍처를 갈아엎는 제안은 피하고, 현재 프로젝트의 핵심 구조인 ScriptableObject 기반 Mechanism, tick 기반 처리, `INewMechanism.Execute(CastContext)` 중심 실행 모델은 유지하는 방향으로 제안해 주세요.
