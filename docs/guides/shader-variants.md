# 셰이더 배리언트 프리로드

게임을 시작할 때 Unity가 `Assets/Settings/Gameplay.shadervariants`를 읽는다. 등록 위치는 **Project Settings → Graphics → Preloaded Shaders**다. 현재 컬렉션에는 셰이더 38종의 배리언트 302개가 있다. 한 항목은 셰이더, 패스 유형, 함께 켜지는 키워드의 조합이다. 플랫폼별 최종 빌드의 컴파일 수는 달라질 수 있다.

## 등록한 키워드

| 용도 | 컬렉션에 들어 있는 키워드 |
| --- | --- |
| 배경 표면과 가시성 | `_NORMALMAP`, `_METALLICSPECGLOSSMAP`, `_DIFFUSE_COLOR_ADJUST`, `_DITHER_FADE`, `_CLIP_ABOVE_HEIGHT`, `_TREE_ROOT_VISIBILITY` |
| 발광·투명 표면·지형 | `_EMISSION`, `_MOB_EMISSION_MAP`, `_SURFACE_TYPE_TRANSPARENT`, `_ALPHAPREMULTIPLY_ON`, `_ALPHATEST_ON`, `_MASKMAP` |
| 바닥 데칼 | `_DBUFFER_MRT2` |
| 광원과 그림자 | `_CLUSTER_LIGHT_LOOP`, `_ADDITIONAL_LIGHTS`, `_ADDITIONAL_LIGHT_SHADOWS`, `_MAIN_LIGHT_SHADOWS`, `_MAIN_LIGHT_SHADOWS_CASCADE`, `_SHADOWS_SOFT`, `_LIGHT_LAYERS`, `EVALUATE_SH_VERTEX` |
| 반사·인스턴싱·깊이 | `_REFLECTION_PROBE_ATLAS`, `_REFLECTION_PROBE_BLENDING`, `_REFLECTION_PROBE_BOX_PROJECTION`, `INSTANCING_ON`, `_OUTPUT_DEPTH` |
| 화면 공간 앰비언트 오클루전 | `_SCREEN_SPACE_OCCLUSION`, `_ORTHOGRAPHIC`, `_BLUE_NOISE`, `_INTERLEAVED_GRADIENT`, `_SAMPLE_COUNT_LOW`, `_SAMPLE_COUNT_MEDIUM`, `_SOURCE_DEPTH_LOW`, `_SOURCE_DEPTH_NORMALS` |
| 후처리·하늘 | `_BLOOM_HQ`, `_CHROMATIC_ABERRATION`, `_DISTORTION`, `_FILM_GRAIN`, `_TONEMAP_ACES`, `_TONEMAP_NEUTRAL`, `_USE_FAST_SRGB_LINEAR_CONVERSION`, `_SUNDISK_SIMPLE` |

현재 빌드 씬은 `MainMenu`와 `Game`이다. `Game`이 실행 중 불러오는 `Resources/StageKit.prefab`도 함께 확인한다. 적 창 투사체에는 `Cone 1`과 `Cone_Alpha`, 공격 예고 장판에는 `TelegraphOverlay`, 적 체력바에는 `Sprite-Unlit-Default`가 필요하다. 석궁 발사 섬광은 Particles/Unlit에서 발광과 투명 키워드를 함께 켠다. 기본 적 투사체는 실행 중 URP Lit의 `_EMISSION`을 켠다.

프로스트 나이트의 갑옷과 소환 검은 Mob PBR에서 노멀 맵, 메탈릭 맵, 발광 맵을 함께 사용한다. 청염은 별도 셰이더를 쓴다. `Core.mat`에는 Particles/Lit의 발광·투명·프리멀티플라이 알파 조합을 등록했다. PC의 바닥 균열 데칼은 색상과 노멀을 기록하는 `_DBUFFER_MRT2` 패스를 사용한다.

새 머티리얼 옵션이나 실행 중 켜는 키워드를 추가하면 해당 효과를 게임 카메라에 표시하고, 사용하는 패스와 조명 조합을 이 컬렉션에 추가한다. 컬렉션 에셋의 Inspector에서 셰이더별 등록 수와 키워드를 확인할 수 있다.
