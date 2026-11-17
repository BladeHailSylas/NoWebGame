# Entity 관계 기반 — 1차 구현

이 단계는 관계의 사실 판정과 Mask 대조만 제공합니다. 기존 TargetResolver, Mechanism, Projectile, Dash, InteractionFilter, 소환 초기화 및 SkillData는 아직 새 API를 사용하지 않습니다.

## API 사용

```csharp
// caster는 해당 Skill Chain의 시전자 Entity입니다. Owner로 치환하지 않습니다.
EntityRelation relation = EntityRelationResolver.Resolve(caster, target);
bool allowed = TargetRelationMasks.Allows(
    TargetRelationMask.Self | TargetRelationMask.Ally, relation);

// 자식 Collider/Transform은 가장 가까운 부모 Entity로 정규화할 수 있습니다.
EntityRelation hitRelation = EntityRelationResolver.ResolveFromCollider(caster, hitCollider);
EntityRelation transformRelation = EntityRelationResolver.ResolveFromTransforms(casterTransform, targetTransform);
```

타입은 `Systems.Data`, 판정 API는 `Systems.SubSystems`에 있습니다. 결과 enum을 Mask로 직접 캐스팅하지 마십시오. 관계 결과는 단일 값, Mask는 비트 조합입니다. `TargetRelationMasks.IsValid`로 잘못된 비트를 검출할 수 있으며 `Allows`는 잘못된 Mask 전체를 거부합니다. 허용할 모든 관계를 표현하려면 다섯 비트를 조합해야 하며 `-1`은 유효하지 않습니다.

## 판정 순서

1. Caster/Target이 없거나 Unity에서 파괴되었으면 Unknown입니다.
2. 동일 Entity이면 소속 정보와 관계없이 Self입니다.
3. 서로 다른 Entity는 양쪽 프로필 정보가 정상이어야 합니다. 정보가 부족하면 Unknown입니다.
4. 정상 정보 중 어느 쪽이든 ExplicitNeutral이면 Neutral입니다.
5. 그 외 어느 쪽이든 ExplicitUnrelated이면 Unrelated입니다.
6. 둘 다 LegacyLayer이면 기존 AllyEnemyChecker를 사용합니다. You/Ally는 같은 편, Foe는 다른 편이며 Foe끼리는 Ally입니다.

`Entity.RelationProfile`의 기본값은 `LegacyLayer=0`입니다. LegacyLayer의 미분류 Layer를 Neutral이나 Unrelated로 추측하지 않습니다. ExplicitNeutral/ExplicitUnrelated는 Layer와 별도의 명시적 정보입니다. 미정의 프로필 값은 정보 부족으로 처리합니다. 현재 정상 프로필의 관계는 대칭입니다.

`targetable`, 비활성 상태, 거리, Owner는 관계의 사실을 바꾸지 않습니다. 선택 가능성은 후속 시스템에서 별도 검사해야 합니다. 부모 Entity 정규화는 비활성 객체도 포함하고 중첩 Entity의 가장 가까운 부모를 사용합니다. 프로필/Layer는 매 호출 시 읽으며 캐시하지 않습니다.

기존 `AllyEnemyChecker.IsAlly/IsEnemy(int targetLayer, int sourceLayer)`는 그대로입니다. 따라서 기존 IsAlly는 Self를 구분하지 않고 같은 Layer도 아군으로 처리하지만, 새 Resolve는 Entity 정체성을 먼저 검사합니다.

## 재현 가능한 집중 테스트

Unity Editor 메뉴 `Tools > ExitLab > Check Entity Relations`, 또는 프로젝트의 Unity 버전으로 다음 인자를 사용합니다.

```text
-batchmode -nographics -quit -projectPath <프로젝트 절대 경로> -executeMethod ExitLab.EntityRelationChecks.Run -logFile <로그 절대 경로>
```

테스트는 임시 Preview Scene의 실제 Entity와 Collider를 사용합니다. 기존 Scene이나 에셋을 저장하지 않습니다. 결과는 프로젝트 상위의 `audit-results/target-relation-stage1-tests.txt`에 기록합니다. 실패 시 예외를 발생시키므로 batch 실행 로그의 종료 코드와 결과를 함께 확인하십시오.

검증 범위: 전체 프로필 교차표, Self/동일 진영, 누락·파괴된 객체, 자식·중첩·비활성 Entity, Owner 독립성, 동적 Layer/프로필, 32가지 유효 Mask 조합, Unknown 및 잘못된 비트 거부, 기존 Checker 회귀, 프로필 기본값과 직렬화 왕복.

## 후속 구간의 주의점

- 2차 연결 전까지 게임의 기존 대상 선택·명중 동작은 그대로입니다. `EntityIdentity.IsValid`는 targetable까지 포함하므로 관계 사실 판정과 혼용하지 마십시오.
- 물리 후보 Layer가 자식 Collider를 누락하면 이 API가 복구할 수 없습니다. 물리 검색과 관계 필터를 분리해야 합니다.
- 소환체 소속 복사 및 독립 Skill의 Caster 전달은 2차 연결 시 적용해야 합니다. 이번에는 SummonEntity의 You 고정 초기화를 변경하지 않았습니다.
- InteractionFilter의 환경 피해 호환 분기 및 Owner 정책도 아직 변경하지 않았습니다.
- 3차 마이그레이션 전에는 기존 LayerMask 값을 TargetRelationMask로 읽지 마십시오. 특히 Foe=128은 새 Mask의 Enemy=4와 다릅니다.
- 새 Entity 프로필 필드는 추후 Unity에서 기존 Scene/Prefab을 저장할 때 직렬화될 수 있습니다. 이번 단계에서는 해당 에셋을 재저장하지 않습니다.
