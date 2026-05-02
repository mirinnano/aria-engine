namespace AriaEngine.Core;

/// <summary>
/// スキップモードを管理するクラス
/// </summary>
public class SkipModeManager
{
    private readonly GameState _state;
    private readonly VirtualMachine _vm;

    public SkipModeManager(GameState state, VirtualMachine vm)
    {
        _state = state;
        _vm = vm;
    }

    /// <summary>
    /// スキップモードで1フレーム分処理を進める（時間ベース・1msg/呼び出し）
    /// </summary>
    /// <param name="deltaTimeMs">前フレームからの経過時間（ms）</param>
    /// <returns>スキップした待機数（0 or 1）</returns>
    public int ProcessSkipFrame(float deltaTimeMs)
    {
        if (!_state.Playback.SkipMode && !_state.Playback.ForceSkipMode) return 0;

        _state.Playback.SkipTimerMs += deltaTimeMs;
        int rate = Math.Max(50, _state.Playback.SkipRateMs);
        if (_state.Playback.ForceSkipMode) rate = Math.Max(20, rate / 3); // Ctrl押し時は3倍速

        if (_state.Playback.SkipTimerMs < rate) return 0;
        _state.Playback.SkipTimerMs -= rate;

        return ProcessSingleSkip();
    }

    /// <summary>
    /// 1回分のスキップ処理（1msg分）
    /// </summary>
    private int ProcessSingleSkip()
    {
        int guard = 64;

        while ((_state.Playback.SkipMode || _state.Playback.ForceSkipMode) && _state.Execution.State != VmState.Ended && guard-- > 0)
        {
            if (_state.Execution.State == VmState.WaitingForClick)
            {
                if (!CanSkipCurrentWait()) return 0;

                CompleteCurrentText();

                if (_state.TextRuntime.IsWaitingPageClear)
                {
                    _state.TextRuntime.CurrentTextBuffer = "";
                    _state.TextRuntime.DisplayedTextLength = 0;
                    _state.TextRuntime.IsWaitingPageClear = false;
                }

                _vm.ResumeFromClick();
                return 1;
            }

            if (_state.Execution.State == VmState.WaitingForAnimation)
            {
                if (_state.TextRuntime.TextSpeedMs > 0 && _state.TextRuntime.DisplayedTextLength < _state.TextRuntime.CurrentTextBuffer.Length)
                {
                    CompleteCurrentText();
                    _state.Execution.State = VmState.Running;
                    return 1;
                }
                // Tweenアニメーションを即完了
                _vm.FinishAllTweens();
                _state.Execution.State = VmState.Running;
                return 1;
            }

            if (_state.Execution.State == VmState.WaitingForDelay)
            {
                _state.Execution.DelayTimerMs = 0f;
                _state.Execution.State = VmState.Running;
                return 1;
            }

            if (_state.Execution.State == VmState.FadingIn || _state.Execution.State == VmState.FadingOut)
            {
                _state.Render.IsFading = false;
                _state.Render.TransitionStyle = TransitionType.Fade;
                _state.Render.FadeProgress = _state.Execution.State == VmState.FadingIn ? 1.0f : 0.0f;
                _state.Execution.State = VmState.Running;
                return 1;
            }

            if (_state.Execution.State == VmState.WaitingForButton)
            {
                // 選択肢に到達したらスキップを一時停止して選択肢を表示
                StopSkip();
                return 0;
            }

            if (_state.Execution.State == VmState.Running)
            {
                int beforePc = _state.Execution.ProgramCounter;
                _vm.Step();

                // プログラムカウンタが進まない場合、ループを停止
                if (_state.Execution.State == VmState.Running && _state.Execution.ProgramCounter == beforePc) return 0;
                continue;
            }

            return 0;
        }

        return 0;
    }

    /// <summary>
    /// 現在の待機をスキップできるか判定する
    /// </summary>
    public bool CanSkipCurrentWait()
    {
        return _state.Playback.ForceSkipMode || _state.Playback.SkipUnread || _state.TextRuntime.CurrentInstructionWasRead;
    }

    /// <summary>
    /// スキップモードを停止する
    /// </summary>
    public void StopSkip()
    {
        _state.Playback.SkipMode = false;
        _state.Playback.ForceSkipMode = false;
        _state.Playback.SkipTimerMs = 0f;
    }

    /// <summary>
    /// スキップモードが有効かどうかを判定する
    /// </summary>
    public bool IsSkipModeActive => _state.Playback.SkipMode || _state.Playback.ForceSkipMode;

    /// <summary>
    /// 現在のテキストを完了させる
    /// </summary>
    private void CompleteCurrentText()
    {
        _state.TextRuntime.DisplayedTextLength = _state.TextRuntime.CurrentTextBuffer.Length;

        if (_state.TextWindow.TextTargetSpriteId >= 0 &&
            _state.Render.Sprites.TryGetValue(_state.TextWindow.TextTargetSpriteId, out var textSprite) &&
            textSprite.Type == SpriteType.Text)
        {
            textSprite.Text = _state.TextRuntime.CurrentTextBuffer;
        }
    }
}
