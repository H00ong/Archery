# 새 스킬 Unity 세팅 가이드

새 스킬 SO를 Unity에서 어떻게 추가해야 하는지, 지금 코드 기준으로 바로 따라 할 수 있게 정리해드릴게요.네, Unity 창에서 해야 할 작업만 기준으로 정리하면 됩니다. 핵심은 **새 스킬마다 `SkillDefinition` ScriptableObject를 하나씩 만들고**, 그 안에 **id / maxLevel / moduleScript / availability / icon / Addressables label**을 맞추는 것입니다.

## 1. SkillDefinition 만드는 기본 절차

가장 안전한 방법은 기존 스킬 SO를 복제하는 방식입니다.

1. Unity Project 창에서  
   `Assets/Scriptable Data/Player Skill Data/` 이동

2. 기존 스킬 SO 하나 선택  
   예: `MoveSpeedIncrease`, `MultiShot`, `IceOrb` 같은 기존 SkillDefinition

3. `Ctrl + D`로 복제

4. 이름을 새 스킬 이름으로 변경  
   예: `FireEnhance`, `PierceShot`, `HealthRegen`

5. Inspector에서 아래 필드 수정

| 필드                  | 설정                                   |
| --------------------- | -------------------------------------- |
| `id`                  | 스킬 클래스 이름과 정확히 동일         |
| `rarity`              | 원하는 희귀도                          |
| `maxLevel`            | 아래 표 참고                           |
| `icon`                | 스킬 선택 UI에 보일 아이콘 Sprite      |
| `moduleScript`        | 해당 `.cs` 스크립트 드래그             |
| `availability`        | 조건부 등장 여부                       |
| `conditionEffectType` | Fire/Ice/Poison/Lightning 등 조건 속성 |

6. Addressables 체크  
   해당 SkillDefinition asset에 **`skill_config` label** 추가

---

## 2. 새로 추가된 SkillDefinition 목록

아래 항목들을 `Assets/Scriptable Data/Player Skill Data/` 아래에 각각 만들어주면 됩니다.

| SkillDefinition 이름 / id | moduleScript              | maxLevel | availability                 | conditionEffectType |
| ------------------------- | ------------------------- | -------: | ---------------------------- | ------------------- |
| `FireAttackGrant`         | `FireAttackGrant.cs`      |        1 | `RequireAttackEffectMissing` | `Fire`              |
| `IceAttackGrant`          | `IceAttackGrant.cs`       |        1 | `RequireAttackEffectMissing` | `Ice`               |
| `PoisonAttackGrant`       | `PoisonAttackGrant.cs`    |        1 | `RequireAttackEffectMissing` | `Poison`            |
| `LightningAttackGrant`    | `LightningAttackGrant.cs` |        1 | `RequireAttackEffectMissing` | `Lightning`         |
| `FireEnhance`             | `FireEnhance.cs`          |        3 | `RequireAttackEffectPresent` | `Fire`              |
| `IceEnhance`              | `IceEnhance.cs`           |        3 | `RequireAttackEffectPresent` | `Ice`               |
| `PoisonEnhance`           | `PoisonEnhance.cs`        |        3 | `RequireAttackEffectPresent` | `Poison`            |
| `LightningEnhance`        | `LightningEnhance.cs`     |        3 | `RequireAttackEffectPresent` | `Lightning`         |
| `FireBarrel`              | `FireBarrel.cs`           |        3 | `Always`                     | 아무거나            |
| `PoisonBarrel`            | `PoisonBarrel.cs`         |        3 | `Always`                     | 아무거나            |
| `LightningBarrel`         | `LightningBarrel.cs`      |        3 | `Always`                     | 아무거나            |
| `ShieldBarrel`            | `ShieldBarrel.cs`         |        3 | `Always`                     | 아무거나            |
| `HealthBarrel`            | `HealthBarrel.cs`         |        3 | `Always`                     | 아무거나            |
| `FireOrb`                 | `FireOrb.cs`              |        3 | `Always`                     | 아무거나            |
| `LightningOrb`            | `LightningOrb.cs`         |        3 | `Always`                     | 아무거나            |
| `PierceShot`              | `PierceShot.cs`           |        3 | `Always`                     | 아무거나            |
| `HomingShot`              | `HomingShot.cs`           |        3 | `Always`                     | 아무거나            |
| `ReflectShot`             | `ReflectShot.cs`          |        3 | `Always`                     | 아무거나            |
| `MaxHPIncrease`           | `MaxHPIncrease.cs`        |        3 | `Always`                     | 아무거나            |
| `HealthRegen`             | `HealthRegen.cs`          |        3 | `Always`                     | 아무거나            |
| `DefenseIncrease`         | `DefenseIncrease.cs`      |        3 | `Always`                     | 아무거나            |
| `GroundZoneSkill`         | `GroundZoneSkill.cs`      |        3 | `Always`                     | 아무거나            |

