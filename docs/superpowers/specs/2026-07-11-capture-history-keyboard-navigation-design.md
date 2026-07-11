# Capture History Keyboard Navigation Design

## Goal

Allow keyboard users to move through the editor's right-side capture history with the Up and Down arrow keys without changing the existing canvas-object arrow-key behavior.

## Interaction contract

- History navigation is active only while keyboard focus is within a capture-history thumbnail.
- Up selects the visually previous thumbnail; Down selects the visually next thumbnail.
- Navigation clamps at the first and last entries and never wraps.
- Changing the selection immediately loads that capture and its saved editor objects.
- The newly selected thumbnail keeps keyboard focus and is scrolled into view.
- A mouse click loads and focuses the clicked thumbnail so the next arrow press continues from it.
- The currently displayed capture is the single Tab stop within the history list.
- Arrow keys outside the history retain their existing editor-object movement behavior.

## Architecture

Keep the existing dynamically-created thumbnail cards. Add a small pure `CaptureHistoryNavigationPolicy` that calculates the next index, and let `EditorWindow` own focus detection, capture loading, card rebuilding, and focus restoration. This avoids a larger `ListBox`/binding rewrite while still giving the list native-feeling keyboard behavior.

## Edge cases

- An empty history produces no target.
- If no current entry can be resolved, Down starts at the first item and Up starts at the last.
- Modified Up/Down keys while focus is in history are consumed rather than moving canvas objects.
- Rebuilding the history must not leave focus on a removed visual; focus restoration runs after layout at input priority.

## Testing

- Pure policy tests cover Up, Down, first/last clamping, empty history, and unresolved selection.
- Focused tests run before the full Release suite.
- A Release build validates WPF event wiring and XAML.
