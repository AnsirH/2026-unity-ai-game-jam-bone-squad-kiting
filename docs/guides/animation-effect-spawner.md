# 애니메이션 이벤트 이펙트

실제 Animator와 같은 오브젝트에 `AnimationEffectSpawner`를 추가한다.
Effects에 프리팹과 Spawn Point를 등록하고, 편집 가능한 애니메이션 클립의 원하는 프레임에
이벤트를 추가한다. Function은 `SpawnEffect`, Int는 Effects 배열 번호(0부터)다.

- Position Offset / Rotation Offset은 Spawn Point 기준이다.
- Follow Spawn Point를 끄면 생성한 월드 위치에 남는다. 바닥 데칼은 끈다.
- 파티클은 재생 완료 시 반환한다. Lifetime은 최대 유지 시간이며 시작 지연보다 길게 설정한다.
- 데칼은 Lifetime 후 반환한다. 파티클이 포함된 데칼을 시간 기준으로 유지하려면
  Return When Particles Finish를 끈다.
- 캐릭터가 비활성화되면 해당 스포너의 이펙트도 반환하고 재사용 시 이전 파티클과 궤적을 지운다.

이 스포너는 시각 연출만 실행하며 피해 판정이나 투사체 발사를 처리하지 않는다.
