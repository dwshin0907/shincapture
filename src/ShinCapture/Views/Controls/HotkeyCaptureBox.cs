using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShinCapture.Services.Hotkeys;

namespace ShinCapture.Views.Controls;

/// <summary>클릭 후 키 조합을 눌러 단축키를 설정하는 박스. Value는 "Ctrl+Shift+G" 형식("" = 미설정).</summary>
public class HotkeyCaptureBox : TextBox
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(HotkeyCaptureBox),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>사용자가 값을 확정(또는 비움)했을 때.</summary>
    public event EventHandler? ValueCommitted;

    public HotkeyCaptureBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = Cursors.Hand;
        Padding = new Thickness(4, 3, 4, 3);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (HotkeyCaptureBox)d;
        if (!box.IsKeyboardFocused)
            box.Text = (string)e.NewValue ?? "";
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Text = "키를 누르세요…";
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        Text = Value ?? "";
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape) { Text = Value ?? ""; Keyboard.ClearFocus(); return; }
        if (key == Key.Back || key == Key.Delete)
        {
            Value = ""; Text = "";
            ValueCommitted?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (HotkeyInput.IsModifierKey(key)) return; // 일반 키가 올 때까지 대기

        var mods = Keyboard.Modifiers;
        if (!HotkeyInput.IsValidGlobalHotkey(mods, key, out _)) return; // 무효 조합 무시

        Value = HotkeyInput.Format(mods, key);
        Text = Value;
        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }
}
