# 성문 협곡 맵

`Stage02Map`의 석교, 성문과 안뜰은 `Art/Material/FortressCrossing`의 머티리얼을 사용한다. 석재 색을 조정할 때는 배경 PBR의 `Adjust Diffuse Color`에서 색상, 채도, 명도와 대비를 바꾼다. 안뜰 포장은 `Fortress Grounds/Gate Courtyard Paving`에 있고 석교와 같은 바닥 머티리얼을 사용한다.

## 외곽 페이드

`Stage 02 Visibility`는 숲길과 다리, 협곡 중앙을 밝게 두고 숲길 양옆과 협곡 양 끝, 성벽 뒤를 약 10유닛에 걸쳐 어둡게 한다. 경계는 `Art/Terrain/FortressCrossing/Stage02Visibility.png`에서 편집한다. 흰색은 원래 밝기이고 검은색은 최대 어둡기다. 같은 폴더의 머티리얼에서 `Maximum darkness`를 바꿀 수 있다. 기본값은 1스테이지와 같은 0.8이다.

`BackgroundTest`에서 두 맵을 함께 볼 때는 2스테이지의 `StageTreeVisibility`에서 `Secondary Mask`를 켠다. 나무와 성문·망루·성벽은 각 오브젝트의 바닥 위치에서 해당 맵의 밝기를 읽는다.

성벽 뒤의 주 지형과 외곽 지형은 안뜰과 같은 높이로 이어진다. `Stage02VisibilitySurface` 메시는 강둑에서는 Terrain 높이를, 하천에서는 수면 높이를 따라간다. 지형이나 수위를 바꾸면 이 메시의 높이도 갱신한다. 경사가 화면에서 겹치는 곳은 스텐실로 페이드를 한 번만 적용한다.

## 강물

`Art/Shader/GorgeRiver.shader`를 사용하는 `River Slate` 머티리얼에서 `Current speed`, `Ripple strength`, `Foam coverage`로 속도, 잔물결, 포말을 조절한다. `Channel`과 `Shallows`는 깊은 물과 얕은 물의 색이다.

강 메시의 첫 번째 UV는 강줄기를 따라 잰 거리다. 셰이더는 시간차를 둔 두 물결을 섞고, 수심과 바위 뒤의 물살에 맞춰 포말을 끊어 낸다. 두 번째 UV로 읽는 `Riverbed Depth and Wake`에는 수심(R, −2~4m), 하류 쪽 난류의 세기(G), 물살이 옆으로 꺾이는 정도(B)가 저장돼 있다. 이 정보는 지형·암반·교각의 배치를 기준으로 구웠다. 물가에서 이들을 옮기면 해당 구간의 마스크도 갱신해야 한다.

흐름 보간은 [Catlike Coding의 Texture Distortion](https://catlikecoding.com/unity/tutorials/flow/texture-distortion/), 수심과 포말 처리는 [Unity Boat Attack의 물 셰이더](https://github.com/Unity-Technologies/BoatAttack/blob/master/Packages/com.verasl.water-system/Shaders/WaterCommon.hlsl)를 참고했다.

## 강둑과 돌 재질

강둑은 Terrain의 `Gorge Stone` 레이어와 `Rocky River Gorge/Rock Shelves` 아래의 층상 암반, 물가 자갈로 구성한다.

숲길 양옆의 소나무와 풀은 `River Gorge Terrain`의 `Paint Details`로 편집한다. 소나무는 1스테이지와 같은 `FR_Pine_01`과 `FR_Pine_03`을 사용한다. `DryGrassTuft`는 나무보다 흙길 가까이에 군락으로 칠했다.

다리 진입부의 닳은 포장석에는 Poly Haven의 CC0 돌 표면을 사용했다. [Rock Face](https://polyhaven.com/a/rock_face)