`AttackGrant` 계열은 기본 공격에 해당 속성이 없을 때만 떠야 하므로 `maxLevel = 1`입니다. 획득하면 다시 뜨면 안 됩니다.

`Enhance` 계열은 해당 속성을 이미 가진 상태에서만 뜨게 하려면 `RequireAttackEffectPresent`로 두면 됩니다. 만약 예전에 말했던 것처럼 “FireEnhance를 먼저 먹고 나중에 FireAttackGrant를 먹어도 누적 강화분이 바로 적용”되는 흐름을 원하면, Enhance 계열만 `Always`로 바꾸면 됩니다.

---

## 3. NameRegistry 확인

`Assets/Scriptable Data/Registry/NameRegistry.asset`에서 `skillIds` 목록에 아래 id들이 들어가 있어야 합니다.

```text
FireEnhance
IceEnhance
PoisonEnhance
LightningEnhance
FireAttackGrant
IceAttackGrant
PoisonAttackGrant
LightningAttackGrant
FireBarrel
PoisonBarrel
LightningBarrel
ShieldBarrel
HealthBarrel
FireOrb
LightningOrb
PierceShot
HomingShot
ReflectShot
MaxHPIncrease
HealthRegen
DefenseIncrease
GroundZoneSkill
```

이미 내가 코드 작업하면서 추가해둔 상태일 가능성이 높지만, Unity Inspector에서 한 번만 확인하면 됩니다. 빠져 있으면 직접 추가하세요.

---

## 4. Barrel 스킬은 BarrelScriptable도 필요함

아래 스킬들은 SkillDefinition만 만들면 끝이 아니고, `BarrelManager`가 읽을 `BarrelScriptable` asset도 필요합니다.

필요한 BarrelScriptable:

| BarrelScriptable  | type        | kind     |
| ----------------- | ----------- | -------- |
| `FireBarrel`      | `Fire`      | `Attack` |
| `PoisonBarrel`    | `Poison`    | `Attack` |
| `LightningBarrel` | `Lightning` | `Attack` |
| `ShieldBarrel`    | `Shield`    | `Shield` |
| `HealthBarrel`    | `Heal`      | `Heal`   |

작업 위치:

```text
Assets/Scriptable Data/Barrel Data/
```

해야 할 것:

1. 기존 `IceBarrel` 또는 기존 BarrelScriptable 복제
2. 이름 변경
3. `type` 설정
4. `kind` 설정
5. `barrelPrefab` 연결
6. 공격 Barrel이면 `meteorPrefab`도 연결
7. 수치 필드 설정
8. Addressables에서 **`barrel_config` label** 추가

`ShieldBarrel`, `HealthBarrel`은 공격 투사체가 아니라 즉시 보호막/회복을 주는 구조라서 `kind`를 꼭 각각 `Shield`, `Heal`로 맞춰야 합니다.

---

## 5. Orb 스킬은 Orb 설정 asset도 필요할 수 있음

아래 스킬은 `OrbManager`가 해당 속성 Orb 설정을 찾아야 정상 생성됩니다.

| 스킬           | 필요한 Orb type |
| -------------- | --------------- |
| `FireOrb`      | `Fire`          |
| `LightningOrb` | `Lightning`     |

작업 위치는 기존 Orb 설정 asset들이 있는 곳입니다. 보통:

```text
Assets/Scriptable Data/Orb Data/
```

해야 할 것:

1. 기존 `IceOrb` 또는 `VenomOrb` 설정 asset 복제
2. `type`을 `Fire` 또는 `Lightning`으로 변경
3. Orb prefab 연결
4. 회전 속도, 데미지 등 원하는 값 조정
5. Addressables에서 **`orb_config` label** 추가

`SkillDefinition`만 있고 Orb 설정 asset이 없으면 스킬은 선택돼도 실제 Orb 생성에서 실패하거나 아무것도 안 나올 수 있습니다.

---

## 6. 화살 스킬에서 추가로 확인할 것

`PierceShot`, `HomingShot`, `ReflectShot`은 SkillDefinition만 만들면 대부분 동작합니다.

다만 `ReflectShot`은 화살 prefab 설정을 확인해야 합니다.

- 플레이어 화살 prefab에서 `Projectile` 컴포넌트 확인
- 장애물에 닿았을 때 반사하려면 기존 `destroyOnObstacle` 같은 옵션이 켜져 있어야 반사 분기가 정상적으로 탑니다
- 장애물 Collider가 제대로 Trigger/Collision 이벤트를 주는지도 확인

`HomingShot`은 `maxLevel = 3`이면 꽤 강할 수 있습니다. 너무 심하게 따라가면 Unity에서 `maxLevel = 1` 또는 `2`로 낮춰도 됩니다.

---

