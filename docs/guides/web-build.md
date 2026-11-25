# 웹 빌드와 Unity Play 업로드

Unity Play에는 웹(WebGL) 빌드만 올릴 수 있다. 이 문서는 웹 빌드를 뽑고 올리는 순서를 적는다.

## 빌드

에디터 메뉴 `Tools > Build > Web (Unity Play)`를 누르면 `bone-squad-kiting/Builds/Web`에 빌드가 만들어진다. 코드는 [WebBuild.cs](../../bone-squad-kiting/Assets/Editor/WebBuild.cs)에 있다.

Build Profiles 창의 `Build` 버튼으로는 빌드하지 않는다. 아래 두 가지 처리가 빠져서 브라우저에서 몇 분 동안 멈추고 용량도 커진다.

| 처리 | 이유 |
| --- | --- |
| 시작할 때 셰이더 미리 컴파일 목록(`Gameplay.shadervariants`)을 비움 | 브라우저에서는 변형 302개를 한꺼번에 컴파일하는 데 몇 분이 걸려 첫 화면이 뜨지 않는다. 빌드가 끝나면 원래대로 되돌린다. |
| 텍스처 최대 크기를 낮춤 | 원본 크기로는 용량이 1GB에 가까워진다. 빌드하는 동안에만 적용하므로 Windows 빌드 화질은 그대로다. |

텍스처 크기별 결과는 다음과 같다. 메뉴의 기본값은 1024다.

| 텍스처 최대 크기 | 용량 | 빌드 시간 |
| --- | --- | --- |
| 512 | 약 134MB | 약 10분 |
| 1024 | 약 300MB | 따로 재지 않음 |
| 2048 | 약 922MB | 약 1시간 50분 |

512와 2048은 게임 화면(960×600)에서 눈에 띄는 차이가 없어서, Unity Play에는 512 빌드를 올렸다. 512로 뽑으려면 배치 모드 인자 `-webMaxTexture 512`를 주거나 `WebBuild.BuildTo("Builds/Web", 512)`를 호출한다.

## 업로드

1. `Edit > Project Settings > Services`에서 프로젝트가 Unity Cloud에 연결돼 있는지 확인한다. 연결이 없으면 업로드가 "Project ID must not be empty!"로 실패한다.
2. `File > Build Profiles`에서 Web을 고르고 `Publish to Play`를 누른다.
3. `Locate Build...`로 `bone-squad-kiting/Builds/Web`을 고르고 `Publish`를 누른다. 이미 올린 게임을 고치는 경우에는 기존 게임 업데이트를 고른다.
4. 브라우저에 열리는 편집 페이지에서 제목과 설명을 확인하고 공개한다.

올릴 수 있는 용량은 압축 기준 1GB까지다.

## 빌드에서만 생기는 문제

에디터에서는 정상인데 빌드에서만 깨지는 경우가 있었다.

- 코드로 만든 기본 도형(`GameObject.CreatePrimitive`)의 기본 머티리얼은 빌드에서 쓸 수 없어 마젠타로 그려진다. 프로젝트의 머티리얼 파일을 복사해서 쓴다.
- 셰이더 기능(발광 등)을 코드로만 켜면 그 변형이 빌드에 들어가지 않는다. 그 기능을 켠 머티리얼 파일이 빌드에 포함돼 있어야 한다. 적 투사체는 이 때문에 `Prototype/Materials/EnemyShot.mat`을 쓴다.
