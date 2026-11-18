# 셰이더 배리언트 프리로드

게임을 시작할 때 Unity가 `Assets/Settings/Gameplay.shadervariants`를 읽는다. 등록 위치는 **Project Settings → Graphics → Preloaded Shaders**다. 현재 컬렉션에는 셰이더 31종의 배리언트 264개가 있다. 한 항목은 셰이더, 패스 유형, 함께 켜지는 키워드의 조합이다. 플랫폼별 최종 빌드의 컴파일 수는 달라질 수 있다.

## 등록한 키워드

| 용도 | 컬렉션에 들어 있는 키워드 |
| --- | --- |
| 배경 표면과 가시성 | `_NORMALMAP`, `_METALLICSPECGLOSSMAP`, `_DIFFUSE_COLOR_ADJUST`, `_DITHER_FADE`, `_CLIP_ABOVE_HEIGHT`, `_TREE_ROOT_VISIBILITY` |
| 발광·투명 표면·지형 | `_EMISSION`, `_SURFACE_TYPE_TRANSPARENT`, `_ALPHATEST_ON`, `_MASKMAP` |
| 광원과 그림자 | `_CLUSTER_LIGHT_LOOP`, `_ADDITIONAL_LIGHTS`, `_ADDITIONAL_LIGHT_SHADOWS`, `_MAIN_LIGHT_SHADOWS`, `_MAIN_LIGHT_SHADOWS_CASCADE`, `_SHADOWS_SOFT`, `_LIGHT_LAYERS`, `EVALUATE_SH_VERTEX` |
| 반사·인스턴싱·깊이 | `_REFLECTION_PROBE_ATLAS`, `_REFLECTION_PROBE_BLENDING`, `_REFLECTION_PROBE_BOX_PROJECTION`, `INSTANCING_ON`, `_OUTPUT_DEPTH` |
| 화면 공간 앰비언트 오클루전 | `_SCREEN_SPACE_OCCLUSION`, `_ORTHOGRAPHIC`, `_BLUE_NOISE`, `_INTERLEAVED_GRADIENT`, `_SAMPLE_COUNT_LOW`, `_SAMPLE_COUNT_MEDIUM`, `_SOURCE_DEPTH_LOW`, `_SOURCE_DEPTH_NORMALS` |
| 후처리·하늘 | `_BLOOM_HQ`, `_CHROMATIC_ABERRATION`, `_DISTORTION`, `_FILM_GRAIN`, `_TONEMAP_ACES`, `_TONEMAP_NEUTRAL`, `_USE_FAST_SRGB_LINEAR_CONVERSION`, `_SUNDISK_SIMPLE` |

Unity 에디터의 렌더링 기록과 머티리얼을 대조했다. 빌드 목록의 `Game`과 `Arena`에서 PC·Mobile 렌더 파이프라인을 확인하고, 불투명 투사체가 실행 중 켜는 URP Lit의 `_EMISSION`과 모바일 환경광의 정점 계산 조합을 보강했다. 몹의 피격 발광과 디졸브는 수치로 조절한다. 공격 예고 장판과 참격 셰이더는 키워드가 비어 있는 기본 패스를 등록했다.

새 머티리얼 옵션이나 실행 중 켜는 키워드를 추가하면 해당 효과를 게임 카메라에 표시하고, 사용하는 패스와 조명 조합을 이 컬렉션에 추가한다. 컬렉션 에셋의 Inspector에서 셰이더별 등록 수와 키워드를 확인할 수 있다.
