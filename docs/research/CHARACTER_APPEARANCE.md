# 캐릭터 착용 외형 식별 조사

조사일: 2026-09-14

## 요약

붕괴: 스타레일 공개 프로필에는 캐릭터가 착용한 외형을 구분하는 `dressedSkinId`가 있다. MiHoMo parsed v2 응답은 이 값을 보존하지 않으므로, 유물·스탯용 parsed 응답과 원본 응답을 함께 읽어야 현재 외형을 반영할 수 있다.

## 확인된 사실

1. Enka의 실제 HSR 원본 응답은 `detailInfo.avatarDetailList[*].dressedSkinId`를 제공하며 기본 외형이면 이 속성을 생략한다. 공개 UID `800333171`에서 카스토리스 `avatarId=1407`, `dressedSkinId=1140701`을 확인했다. 지원 캐릭터는 `assistAvatarList`에 포함될 수 있다. [Enka 실제 원본 응답](https://enka.network/api/hsr/uid/800333171)
2. 같은 공개 프로필 페이지가 카스토리스의 외형 원형 아이콘을 `AvatarRoundIcon/AvatarSkin/1140701.png`로 사용한다. [Enka 실제 프로필 페이지](https://enka.network/hsr/800333171/)
3. 전신 외형 이미지 `AvatarDrawCard/AvatarSkin/1140701.png`는 기본 캐릭터 `AvatarDrawCard/1407.png`와 별개의 이미지다. [확인한 외형 전신 이미지](https://enka.network/ui/hsr/SpriteOutput/AvatarDrawCard/AvatarSkin/1140701.png)
4. MiHoMo의 `enhanced`는 외형 신호가 아니다. 공개 모델은 원본 `enhancedId`를 별도 필드로 정의하고, parsed 변환은 이를 Boolean으로 바꾼다. 반면 parsed 캐릭터 모델에는 `dressedSkinId`가 없다. [MiHoMo 모델 51~61행](https://github.com/Mar-7th/mihomo.py/blob/master/mihomo/model.py#L51-L61), [MiHoMo 파서 126~168행](https://github.com/Mar-7th/mihomo.py/blob/master/mihomo/api.py#L126-L168), [원본 API 문서](https://march7th.xyz/en/api/raw.html), [Parsed API 문서](https://march7th.xyz/en/api/parsed.html)
5. StarRailRes는 캐릭터 ID별 기본 portrait를 제공하지만 현재 외형별 portrait와 외형 ID 매핑은 제공하지 않는다. [StarRailRes 캐릭터 portrait 목록](https://github.com/Mar-7th/StarRailRes/tree/master/image/character_portrait), [캐릭터 메타데이터](https://raw.githubusercontent.com/Mar-7th/StarRailRes/master/index_new/en/characters.json)
6. 모든 외형이 `{dressedSkinId}.png` 규칙으로 직접 열리는 것은 아니다. 조사한 일반 외형들은 규칙과 일치했지만 `1141501`은 해당 후보 경로가 없고 기본 캐릭터 이미지로 매핑되는 예외였다. 따라서 이미지 로드 실패 폴백이 필요하다.

## 구현 판단

- MiHoMo parsed 응답은 기존 유물·스탯 파싱의 원본으로 유지한다.
- 같은 UID의 MiHoMo 원본 응답에서 `avatarDetailList`와 `assistAvatarList`를 모두 확인해 `캐릭터 ID → dressedSkinId`만 추출한다.
- ID는 JSON 숫자와 문자열을 모두 허용하되 ASCII 숫자로만 구성된 양수인지 검증한 뒤 URL에 넣는다.
- 외형 확인 완료 표시와 추출한 매핑을 parsed JSON에 병합하고 같은 Redis TTL로 캐시해 서로 다른 시점의 응답이 섞이지 않게 한다.
- 외형 ID가 있으면 Enka 전신 외형 이미지를 먼저 보여주고, 파일이 없거나 네트워크 로드가 실패하면 기존 StarRailRes 기본 portrait로 전환한다.
- 원본 외형 조회는 보조 기능이다. 실패 사실은 경고와 로그로 알리되 유물 가져오기 자체는 성공시킨다.

## 검증 방법

```powershell
dotnet test tests/ArtifactGrade.Api.Tests/ArtifactGrade.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~MihomoRelic" --nologo
```

위 명령은 MiHoMo 관련 테스트만 빠르게 실행한다. 외형 ID가 전신 이미지 URL에 반영되는지, Redis 캐시 뒤에도 같은 결과인지, 외형 API가 실패해도 기본 이미지와 유물 데이터가 유지되는지를 확인한다.

전체 검증은 다음 명령으로 실행한다.

```powershell
dotnet test ArtifactGrade.slnx -c Release --no-restore --disable-build-servers
dotnet build ArtifactGrade.slnx -c Release --no-restore --nologo
dotnet format ArtifactGrade.slnx --no-restore --verify-no-changes
```

실제 화면에서는 `start-dev.cmd`를 더블클릭한 뒤 외형을 착용한 캐릭터가 공개된 UID를 불러온다. 캐릭터 탭을 선택하고 왼쪽 `<img>`의 주소와 로드 크기를 확인해 외형 경로가 선택됐는지 검증한다.

## 한계

- 공개 프로필에 전시한 캐릭터만 조회할 수 있다.
- Enka 이미지 경로 규칙이 바뀌거나 외형 파일이 제공되지 않으면 기본 portrait가 표시된다.
- HSR Scanner v4에는 `dressedSkinId`가 없으므로 Scanner JSON 가져오기는 캐릭터 ID 기반 기본 portrait를 사용한다.
