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
        if (!_state.SkipMode && !_state.ForceSkipMode) return 0;

        _state.SkipTimerMs += deltaTimeMs;
        int rate = Math.Max(50, _state.SkipRateMs);
        if (_state.ForceSkipMode) rate = Math.Max(20, rate / 3); // Ctrl押し時は3倍速

        if (_state.SkipTimerMs < rate) return 0;
        _state.SkipTimerMs -= rate;

        return ProcessSingleSkip();
    }

    /// <summary>
    /// 1回分のスキップ処理（1msg分）
    /// </summary>
    private int ProcessSingleSkip()
    {
        int guard = 64;

        while ((_state.SkipMode || _state.ForceSkipMode) && _state.State != VmState.Ended && guard-- > 0)
        {
            if (_state.State == VmState.WaitingForClick)
            {
                if (!CanSkipCurrentWait()) return 0;

                CompleteCurrentText();

                if (_state.IsWaitingPageClear)
                {
                    _state.CurrentTextBuffer = "";
                    _state.DisplayedTextLength = 0;
                    _state.IsWaitingPageClear = false;
                }

                _vm.ResumeFromClick();
                return 1;
            }

            if (_state.State == VmState.WaitingForAnimation)
            {
                if (_state.TextSpeedMs > 0 && _state.DisplayedTextLength < _state.CurrentTextBuffer.Length)
                {
                    CompleteCurrentText();
                    _state.State = VmState.Running;
                    return 1;
                }
                // Tweenアニメーションを即完了
                _vm.FinishAllTweens();
                _state.State = VmState.Running;
                return 1;
            }

            if (_state.State == VmState.WaitingForDelay)
            {
                _state.DelayTimerMs = 0f;
                _state.State = VmState.Running;
                return 1;
            }

            if (_state.State == VmState.FadingIn || _state.State == VmState.FadingOut)
            {
                _state.IsFading = false;
                _state.FadeProgress = _state.State == VmState.FadingIn ? 1.0f : 0.0f;
                _state.State = VmState.Running;
                return 1;
            }

            if (_state.State == VmState.WaitingForButton)
            {
                // 選択肢に到達したらスキップを一時停止して選択肢を表示
                StopSkip();
                return 0;
            }

            if (_state.State == VmState.Running)
            {
                int beforePc = _state.ProgramCounter;
                _vm.Step();

                // プログラムカウンタが進まない場合、ループを停止
                if (_state.State == VmState.Running && _state.ProgramCounter == beforePc) return 0;
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
        return _state.ForceSkipMode || _state.SkipUnread || _state.CurrentInstructionWasRead;
    }

    /// <summary>
    /// スキップモードを停止する
    /// </summary>
    public void StopSkip()
    {
        _state.SkipMode = false;
        _state.ForceSkipMode = false;
        _state.SkipTimerMs = 0f;
    }

    /// <summary>
    /// スキップモードが有効かどうかを判定する
    /// </summary>
    public bool IsSkipModeActive => _state.SkipMode || _state.ForceSkipMode;

    /// <summary>
    /// 現在のテキストを完了させる
    /// </summary>
    private void CompleteCurrentText()
    {
        _state.DisplayedTextLength = _state.CurrentTextBuffer.Length;

        if (_state.TextTargetSpriteId >= 0 &&
            _state.Sprites.TryGetValue(_state.TextTargetSpriteId, out var textSprite) &&
            textSprite.Type == SpriteType.Text)
        {
            textSprite.Text = _state.CurrentTextBuffer;
        }
    }
}