## 7. GroundZoneSkill 확인

`GroundZoneSkill`은 코드상 스킬 선택 UI에 뜨려면 SkillDefinition asset이 필요합니다.

설정:

| 필드               | 값                   |
| ------------------ | -------------------- |
| `id`               | `GroundZoneSkill`    |
| `moduleScript`     | `GroundZoneSkill.cs` |
| `maxLevel`         | 3                    |
| `availability`     | `Always`             |
| `icon`             | 장판 아이콘          |
| Addressables label | `skill_config`       |

레벨이 오르면 장판 데미지와 범위가 같이 증가하도록 코드가 이미 바뀐 상태입니다.

---

## 8. 최종 체크리스트

Unity에서 마지막으로 이것만 확인하면 됩니다.

1. 새 SkillDefinition asset들이 전부 있음
2. 각 asset의 `id`가 클래스 이름과 정확히 같음
3. `moduleScript`에 해당 `.cs` 파일이 들어가 있음
4. 모든 SkillDefinition에 `skill_config` Addressables label이 있음
5. Grant 스킬은 `maxLevel = 1`
6. 대부분의 새 스킬은 `maxLevel = 3`
7. AttackGrant는 `RequireAttackEffectMissing`
8. Enhance는 원하는 정책에 따라 `RequireAttackEffectPresent` 또는 `Always`
9. Barrel 스킬용 BarrelScriptable asset 생성 + `barrel_config` label
10. Fire/Lightning Orb 설정 asset 생성 + `orb_config` label
11. NameRegistry의 `skillIds`에 새 id들이 등록되어 있음

이대로 하면 Unity 에디터 쪽 세팅은 거의 끝입니다.

---

## 9. HP 슬라이더 / Regen / 적 체력바 UI 세팅

`HealthRegen` 스킬 등을 UI에 보이게 하려면 스킬 코드 외에 InGame HUD 쪽 Unity 세팅이 별도로 필요합니다. 코드(`InGameHud.cs`, `EnemyHealthBar.cs`)는 이미 완성되어 있고, 아래 오브젝트를 만들어 인스펙터 필드에 연결하기만 하면 됩니다.

### 9-1. 플레이어 HP 슬라이더 + Regen 텍스트 (`InGameHud.cs`, InGameCanvas)

InGameCanvas에 있는 `InGameHud` 컴포넌트 인스펙터에서:

| 필드            | 설정                                                                                              |
| --------------- | ------------------------------------------------------------------------------------------------- |
| `hpSlider`      | HP를 표시할 Slider (value는 코드가 매 프레임 0~1로 자동 세팅)                                     |
| `hpText`        | (선택) "현재/최대" TMP 텍스트                                                                     |
| `characterIcon` | 현재 캐릭터 아이콘 Image                                                                          |
| `regenText`     | HP 슬라이더 옆에 배치할 TMP 텍스트. `HealthRegen` 보유 &amp; 회복량 &gt; 0일 때만 `"+N/sec"` 표시 |
| `regenRoot`     | (선택) 회복 스킬 없을 때 꺼질 오브젝트. 비워두면 `regenText` 자신이 꺼짐                          |
| `shieldSlider`  | HP 슬라이더와 별개의 Slider. 보호막(`Health.Shield`)이 0보다 클 때만 표시                         |
| `shieldRoot`    | (선택) 비워두면 `shieldSlider` 자신이 꺼짐                                                        |

Slider/TMP 오브젝트를 씬에 만들어서 드래그해 넣기만 하면 값 갱신, on/off 처리는 `Update()`가 자동으로 처리합니다.

### 9-2. 적 머리 위 HP 슬라이더 (`EnemyHealthBar.cs`, 적 프리팹)

각 적 프리팹에 필요한 작업:

1. 적 프리팹 자식으로 **World Space Canvas** 생성 (머리 위쯤 위치, 작은 스케일)
2. 그 캔버스 자식으로 **Slider** 생성
3. 캔버스(또는 원하는 오브젝트)에 `EnemyHealthBar` 컴포넌트를 붙이고:
   - `hpSlider` → 방금 만든 Slider
   - `worldOffset` → 기본값 (0, 2, 0), 필요시 조정
   - `hideWhenFull` → 체력이 가득 찼을 때 숨길지 여부 (기본 `true`)
4. `EnemyController.healthBar`는 같은 프리팹 자식에서 `GetComponentInChildren`으로 자동으로 찾으므로 보통 수동 연결 불필요

주의: `EnemyHealthBar`는 `CameraController.Instance`를 참조해 매 `LateUpdate`마다 카메라를 향하도록 회전하므로, 씬에 `CameraController`가 있어야 합니다. 또한 `hideWhenFull=true`면 체력이 꽉 찼거나(1) 0일 때 자동으로 안 보이는 게 정상 동작입니다 (버그 아님).
