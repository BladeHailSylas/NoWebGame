# 상대적 Target Mask — 2차 실행 경로

## 이행 계약

기존 SkillData/Prefab/Scene은 변환하거나 저장하지 않습니다. 각 `NewMechanism`의 `targetMaskVersion`이 0이면 기존 LayerMask 실행 경로, 1이면 `targetRelations`를 사용하는 새 경로입니다. 버전 필드가 없는 기존 에셋은 0으로 읽힙니다. 새 Mask의 None(0)은 명시적인 무허용 설정이며 미변환 표시가 아닙니다. 버전이 잘못되거나 정의되지 않은 관계 비트가 있으면 Entity를 허용하지 않습니다.

검증용 메모리 객체에서는 `ConfigureRelativeTargets(TargetRelationMask.Enemy)`로 새 경로를 명시합니다. 이 API는 에셋을 저장하거나 기존 `mask`를 변환하지 않습니다. 루트와 각 자식 Mechanism을 각각 설정해야 합니다. 부모의 버전/관계 설정을 모든 자식에 자동 적용하지 않습니다.

이행 기간에는 `INewMechanism.Mask`, `SkillCommand.Mask`, `TargetRequest.TargetMask`를 Legacy 물리 마스크로 유지합니다. 새 코드의 `TargetPolicy`/`IRelativeTargeting`은 관계 Flags만 전달합니다. 기존 INewMechanism 구현체가 새 인터페이스를 제공하지 않으면 Legacy입니다. 일반 이동, Teleport 장애물, 조준/지형 LayerMask는 변경하지 않았습니다.

## 새 경로의 처리

- 커서의 모든 Collider 후보를 찾고 가장 가까운 부모 Entity로 정규화합니다. 관계·targetable·활성·사거리를 검사한 뒤 적합 후보를 고릅니다. 후보가 겹쳐도 첫 부적합 Collider 때문에 탐색을 끝내지 않습니다. 같은 거리에서는 Entity 인스턴스 번호로 순서를 정합니다.
- 물리 검색은 AllLayers로 넓게 수집하고 Caster 기준 관계 필터를 적용합니다. 전역 Physics2D Trigger 설정은 유지합니다. 관계 Flags를 Physics2D LayerMask 인자로 사용하지 않습니다.
- requireRetarget 자식은 자신의 TargetPolicy로 재탐색합니다. 상속 Target은 다시 탐색하지 않으며 Damage/Stack의 실제 적용 시 자식 관계 Mask를 검사합니다. Detect 관계 실패는 onNotFound로 전달합니다.
- TowardsSelf 선택은 Self 비트가 필요합니다. None이어도 좌표 이동/연출 및 Dummy/Switch 같은 제어 흐름은 가능합니다. Entity에 직접 적용할 효과와 물리 명중은 발생하지 않습니다.
- Area는 한 활성 주기당 Entity 한 번, Ray/Melee는 한 실행당 한 번 명중합니다. Hitscan은 벽 이전의 첫 허용 Entity를 선택합니다. 거부 Entity는 뒤의 적을 가리지 않습니다.
- Projectile/Dash는 이동 중 Cast와 도착 Overlap 모두 같은 관계 판정을 사용합니다. 벽이 같은 거리에 있으면 벽이 우선합니다. 이동체 본체는 제외하지만 별도 투사체의 Caster는 Self 설정에 따라 명중 가능합니다.
- 거부된 지정 Entity 위치에 도달해도 관계 거부 자체로 명중/종료를 만들지 않습니다. 수명 종료·취소·대상 소실 등 기존 계약으로 종료합니다. 허용된 Entity인데 실제 Collider 접촉이 없으면 TargetNotHit입니다.
- 일반 이동/Teleport의 차단 규칙은 유지합니다. 물리 충돌 매트릭스나 Collider 설정에 의한 실제 이동 제한은 관계 Mask와 별도입니다.
- 기존 `ExitCause.EnemyHit` 숫자와 이름은 호환성을 위해 유지합니다. 새 경로에서는 지정 대상 외의 **허용 Entity 명중**을 뜻하므로 Ally/Neutral도 이 사유가 될 수 있습니다. 지정 대상은 TargetHit입니다.

## 피해·소환

새 DamageMechanism은 DamageData의 `UseRelationPolicy`를 명시합니다. InteractionFilter는 해당 피해에 공용 관계 API를 사용하고, Owner 수신 허용은 별도 우선 조건으로 유지합니다. Self는 허용, Ally/Unrelated는 차단, Enemy/Neutral/Owner는 기존 허용 변수를 요구합니다. 정보가 부족한 Entity는 Unknown으로 거부하고, 출처 없음/비Entity 환경 피해만 AllowNeutralDamage 호환 분기로 처리합니다. 기존 피해 데이터의 기본값은 Legacy이므로 미변환 콘텐츠는 원래 정책을 유지합니다.

새 SummonMechanism은 생성 시 소환자의 Layer와 관계 프로필을 복사합니다. Owner는 원래 소환자이며 Entity 정체성은 별개입니다. 이후 소환자의 소속 변경을 추종하지 않습니다. 기존 SummonMechanism의 You 초기화는 이행 기간에 유지합니다. 독립 기술의 Caster는 그 기술을 실행한 소환체이며 Owner로 치환하지 않습니다.

## 로그와 검증

AttackTrace의 `targetVersion`, `relations`, 실제 `queryLayers`를 함께 확인하세요. 기존 Foe-only 투사체와 새 관계 투사체의 차이를 명확히 구분할 수 있습니다. 자식이 미변환 상태이면 부모가 새 경로여도 자식의 동작은 Legacy입니다.

- Edit Mode 메뉴: `Tools > ExitLab > Check Relative Target Pipeline`
- batch 메서드: `ExitLab.RelativeTargetPipelineChecks.Run`
- Play Mode 전용 batch: `ExitLab.RelativeTargetPlayModeLauncher.Run` (`-quit` 없이 실행)

검사는 저장된 Scene 옆의 임시 Scene 또는 별도 검증 사본에서 수행하며, 원본 에셋을 저장하지 않습니다. Play Mode batch는 분리된 검증 사본에서만 실행합니다. 결과 파일은 프로젝트 상위의 `target-stage2-tests.txt`, `target-stage2-playmode-tests.txt`입니다.

3차 마이그레이션 전에는 Battlefield의 기존 Enemy 공격이 자동으로 바뀌지 않습니다. 128→Enemy는 승인된 콘텐츠 의도 변환이며 비트 캐스팅이 아닙니다. 기존 0→None을 지켜야 하므로, 부모만 명중하더라도 None인 자식 효과는 실행되지 않을 수 있습니다.
