using ShinCapture.Models;

namespace ShinCapture.Views;

internal sealed class CaptureSessionGate
{
    private long _nextSessionId;
    private CaptureSession? _activeSession;

    internal CaptureSession? ActiveSession => _activeSession;

    internal bool TryBegin(CaptureMode mode, bool editorAutoTranslate, out CaptureSession? session)
    {
        if (_activeSession != null)
        {
            session = null;
            return false;
        }

        session = new CaptureSession(++_nextSessionId, mode, editorAutoTranslate);
        _activeSession = session;
        return true;
    }

    internal bool Complete(CaptureSession session)
    {
        if (!ReferenceEquals(_activeSession, session))
            return false;

        _activeSession = null;
        return true;
    }

    internal CaptureSession? Reset()
    {
        CaptureSession? activeSession = _activeSession;
        _activeSession = null;
        return activeSession;
    }
}

internal sealed record CaptureSession(long Id, CaptureMode Mode, bool EditorAutoTranslate);
