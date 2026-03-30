using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class CommandStackTests
{
    [Fact]
    public void Execute_AddsToUndoStack()
    {
        var stack = new CommandStack();
        var cmd = new TestCommand();
        stack.Execute(cmd);
        Assert.True(cmd.Executed);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Undo_ReversesLastCommand()
    {
        var stack = new CommandStack();
        var cmd = new TestCommand();
        stack.Execute(cmd);
        stack.Undo();
        Assert.True(cmd.Undone);
        Assert.False(stack.CanUndo);
        Assert.True(stack.CanRedo);
    }

    [Fact]
    public void Redo_ReExecutesCommand()
    {
        var stack = new CommandStack();
        var cmd = new TestCommand();
        stack.Execute(cmd);
        stack.Undo();
        stack.Redo();
        Assert.Equal(2, cmd.ExecuteCount);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Execute_AfterUndo_ClearsRedoStack()
    {
        var stack = new CommandStack();
        stack.Execute(new TestCommand());
        stack.Undo();
        stack.Execute(new TestCommand());
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        var stack = new CommandStack();
        stack.Execute(new TestCommand());
        stack.Execute(new TestCommand());
        stack.Clear();
        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Changed_EventFires_OnExecuteUndoRedo()
    {
        var stack = new CommandStack();
        var count = 0;
        stack.Changed += (_, _) => count++;
        stack.Execute(new TestCommand());
        stack.Undo();
        stack.Redo();
        Assert.Equal(3, count);
    }

    private class TestCommand : IEditorCommand
    {
        public bool Executed { get; private set; }
        public bool Undone { get; private set; }
        public int ExecuteCount { get; private set; }
        public void Execute() { Executed = true; ExecuteCount++; }
        public void Undo() { Undone = true; }
    }
}
