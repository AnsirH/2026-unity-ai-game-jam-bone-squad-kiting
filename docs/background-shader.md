# 배경 PBR 셰이더

배경 메시의 머티리얼에서 `Bone Squad/Background PBR`을 선택한다. 기본값은 불투명이다. `Base Map`과 `Base Color`로 색을 정하고, `Metallic`과 `Smoothness`로 금속성과 매끄러움을 조절한다.

`Assets/Art/Fbx/ForestRuins`의 배경 모델은 이 셰이더를 사용한다. 해당 머티리얼은 기존 양면 표현을 유지하도록 `Cull Mode`를 `Off`로 설정했다. 새 머티리얼의 기본값은 뒷면을 생략하는 `Back`이다.

| 머티리얼 옵션 | 로컬 키워드 | 켰을 때의 동작 |
| --- | --- | --- |
| Use Normal Map | `_NORMALMAP` | `Normal Map`의 접선 공간 노멀을 사용한다. 텍스처의 임포트 타입은 `Normal map`으로 설정하고 강도는 `Normal Strength`로 조절한다. |
| Use Metallic Smoothness Map | `_METALLICSPECGLOSSMAP` | 맵의 R 채널을 금속성으로, A 채널과 `Smoothness`를 곱한 값을 매끄러움으로 쓴다. 이 맵은 sRGB를 끄고 임포트한다. |
| Adjust Diffuse Color | `_DIFFUSE_COLOR_ADJUST` | `Base Map`의 RGB에 HSV와 대비를 적용한 뒤 `Base Color`를 곱한다. 기본값은 꺼짐이다. |
| Dithered Opacity | `_DITHER_FADE` | `Opacity × Base Color의 알파 × Base Map의 알파`로 남길 픽셀의 비율을 정한다. |
| Clip Above Height | `_CLIP_ABOVE_HEIGHT` | `Cutoff Height (World Y)`보다 높은 부분을 화면, 그림자, 깊이·노멀 및 라이트맵에서 제외한다. |

2스테이지 철문의 `Portcullis - Crossing` 머티리얼은 절단 높이 10.48을 사용한다. 철문을 올리면 아치 윗선에서 격자가 사라진다. 성채 전체의 높이를 옮길 때는 같은 거리만큼 절단 높이도 옮긴다.

디더링을 켜고 최종 불투명도를 0.5로 설정하면 4×4픽셀마다 8픽셀이 남는다. 0에서는 모두 사라지고 1에서는 모두 남는다. 남은 픽셀은 불투명 재질처럼 깊이를 기록한다. 색상과 카메라 깊이·노멀 패스는 화면 픽셀을, 그림자 패스는 섀도 맵의 픽셀을 기준으로 같은 패턴을 계산한다.

### 디퓨즈 색 보정

`Diffuse Color`에서 `Adjust Diffuse Color`를 켠다. `Hue Shift (Degrees)`는 −180°부터 +180°까지 색상을 회전한다. `Saturation`과 `Value`는 0~2배이며 각각 0에서 무채색과 검정이 된다. `Contrast`는 HSV 보정 후 sRGB의 중간 회색(0.5)을 기준으로 적용한다. 0이면 중간 회색, 1이면 원래 대비, 2이면 대비가 두 배다. 색상 이동 0°와 나머지 슬라이더 1이 중립값이다.

색 보정은 화면 조명과 라이트맵 베이크에 쓰이는 표면색에 적용한다. 디더링에 쓰는 알파는 원래 텍스처 값을 사용한다.

## 표현 기능 추가

- `Assets/Art/Shader/Utility.hlsl`에는 다른 셰이더에서도 쓸 수 있는 디더 임계값 계산이 있다.
- `Assets/Art/Shader/Background/BackgroundSurface.hlsl`에서 배경 전용 표면 입력과 가시성을 계산한다. 새 재질 속성은 모든 변형에서 같은 `UnityPerMaterial` 레이아웃을 갖도록 선언한다.
- `BackgroundPBR.shader`에 `[Toggle(_KEYWORD)]` 속성을 추가하고, 해당 기능을 쓰는 패스에 `#pragma shader_feature_local`을 선언한다. 전용 HLSL에서 계산과 텍스처 샘플링을 `#if defined(_KEYWORD)`로 감싼다. 픽셀 가시성이 달라지는 기능은 그림자와 깊이 패스에도 적용한다. 두 패스의 공통 구현은 `BackgroundDepth.hlsl`에 있다.
- 빌드에서 실행 중에만 켤 키워드는 해당 조합을 사용하는 머티리얼을 빌드에 포함하거나 Shader Variant Collection으로 보존한다.
