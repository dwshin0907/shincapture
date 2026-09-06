using System.Collections.Generic;

namespace ShinCapture.Editor;

public sealed record EditorToolDescriptor(
    string Name,
    string DisplayName,
    string IconKey,
    string Group,
    string Shortcut,
    string ToolTip,
    EditorToolVisibility Visibility);

public static class EditorToolbarCatalog
{
    public static IReadOnlyList<EditorToolDescriptor> Tools { get; } =
    [
        new("선택", "선택", "cursor", "select", "V", "선택 도구 (V)", EditorToolVisibility.Essential),
        new("펜", "펜", "pen", "draw", "P", "펜으로 그리기 (P)", EditorToolVisibility.Essential),
        new("형광펜", "형광펜", "highlighter", "draw", "H", "형광펜으로 강조 (H)", EditorToolVisibility.Essential),
        new("도형", "도형", "shape", "draw", "U", "도형 그리기 (U)", EditorToolVisibility.Essential),
        new("화살표", "화살표", "arrow", "draw", "A", "화살표 그리기 (A)", EditorToolVisibility.Essential),
        new("텍스트", "텍스트", "text", "text", "T", "텍스트 추가 (T)", EditorToolVisibility.Common),
        new("말풍선", "말풍선", "balloon", "text", "B", "말풍선 추가 (B)", EditorToolVisibility.All),
        new("모자이크", "모자이크", "mosaic", "effect", "M", "영역 모자이크 (M)", EditorToolVisibility.Common),
        new("블러", "블러", "blur", "effect", "", "영역 흐리게", EditorToolVisibility.All),
        new("번호", "번호", "number", "effect", "N", "순서 번호 추가 (N)", EditorToolVisibility.All),
        new("이미지", "이미지", "image", "insert", "I", "이미지 삽입 (I)", EditorToolVisibility.All),
        new("색상추출", "색상 추출", "eyedropper", "insert", "", "이미지에서 색상 추출", EditorToolVisibility.All),
        new("크롭", "자르기", "crop", "edit", "C", "이미지 자르기 (C)", EditorToolVisibility.All),
        new("지우개", "지우개", "eraser", "edit", "E", "개체 지우기 (E)", EditorToolVisibility.All)
    ];
}
