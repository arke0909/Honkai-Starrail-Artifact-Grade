# 캐릭터별 유물 유효 옵션 조사

- 조사일: 2026-09-14
- 대상 기능: 자동으로 불러온 유물을 장착 캐릭터에 맞춰 평가
- 결론: 범용 `치명타 딜러` 프로필을 사용하지 않고 캐릭터 ID별 주옵션·12개 부옵션 가중치와 정규화 값을 적용한다.

## 문제 정의

기존 화면은 사용자가 치명타·공격력·HP·방어력·격파·서포터 중 하나를 직접 골랐다. 이 방식은 카스토리스처럼 치명타를 사용하지만 공격력이 아니라 HP로 피해가 증가하는 캐릭터를 정확히 설명하지 못한다. 같은 치명타 딜러라도 공격력%, HP%, 방어력%의 가치가 달라서 하나의 범용 프로필로 묶으면 유물 점수의 의미가 흐려진다.

## 확인된 사실

MiHoMo parsed 응답에는 캐릭터 ID, 운명의 길, 속성, 현재 능력치와 스킬 설명이 있지만 정규화된 유물 부옵션 가중치는 없다. 따라서 응답의 캐릭터 이름이나 스킬 설명 문자열만 분석해 성장 방식을 추측하는 것은 언어와 신규 기믹 변화에 취약하다.

StarRailScore의 `score.json`은 캐릭터 ID마다 다음 값을 제공한다.

- `main`: 부위별 주옵션 가중치
- `weight`: HP·공격력·방어력의 고정값/퍼센트, 속도, 치명타 확률/피해, 효과 명중/저항, 격파 특수효과까지 12개 부옵션 가중치
- `max`: 해당 기준의 정규화 값

카스토리스 ID `1407`의 부옵션 가중치는 HP% `1.0`, 치명타 확률 `1.0`, 치명타 피해 `1.0`, 고정 HP `0.3`, 속도 `0.1`, 공격력% `0`이다. StarRailRes의 카스토리스 전투 스킬 데이터도 피해가 최대 HP를 기준으로 계산됨을 명시한다. 따라서 카스토리스는 공격력 기반 캐릭터와 분리해 `HP 기반 · 치명타 중심`으로 표시하는 것이 맞다. 이 문구는 역할을 단정하지 않고 실제 높은 가중치만 요약한다.

비교 사례는 다음과 같다.

| 캐릭터 | 대표 가중치 | 해석 |
|---|---|---|
| 단항 `1002` | 공격력%·치확·치피 중심 | 공격력 기반 치명타 딜러 |
| 어벤츄린 `1304` | 방어력%·치확·치피 중심 | 방어력 기반 치명타 딜러 |
| 반디 `1310` | 격파 1.0, 공격력% 0.6, 치명타 0.1 | 공격력·격파 기반 딜러 |
| 나타샤 `1105` | HP·속도·효과 저항 중심, 치명타 0 | HP 기반 생존/지원 |

StarRailScore README는 부옵션 원점수를 기본 롤 횟수와 품질 상승 단계 수의 0.1배로 계산한다고 설명한다. 같은 고정 커밋의 `scripts/generate.py`는 최고 품질 롤을 `1.2`로 놓고 여섯 부위의 이론 최고 원점수 평균을 `max`로 생성한다. 따라서 불러온 수치를 최고 증가량으로 나눠 최고 롤을 `1.0`으로 만드는 계산은 `max`와 단위가 맞지 않는다.

Fribbels의 Stat Score 문서는 캐릭터별 가중치를 0부터 1 사이로 두고, 5성 유물 한 번의 증가량으로 환산해 비교하는 방식을 설명한다. 동시에 단순 가중치 점수는 파티 버프, 패시브, 목표 속도, 초과 치명타처럼 전투에서 비선형적으로 작동하는 조건을 모두 반영하지 못한다고 명시한다.

## 구현 판단

1. 화면의 수동 프로필 선택과 직접 입력 영역을 제거한다.
2. 선택한 캐릭터 ID로 StarRailScore의 부위별 `main`, `weight` 12개와 `max`를 서버에서 조회한다.
3. 서버는 원본 JSON을 `RelicMainStat`과 12개 `RelicStat`에 매핑하고 0~1 범위, 누락 필드와 양수 `max`를 검증한다.
4. 재현 가능한 결과를 위해 원본 주소를 조사 시점 커밋 `fb8268bc6345c52501bd4ec23f8df89b26497e0a`에 고정한다. 최초 조회 결과는 API 프로세스 메모리에 재사용하고 원본 JSON을 Redis에 30일 캐시해 일시적인 원격 장애에 대비한다.
5. 가중치 `0.8~1.0`은 `핵심`, `0.4~0.7`은 `유효`, `0보다 크고 0.4 미만`은 `보조`, `0`은 `비유효`로 표시한다.
6. 화면 상단에는 가중치에서 파생한 `HP 기반 · 치명타 중심`, `공격력 기반 · 치명타 중심`, `격파 · 속도 중심` 같은 중립적인 요약만 보여준다. 실제 점수 계산의 원본은 개별 가중치다.
7. 각 유물 카드에는 주옵션과 실제 부옵션 수치, 유효도, 가중치와 점수 기여도를 함께 표시한다. 5성 부옵션 수치는 원본 게임 데이터의 기본값과 단계값으로 롤 횟수·품질을 복원하고, 총점은 원본의 구조처럼 주옵션과 부옵션을 50%씩 합산한다.
8. 원본에 없는 신규 캐릭터나 미장착 유물은 범용 치명타 점수로 대체하지 않고 `캐릭터 전용 평가 없음`을 표시한다.

