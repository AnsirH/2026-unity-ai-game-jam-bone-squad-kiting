# 아군·적 몹 PBR 셰이더

몹 머티리얼의 Shader 메뉴에서 `Bone Squad/Mob PBR`을 선택한다. 아군은 가장자리와 피격 발광이 모두 밝은 하늘색이고, 적은 두 효과 모두 빨간색이다. 새 모델을 추가할 때는 몸과 장착 무기에 같은 진영 설정을 적용한다.

`Assets/Art/Material/MobExamples`에는 병사 텍스처를 사용하는 `AllyMobExample`과 근접 적 텍스처를 사용하는 `EnemyMobExample`이 있다. 해당 진영의 예시를 복제한 뒤 `Base Map`을 새 모델의 텍스처로 교체한다.

| 진영 | 프레넬·피격 발광색 (Unity Color RGB) | Fresnel Intensity |
| --- | --- | --- |
| 아군 | (0.18, 0.346, 1) | 0.5 |
| 적 | (1, 0, 0) | 0.75 |

| 인스펙터 항목 | 사용법 |
| --- | --- |
| Base Map, Base Color | 텍스처와 색상을 곱해 표면색을 정한다. 두 알파 값은 모두 디더링에 반영한다. |
| Metallic, Smoothness | 배경 PBR과 같은 방식으로 금속성과 매끄러움을 조절한다. `Use Metallic Smoothness Map`을 켜면 맵의 R이 금속성을 정하고 A에 `Smoothness`를 곱한다. 맵의 sRGB 설정은 끈다. |
| Use Normal Map | 임포트 타입이 Normal map인 텍스처를 넣고 `Normal Strength`로 강도를 조절한다. |
| Hit Flash Color, Hit Flash Intensity | 피격 순간 표면 전체에 더할 진영 색과 최대 발광 강도(0~10)를 정한다. 기본 강도는 3이며, 0이면 피격 발광이 꺼진다. |
| Fresnel Color, Intensity, Power | 모델 가장자리에 더할 빛의 색과 밝기를 정한다. Power를 높이면 빛이 가장자리로 좁아지고, Intensity를 0으로 두면 꺼진다. |
| Opacity | 1에서 0으로 낮추면 픽셀이 4×4 디더 패턴으로 사라진다. 최종 불투명도는 `Opacity × Base Color 알파 × Base Map 알파`이며, 남은 픽셀은 깊이를 기록한다. |
| Dissolve Amount | 0이면 온전한 모델이고, 1이면 모두 사라진다. 중간값에서는 모델 UV를 따라 노이즈 무늬로 사라진다. 별도 노이즈 텍스처는 필요 없다. |
| Noise Scale, Edge Color, Edge Width | Scale을 높이면 디졸브 무늬가 작아진다. Edge Color는 사라지는 경계의 발광색이며, Edge Width를 0으로 두면 경계 발광이 꺼진다. |

그림자와 카메라 깊이·노멀에도 같은 디졸브가 적용된다. 디더 패턴은 카메라 패스에서는 화면 픽셀, 그림자 패스에서는 섀도 맵 픽셀을 기준으로 계산한다.

피격 시 해당 개체 렌더러의 `MaterialPropertyBlock`에서 `_HitFlashAmount`를 1로 설정한 뒤 0으로 낮춘다. 1이면 설정한 밝기로 빛나고, 0이면 원래 모습으로 돌아간다. 무기나 갑옷도 함께 빛나게 하려면 각 머티리얼에 이 셰이더를 지정한다.

연출 코드에서는 개별 렌더러의 `MaterialPropertyBlock`으로 `_Opacity`와 `_DissolveAmount`를 0~1 범위에서 조절한다. 피격 발광은 디더와 디졸브를 통과한 표면에만 더해진다.
