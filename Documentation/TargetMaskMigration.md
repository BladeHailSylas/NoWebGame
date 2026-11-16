# 상대적 Target Mask: 에셋 마이그레이션

## 저장 형식과 Inspector

각 `NewMechanism`은 `targetMaskVersion`과 `targetRelations`를 저장합니다. 버전 0은 기존 LayerMask 실행 경로, 버전 1은 Skill Chain의 Caster를 기준으로 한 관계 경로입니다. 버전 1에서는 기존 `mask` 및 Melee의 `enemyMask`가 관계 선택에 사용되지 않습니다. 해당 필드는 구버전 호환과 검토를 위해 보존합니다.

Inspector의 관계 선택은 Self / Ally / Enemy / Neutral / Unrelated입니다. None은 아무 Entity 관계도 허용하지 않습니다. Unknown은 선택할 수 없으며 Everything은 유효한 다섯 비트(31)로 저장합니다. 잘못된 기존 비트는 오류로 표시하며 자동으로 바꾸지 않습니다. Inspector에서 새 Mechanism 타입을 선택하면 버전 1 / None으로 생성되므로 의도한 관계를 직접 선택해야 합니다. 기존 객체를 로드하는 것만으로는 버전이 바뀌지 않습니다.

물리 후보 검색은 버전 1에서 All Layers를 사용하되, 관계 필터가 Entity 허용 여부를 판정합니다. 벽과 이동 충돌 설정은 별도 책임입니다. Ally는 Self를 포함하지 않습니다. `requireRetarget` 자식은 자신의 관계 설정으로 재탐색합니다.

## 변환 도구

1. 변경 사항을 저장하고 `Tools > Target Masks > Preview Migration`을 실행합니다.
2. Console에 표시된 보고서에서 모든 변환과 REVIEW 항목을 확인합니다.
3. `Apply Reviewed Migration`은 새로 스캔한 계획을 보여주며, REVIEW가 하나라도 있으면 전체 적용을 거부합니다. 적용 전 백업 경로는 프로젝트의 상위 폴더에 생성됩니다.

자동 변환은 승인된 `128(Foe) → Enemy(4)`, `0 → None(0)`만 수행합니다. 물리 레이어와 관계 Flags의 숫자는 다른 의미이므로 단순 캐스팅하지 않습니다. Melee의 두 기존 Mask가 다르거나, 혼합 Mask·Everything·미지원 버전·알 수 없는 직렬화 형식이 있으면 수동 검토가 필요합니다. 버전 1 객체는 다시 변환하지 않습니다.

이 도구는 현재 프로젝트의 Unity YAML 관리 참조 형식(version 2)을 대상으로 합니다. 기존 텍스트에 새 필드만 삽입하여 관리 참조 ID, GUID, 콜백 그래프와 기존 수치를 보존합니다. dry-run 이후 파일 변경, 로드된 에셋의 미저장 변경, Assets 외부 경로를 거부합니다. 프로젝트 전체 SaveAssets는 호출하지 않습니다. 비텍스트 에셋은 자동 변환하지 않습니다.

## 이번 변환 범위

| 에셋 | Mechanism 수 | 관계 |
| --- | ---: | --- |
| ObjectInstances/SkillData/SkillData.asset | 2 | Enemy 2 |
| ObjectInstances/SkillData/SkillData 1.asset | 2 | Enemy 2 |
| ObjectInstances/SkillData/SkillData 2.asset | 7 | Enemy 6, None 1 |
| ExitLab/RegressionSamples/NestedExitContract.asset | 2 | None 2 |

`SkillData 3.asset`의 빈 관리 참조는 유지합니다. SkillData 2의 OnInterrupted Area는 기존 Mask가 0이므로 None을 유지합니다. 기존 효과 수치나 종료 분기 설정은 바꾸지 않습니다.

## 검증 방법

자동 검증은 별도 Unity 검증 사본에서 실행합니다. `TargetMaskMigrationChecks.ApplyAndVerify`와 `MigratedSkillPlayModeLauncher.Run`은 배치 모드이며 프로젝트 루트에 `TargetMaskMigrationTestCopy.marker`가 있는 사본에서만 실행할 수 있습니다. 전자는 마이그레이션 전 사본을 필요로 합니다. 후자는 변환된 실제 스킬 에셋을 복제하여 양 진영의 근접/Dash/투사체 실행을 검증합니다. `TargetMaskMigrationChecks.VerifyMigrated`는 재시작 후 저장된 정책과 멱등성을 검사합니다.

커밋 전 실제 Editor에서 다음을 확인하십시오.

- Inspector에 Target Policy가 Relative(v1)로 표시되고, 위 표의 관계가 유지되는지 확인합니다. 새 Mechanism의 기본 None을 의도한 관계로 변경한 후 저장·재로드합니다.
- Battlefield에서 Player와 Enemy의 근접/Dash/투사체 공격을 각각 실행합니다. 상대 피해 및 자기 자신/같은 편 제외를 확인합니다. 피해 허용은 피격자의 InteractionFilter 설정도 만족해야 합니다.
- Enemy 투사체가 Player에게 명중하면 동일 trace의 TargetHit → OnCompleted가 한 번 발생하고, OnInterrupted가 발생하지 않는지 확인합니다. 벽 충돌·시간 만료는 각각 기존 설정에 따른 별도 종료입니다.
- 자식 효과와 종료 위치가 유지되며, None인 종료 Area가 Entity 피해를 만들지 않는지 확인합니다.

배치 검증은 실제 Battlefield 씬의 배치·입력·화면 표시와 Inspector의 시각적 조작을 대신하지 않습니다.