StarRailScore 저장소에는 확인 시점에 명시적인 라이선스 파일이 보이지 않았다. 따라서 `score.json` 전체를 저장소에 복제하지 않고 실행 중 고정 커밋 URL에서 조회하도록 구성했다. 원격 조회가 실패하면 Redis의 최근 정상 원본을 사용하고, 둘 다 사용할 수 없거나 JSON 형식이 맞지 않으면 자동 점수 실패 이유를 화면에 표시한다. 가져온 유물 원본 정보 자체는 계속 볼 수 있다.

## 필드 매핑

| StarRailScore | C# `RelicStat` |
|---|---|
| `HPDelta` | `FlatHp` |
| `AttackDelta` | `FlatAttack` |
| `DefenceDelta` | `FlatDefense` |
| `HPAddedRatio` | `HpPercent` |
| `AttackAddedRatio` | `AttackPercent` |
| `DefenceAddedRatio` | `DefensePercent` |
| `SpeedDelta` | `Speed` |
| `CriticalChanceBase` | `CritRate` |
| `CriticalDamageBase` | `CritDamage` |
| `StatusProbabilityBase` | `EffectHitRate` |
| `StatusResistanceBase` | `EffectResistance` |
| `BreakDamageAddedRatioBase` | `BreakEffect` |

주옵션은 같은 HP·공격력·방어력·치명타·효과 명중·속도·격파 필드 외에도 치유량, 에너지 회복 효율과 7개 속성 피해 필드를 `RelicMainStat`에 매핑한다. 각 부위에 원본이 제시하지 않은 주옵션 조합은 가중치 `0`으로 평가한다.

## 검증 기록

- 테스트 우선 작성: 카스토리스가 `HP 기반 · 치명타 중심`으로 분류되는지, 주옵션과 12개 부옵션을 모두 보존하는지, 공격력% 가중치 0이 점수에 반영되는지 확인했다.
- 파서 테스트: 주옵션과 12개 부옵션 필드를 C# enum으로 바꾸고 한 필드라도 누락되면 캐릭터 ID가 포함된 `JsonException`을 내는지 확인했다. 원본은 프로세스당 한 번만 조회하며 원격 503 때 Redis 캐시로 복구하는지도 검증했다.
- 실제 API: 카스토리스 `max=9.24`를 적용한 +15 머리 샘플에서 고정 HP 주옵션은 50점, HP%·치확·치피 최고 품질 롤은 각각 6.5점, 공격력%는 0점으로 계산되어 총 69.5점(SS)이 나왔다.
- 실제 브라우저: UID `800333171`의 카스토리스 탭에서 HP%·치확·치피 `1.0`, 공격력% `0`, 속도 `0.1`과 유물 6개의 자동 점수를 확인했다. 카드마다 주옵션 적합도·가중치·기여도가 함께 표시됐다.
- 비교 확인: 같은 UID의 아케론 탭은 `공격력 기반 · 치명타 중심`으로 바뀌고 공격력%·치확·치피 `1.0`, 속도 `0.8`이 표시됐다.
- UI 확인: `유물 정보 입력` 제목과 수동 입력 영역이 0개인지 확인했고 브라우저 콘솔 오류는 없었다.

## 출처

- [MiHoMo Character 모델 — 구조화된 캐릭터 필드](https://github.com/MetaCubeX/mihomo/blob/main/mihomo/models/character.py#L8-L80)
- [MiHoMo 전투/스킬 모델 — 설명과 현재 속성](https://github.com/MetaCubeX/mihomo/blob/main/mihomo/models/combat.py#L49-L84)
- [StarRailScore README — 유효 옵션과 계산 방식](https://github.com/Mar-7th/StarRailScore/blob/fb8268bc6345c52501bd4ec23f8df89b26497e0a/README.md#L11-L27)
- [StarRailScore 생성 스크립트 — 최고 롤 1.2와 여섯 부위 평균 `max`](https://github.com/Mar-7th/StarRailScore/blob/fb8268bc6345c52501bd4ec23f8df89b26497e0a/scripts/generate.py#L213-L248)
- [StarRailScore `score.json` — 적용한 고정 커밋](https://github.com/Mar-7th/StarRailScore/blob/fb8268bc6345c52501bd4ec23f8df89b26497e0a/score.json)
- [카스토리스 가중치 — 적용한 고정 커밋](https://github.com/Mar-7th/StarRailScore/blob/fb8268bc6345c52501bd4ec23f8df89b26497e0a/score.json#L4013-L4068)
- [카스토리스 본체 스킬의 최대 HP 계수](https://github.com/Mar-7th/StarRailRes/blob/master/index_new/en/character_skills.json#L10875-L10928)
- [카스토리스 기억 정령 스킬의 최대 HP 계수](https://github.com/Mar-7th/StarRailRes/blob/master/index_new/en/character_skills.json#L18092-L18141)
- [Fribbels Stat Score — 가중치 방법과 한계](https://github.com/fribbels/hsr-optimizer/blob/main/docs/guides/en/stat-score.md)
- [Fribbels Benchmark Score — 파티·패시브·구간을 포함한 전투 점수와의 차이](https://github.com/fribbels/hsr-optimizer/blob/main/docs/guides/en/benchmarks.md)

## 사실과 프로젝트 판단 구분

- 위 출처의 응답 필드, 12개 가중치, 카스토리스 HP 계수와 단순 가중치의 한계는 조사로 확인한 사실이다.
- 유효도 네 단계 구간, 캐릭터 유형 요약 문구, 원본 파일을 런타임에 조회하고 미등록 캐릭터를 N/A 처리하는 정책은 이 프로젝트에서 정한 구현 판단이다.
