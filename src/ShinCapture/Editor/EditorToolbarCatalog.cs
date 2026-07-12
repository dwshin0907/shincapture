using System.Collections.Generic;

namespace ShinCapture.Editor;

public sealed record EditorToolDescriptor(
    string Name,
    string IconKey,
    string Group,
    string Shortcut,
    string ToolTip);

public static class EditorToolbarCatalog
{
    public static IReadOnlyList<EditorToolDescriptor> Tools { get; } =
    [
        new("선택", "cursor", "select", "V", "선택 (V)"),
        new("펜", "pen", "draw", "P", "펜 (P)"),
        new("형광펜", "highlighter", "draw", "H", "형광펜 (H)"),
        new("도형", "shape", "draw", "U", "도형 (U)"),
        new("화살표", "arrow", "draw", "A", "화살표 (A)"),
        new("텍스트", "text", "text", "T", "텍스트 (T)"),
        new("말풍선", "balloon", "text", "B", "말풍선 (B)"),
        new("모자이크", "mosaic", "effect", "M", "모자이크 (M)"),
        new("블러", "blur", "effect", "", "블러"),
        new("번호", "number", "effect", "N", "번호 (N)"),
        new("이미지", "image", "insert", "I", "이미지 (I)"),
        new("색상추출", "eyedropper", "insert", "", "색상 추출"),
        new("크롭", "crop", "edit", "C", "크롭 (C)"),
        new("지우개", "eraser", "edit", "E", "지우개 (E)")
    ];
}
