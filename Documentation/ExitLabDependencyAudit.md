# Battlefield 의존성 읽기 전용 점검

Unity 6000.3.10f1 AssetDatabase GUID 조회. 원본 씬 로드/저장 및 게임플레이 없음.

아래는 AssetDatabase에서 경로를 찾지 못한 외부 GUID입니다. fileID=0/null 필드는 이 목록에 포함되지 않습니다.

- 줄 500, GUID `ba36a303aff0ad643b7c60be4ed6161b`: `spec: {fileID: 11400000, guid: ba36a303aff0ad643b7c60be4ed6161b, type: 2}`
- 줄 731, GUID `5a03e60ada405be4caae6ab74fc2e144`: `m_Sprite: {fileID: -6797698898886190169, guid: 5a03e60ada405be4caae6ab74fc2e144, type: 3}`
- 줄 815, GUID `8f586378b4e144a9851e7b34d9b748ee`: `m_fontAsset: {fileID: 11400000, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}`
- 줄 1694, GUID `8bdee2da6ae1adf49ade3cb50d81ab93`: `- {fileID: 11400000, guid: 8bdee2da6ae1adf49ade3cb50d81ab93, type: 2}`
- 줄 1695, GUID `ac1d2dd1c4229e743baf29daa1b7138a`: `- {fileID: 11400000, guid: ac1d2dd1c4229e743baf29daa1b7138a, type: 2}`
- 줄 1696, GUID `7bba607f57a8d974793180efb63d55b2`: `- {fileID: 11400000, guid: 7bba607f57a8d974793180efb63d55b2, type: 2}`
- 줄 1697, GUID `9cd5746326c9a48488a8bb9a52bbd770`: `- {fileID: 11400000, guid: 9cd5746326c9a48488a8bb9a52bbd770, type: 2}`
- 줄 1698, GUID `e987529f010a6df48b34f45c9c8b70eb`: `- {fileID: 11400000, guid: e987529f010a6df48b34f45c9c8b70eb, type: 2}`
- 줄 1699, GUID `3b5ffe37414f45d429b0940d397c5689`: `- {fileID: 11400000, guid: 3b5ffe37414f45d429b0940d397c5689, type: 2}`
- 줄 1949, GUID `f4afef2b6294afb448b5084b95567066`: `spec: {fileID: 11400000, guid: f4afef2b6294afb448b5084b95567066, type: 2}`

## 범위 제한

별도 fixture는 이 원본 콘텐츠를 사용하지 않습니다. CharacterSpec/SkillData/Stack 원본 복구, Player/입력/Root/Anchor 통합 및 Player 빌드는 미검증입니다.
