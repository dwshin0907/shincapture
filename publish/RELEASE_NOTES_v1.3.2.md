# 신캡쳐 v1.3.2 (핫픽스)

> 편집기 zoom/centering + 올가미 검은 점 + UX 개선.

## 🐛 수정

### 1. 작은 캡쳐 시 편집기에서 결과가 안 보이거나 38%/42% 같은 작은 zoom으로 표시

**근본 원인**: 편집기 윈도우 너비가 좁으면 광고 배너(200dip)가 들어 있는 툴바(`WrapPanel`)가 세로로 wrap되면서 `ScrollViewer` 영역이 줄어들고, 그 결과 viewport가 작아져 fit zoom이 잘못 계산됨. 추가로 `Show()` 직후 layout pass가 동기적으로 일어나지 않아 `ActualWidth`가 부정확한 상태에서 chrome이 측정되던 타이밍 버그도 있었음.

**수정**:
- 윈도우 minimum 사이즈를 `min(작업영역 50%, 1100x750)`으로 보장 — 와이드 모니터에서도 과도하게 커지지 않도록 cap.
- `ActualWidth < 1100`일 때 광고 배너 자동 hide (`SizeChanged` 핸들러).
- `SizeWindowToImage` 호출 전 `UpdateLayout()` 명시 호출로 chrome 측정 강제.
- `LoadNewCapture`에서 hidden 상태면 `Show()` 먼저 — 측정값이 정확한 visible 상태에서 사이즈 결정.

### 2. 캡쳐 결과가 편집기 ScrollViewer 안에서 좌상단 정렬 (윈도우 키워야 보임)

**수정**: `EditorCanvas`에 `HorizontalAlignment="Center" VerticalAlignment="Center"` 추가 — Canvas가 viewport보다 작을 때 자동 중앙 정렬.

### 3. 올가미(자유형) 캡쳐에서 둥근 모양에 검은 점/노치 생기는 문제

**근본 원인**: 사용자가 손으로 그린 폴리곤이 미세한 self-intersection / 좌표 round 오차로 1-5px 갭을 만들면, GDI+ `GraphicsPath`의 fill rule(Alternate / Winding)이 일부 영역을 outside로 판정해서 결과 비트맵에 검은 노치가 박혔음.

**수정**: GDI+ FillMode 의존을 버리고 자체 마스크 생성으로 전환 —
1. 외곽선을 4-connected Bresenham 라인으로 마스크에 찍기 (자동 close).
2. boundary 5x5 dilate — 손그림 미세 갭과 round 오차 흡수.
3. 비트맵 가장자리에서 BFS flood fill로 outside 결정.
4. outside만 투명, 나머지 모두 inside.

이 알고리즘은 polygon이 어떻게 자기교차하든 boundary로 둘러싸인 모든 영역을 inside로 처리하므로 검은 점이 원천 차단됨.

## ✨ 추가

- 캡쳐 기록 우클릭 메뉴에 **"저장 폴더 열기"** 추가.

## 📥 다운로드

- `ShinCapture_Setup_v1.3.2.exe`
- `ShinCapture_v1.3.2.zip`

v1.3.1 이전 사용자는 같은 위치에 덮어 설치 (설정 그대로 유지).
