using AriaEngine.Rendering;

namespace AriaEngine.Core.Commands;

public sealed class UiCommandHandler : BaseCommandHandler
{
    private static readonly HashSet<string> ReservedMenuActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "save",
        "load",
        "backlog",
        "lookback",
        "rmenu"
    };

    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Ui,
        OpCode.UiRect,
        OpCode.UiText,
        OpCode.UiImage,
        OpCode.UiButton,
        OpCode.UiGroup,
        OpCode.UiGroupAdd,
        OpCode.UiGroupClear,
        OpCode.UiGroupShow,
        OpCode.UiGroupHide,
        OpCode.UiLayout,
        OpCode.UiAnchor,
        OpCode.UiPack,
        OpCode.UiStyle,
        OpCode.UiState,
        OpCode.UiStateStyle,
        OpCode.UiOn,
        OpCode.UiHotkey,
        OpCode.UiTween,
        OpCode.UiFade,
        OpCode.UiMove,
        OpCode.UiScale,
        OpCode.UiSlider,
        OpCode.UiCheckbox
    };

    public UiCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.Ui:
                if (!ValidateArgs(inst, 2)) return true;
                ApplyTargetProperty(GetString(inst.Arguments[0]), GetString(inst.Arguments[1]), inst.Arguments.Skip(2).ToList(), inst);
                return true;

            case OpCode.UiRect:
                if (!ValidateArgs(inst, 5)) return true;
                State.Render.Sprites[GetVal(inst.Arguments[0])] = new Sprite
                {
                    Id = GetVal(inst.Arguments[0]),
                    Type = SpriteType.Rect,
                    X = GetVal(inst.Arguments[1]),
                    Y = GetVal(inst.Arguments[2]),
                    Width = GetVal(inst.Arguments[3]),
                    Height = GetVal(inst.Arguments[4]),
                    FillColor = State.ChoiceStyle.ChoiceBgColor,
                    FillAlpha = State.ChoiceStyle.ChoiceBgAlpha,
                    CornerRadius = State.ChoiceStyle.ChoiceCornerRadius,
                    BorderColor = State.ChoiceStyle.ChoiceBorderColor,
                    BorderWidth = State.ChoiceStyle.ChoiceBorderWidth,
                    BorderOpacity = State.ChoiceStyle.ChoiceBorderOpacity,
                    HoverFillColor = State.ChoiceStyle.ChoiceHoverColor
                };
                return true;

            case OpCode.UiText:
                if (!ValidateArgs(inst, 4)) return true;
                State.Render.Sprites[GetVal(inst.Arguments[0])] = new Sprite
                {
                    Id = GetVal(inst.Arguments[0]),
                    Type = SpriteType.Text,
                    Text = GetString(inst.Arguments[1]),
                    X = GetVal(inst.Arguments[2]),
                    Y = GetVal(inst.Arguments[3]),
                    FontSize = State.TextWindow.DefaultFontSize,
                    Color = State.TextWindow.DefaultTextColor,
                    Width = inst.Arguments.Count > 4 ? GetVal(inst.Arguments[4]) : 0,
                    Height = inst.Arguments.Count > 5 ? GetVal(inst.Arguments[5]) : 0
                };
                return true;

            case OpCode.UiImage:
                if (!ValidateArgs(inst, 4)) return true;
                State.Render.Sprites[GetVal(inst.Arguments[0])] = new Sprite
                {
                    Id = GetVal(inst.Arguments[0]),
                    Type = SpriteType.Image,
                    ImagePath = GetString(inst.Arguments[1]),
                    X = GetVal(inst.Arguments[2]),
                    Y = GetVal(inst.Arguments[3])
                };
                return true;

            case OpCode.UiButton:
                if (!ValidateArgs(inst, 2)) return true;
                SetButton(GetVal(inst.Arguments[0]), GetVal(inst.Arguments[1]));
                return true;

            case OpCode.UiGroup:
                if (!ValidateArgs(inst, 1)) return true;
                EnsureGroup(GetVal(inst.Arguments[0]));
                return true;

            case OpCode.UiGroupAdd:
                if (!ValidateArgs(inst, 2)) return true;
                AddToGroup(GetVal(inst.Arguments[0]), GetVal(inst.Arguments[1]));
                return true;

            case OpCode.UiGroupClear:
                if (!ValidateArgs(inst, 1)) return true;
                ClearGroup(GetVal(inst.Arguments[0]));
                return true;

            case OpCode.UiGroupShow:
                if (!ValidateArgs(inst, 1)) return true;
                SetGroupVisible(GetVal(inst.Arguments[0]), true);
                return true;

            case OpCode.UiGroupHide:
                if (!ValidateArgs(inst, 1)) return true;
                SetGroupVisible(GetVal(inst.Arguments[0]), false);
                return true;

            case OpCode.UiLayout:
                if (!ValidateArgs(inst, 2)) return true;
                State.UiComposition.Layouts[GetVal(inst.Arguments[0])] = GetString(inst.Arguments[1]).ToLowerInvariant();
                return true;

            case OpCode.UiAnchor:
                if (!ValidateArgs(inst, 2)) return true;
                State.UiComposition.Anchors[GetVal(inst.Arguments[0])] = GetString(inst.Arguments[1]).ToLowerInvariant();
                return true;

            case OpCode.UiPack:
                if (!ValidateArgs(inst, 1)) return true;
                PackGroup(GetVal(inst.Arguments[0]));
                return true;

            case OpCode.UiStyle:
                if (!ValidateArgs(inst, 2)) return true;
                ApplyStyle(GetVal(inst.Arguments[0]), GetString(inst.Arguments[1]));
                return true;

            case OpCode.UiState:
                if (!ValidateArgs(inst, 2)) return true;
                ApplyState(GetVal(inst.Arguments[0]), GetString(inst.Arguments[1]));
                return true;

            case OpCode.UiStateStyle:
                if (!ValidateArgs(inst, 4)) return true;
                ApplyStateStyle(GetVal(inst.Arguments[0]), GetString(inst.Arguments[1]), GetString(inst.Arguments[2]), inst.Arguments.Skip(3).ToList(), inst);
                return true;

            case OpCode.UiOn:
                if (!ValidateArgs(inst, 3)) return true;
                {
                    string eventName = GetString(inst.Arguments[1]).ToLowerInvariant();
                    if (eventName is not ("click" or "hover"))
                    {
                        Reporter.Report(new AriaError($"ui_on は event='{eventName}' をまだサポートしていません。click/hover を指定してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Error, "UI_EVENT_UNSUPPORTED"));
                        return true;
                    }
                    State.UiComposition.Events[$"{GetVal(inst.Arguments[0])}:{eventName}"] = inst.Arguments[2];
                }
                return true;

            case OpCode.UiHotkey:
                if (!ValidateArgs(inst, 2)) return true;
                State.UiComposition.Hotkeys[GetString(inst.Arguments[0]).ToLowerInvariant()] = inst.Arguments[1];
                return true;

            case OpCode.UiTween:
                if (!ValidateArgs(inst, 5)) return true;
                AnimateProperty(GetVal(inst.Arguments[0]), GetString(inst.Arguments[1]), GetFloat(inst.Arguments[2], inst), GetVal(inst.Arguments[3]), GetString(inst.Arguments[4]), inst);
                return true;

            case OpCode.UiFade:
                if (!ValidateArgs(inst, 3)) return true;
                AnimateProperty(GetVal(inst.Arguments[0]), "opacity", NormalizeOpacity(GetFloat(inst.Arguments[1], inst)), GetVal(inst.Arguments[2]), "out_cubic", inst);
                return true;

            case OpCode.UiMove:
                if (!ValidateArgs(inst, 4)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var move))
                {
                    int duration = GetVal(inst.Arguments[3]);
                    Tweens.Add(new Tween { SpriteId = move.Id, Property = TweenProperty.X, From = move.X, To = GetVal(inst.Arguments[1]), DurationMs = duration, Ease = EaseType.EaseOut });
                    Tweens.Add(new Tween { SpriteId = move.Id, Property = TweenProperty.Y, From = move.Y, To = GetVal(inst.Arguments[2]), DurationMs = duration, Ease = EaseType.EaseOut });
                }
                return true;

            case OpCode.UiScale:
                if (!ValidateArgs(inst, 4)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var scale))
                {
                    int duration = GetVal(inst.Arguments[3]);
                    Tweens.Add(new Tween { SpriteId = scale.Id, Property = TweenProperty.ScaleX, From = scale.ScaleX, To = GetFloat(inst.Arguments[1], inst), DurationMs = duration, Ease = EaseType.EaseOut });
                    Tweens.Add(new Tween { SpriteId = scale.Id, Property = TweenProperty.ScaleY, From = scale.ScaleY, To = GetFloat(inst.Arguments[2], inst), DurationMs = duration, Ease = EaseType.EaseOut });
                }
                return true;

            case OpCode.UiSlider:
                if (!ValidateArgs(inst, 7)) return true;
                CreateSlider(GetVal(inst.Arguments[0]), GetVal(inst.Arguments[1]), GetVal(inst.Arguments[2]), GetVal(inst.Arguments[3]), GetVal(inst.Arguments[4]), GetVal(inst.Arguments[5]), GetVal(inst.Arguments[6]));
                return true;

            case OpCode.UiCheckbox:
                if (!ValidateArgs(inst, 5)) return true;
                CreateCheckbox(GetVal(inst.Arguments[0]), GetVal(inst.Arguments[1]), GetVal(inst.Arguments[2]), GetString(inst.Arguments[3]), GetVal(inst.Arguments[4]) != 0);
                return true;

            default:
                return false;
        }
    }

    private void ApplyTargetProperty(string targetRaw, string propRaw, IReadOnlyList<string> values, Instruction inst)
    {
        string target = targetRaw.Trim().ToLowerInvariant();
        string prop = propRaw.Trim().ToLowerInvariant();
        if (values.Count == 0) return;

        if (target == "textbox")
        {
            ApplyTextboxProperty(prop, values, inst);
            return;
        }

        if (target == "choice")
        {
            ApplyChoiceProperty(prop, values, inst);
            return;
        }

        if (target == "text")
        {
            ApplyTextProperty(prop, values, inst);
            return;
        }

        if (target == "skip")
        {
            ApplySkipProperty(prop, values, inst);
            return;
        }

        if (target is "rmenu" or "save" or "load" or "backlog" or "settings")
        {
            ApplyMenuProperty(target, prop, values, inst);
            return;
        }

        if (target == "cursor")
        {
            ApplyCursorProperty(prop, values, inst);
            return;
        }

        if (target == "menu_action")
        {
            if (ReservedMenuActions.Contains(prop))
            {
                Reporter.Report(new AriaError(
                    $"ui menu_action の '{prop}' はエンジン標準UIの予約アクションのため上書きできません。",
                    inst.SourceLine,
                    CurrentScriptFile,
                    AriaErrorLevel.Warning,
                    "UI_MENU_ACTION_RESERVED",
                    hint: "save/load/backlog/rmenu はC#側の標準UIを使用します。settings/gallery/extra など作品固有画面だけを上書きしてください。"));
                return;
            }

            State.MenuRuntime.MenuActionOverrides[prop] = GetString(values[0]);
            return;
        }

        if (target.StartsWith("system.", StringComparison.Ordinal))
        {
            SetSystemButton(target["system.".Length..], IsTruthy(values[0]));
            return;
        }

        if (TryParseTargetId(target, "sprite:", out int spriteId) && State.Render.Sprites.TryGetValue(spriteId, out var sprite))
        {
            ApplySpriteProperty(sprite, prop, values, inst);
            return;
        }

        if (TryParseTargetId(target, "group:", out int groupId) && State.UiComposition.Groups.TryGetValue(groupId, out var children))
        {
            foreach (int id in children.ToArray())
            {
                if (State.Render.Sprites.TryGetValue(id, out var child)) ApplySpriteProperty(child, prop, values, inst);
            }
            return;
        }

        ReportUnsupportedTarget(target, prop, inst);
    }

    private void ApplyTextboxProperty(string prop, IReadOnlyList<string> values, Instruction inst)
    {
        switch (prop)
        {
            case "x": State.TextWindow.DefaultTextboxX = GetVal(values[0]); break;
            case "y": State.TextWindow.DefaultTextboxY = GetVal(values[0]); break;
            case "w":
            case "width": State.TextWindow.DefaultTextboxW = GetVal(values[0]); break;
            case "h":
            case "height": State.TextWindow.DefaultTextboxH = GetVal(values[0]); break;
            case "padding":
                State.TextWindow.DefaultTextboxPaddingX = GetVal(values[0]);
                State.TextWindow.DefaultTextboxPaddingY = values.Count > 1 ? GetVal(values[1]) : State.TextWindow.DefaultTextboxPaddingX;
                break;
            case "fill": State.TextWindow.DefaultTextboxBgColor = GetString(values[0]); break;
            case "fill_alpha": State.TextWindow.DefaultTextboxBgAlpha = GetVal(values[0]); break;
            case "text_color": State.TextWindow.DefaultTextColor = GetString(values[0]); break;
            case "font_size": State.TextWindow.DefaultFontSize = GetVal(values[0]); break;
            case "radius": State.TextWindow.DefaultTextboxCornerRadius = GetVal(values[0]); break;
            case "border": State.TextWindow.DefaultTextboxBorderWidth = GetVal(values[0]); if (values.Count > 1) State.TextWindow.DefaultTextboxBorderColor = GetString(values[1]); break;
            case "border_color": State.TextWindow.DefaultTextboxBorderColor = GetString(values[0]); break;
            case "border_alpha": State.TextWindow.DefaultTextboxBorderOpacity = GetVal(values[0]); break;
            case "shadow":
                State.TextWindow.DefaultTextboxShadowOffsetX = GetVal(values[0]);
                State.TextWindow.DefaultTextboxShadowOffsetY = values.Count > 1 ? GetVal(values[1]) : State.TextWindow.DefaultTextboxShadowOffsetY;
                if (values.Count > 2) State.TextWindow.DefaultTextboxShadowColor = GetString(values[2]);
                break;
            case "shadow_alpha": State.TextWindow.DefaultTextboxShadowAlpha = GetVal(values[0]); break;
            case "visible": State.TextWindow.TextboxVisible = IsTruthy(values[0]); break;
            default: ReportUnsupportedProperty("textbox", prop, inst); break;
        }
    }

    private void ApplyChoiceProperty(string prop, IReadOnlyList<string> values, Instruction inst)
    {
        switch (prop)
        {
            case "w":
            case "width": State.ChoiceStyle.ChoiceWidth = GetVal(values[0]); break;
            case "h":
            case "height": State.ChoiceStyle.ChoiceHeight = GetVal(values[0]); break;
            case "gap": State.ChoiceStyle.ChoiceSpacing = GetVal(values[0]); break;
            case "padding": State.ChoiceStyle.ChoicePaddingX = GetVal(values[0]); break;
            case "fill": State.ChoiceStyle.ChoiceBgColor = GetString(values[0]); break;
            case "fill_alpha": State.ChoiceStyle.ChoiceBgAlpha = GetVal(values[0]); break;
            case "text_color": State.ChoiceStyle.ChoiceTextColor = GetString(values[0]); break;
            case "font_size": State.ChoiceStyle.ChoiceFontSize = GetVal(values[0]); break;
            case "radius": State.ChoiceStyle.ChoiceCornerRadius = GetVal(values[0]); break;
            case "border": State.ChoiceStyle.ChoiceBorderWidth = GetVal(values[0]); if (values.Count > 1) State.ChoiceStyle.ChoiceBorderColor = GetString(values[1]); break;
            case "border_color": State.ChoiceStyle.ChoiceBorderColor = GetString(values[0]); break;
            case "border_alpha": State.ChoiceStyle.ChoiceBorderOpacity = GetVal(values[0]); break;
            case "hover_fill": State.ChoiceStyle.ChoiceHoverColor = GetString(values[0]); break;
            default: ReportUnsupportedProperty("choice", prop, inst); break;
        }
    }

    private void ApplyTextProperty(string prop, IReadOnlyList<string> values, Instruction inst)
    {
        switch (prop)
        {
            case "speed":
            case "text_speed":
            case "type_speed":
                State.TextRuntime.TextSpeedMs = Math.Max(0, GetVal(values[0]));
                Config.Config.GlobalTextSpeedMs = State.TextRuntime.TextSpeedMs;
                Config.Save();
                break;
            case "advance":
            case "advance_mode":
                SetTextAdvance(values, inst);
                break;
            case "advance_ratio":
                State.TextRuntime.TextAdvanceMode = "ratio";
                State.TextRuntime.TextAdvanceRatio = Math.Clamp(GetFloat(values[0], inst, 1f), 0f, 1f);
                break;
            case "shadow":
                State.TextRuntime.DefaultTextShadowX = GetVal(values[0]);
                State.TextRuntime.DefaultTextShadowY = values.Count > 1 ? GetVal(values[1]) : State.TextRuntime.DefaultTextShadowY;
                if (values.Count > 2) State.TextRuntime.DefaultTextShadowColor = GetString(values[2]);
                ApplyCurrentTextDecoration();
                break;
            case "shadow_color":
                State.TextRuntime.DefaultTextShadowColor = GetString(values[0]);
                ApplyCurrentTextDecoration();
                break;
            case "outline":
                State.TextRuntime.DefaultTextOutlineSize = GetVal(values[0]);
                if (values.Count > 1) State.TextRuntime.DefaultTextOutlineColor = GetString(values[1]);
                ApplyCurrentTextDecoration();
                break;
            case "outline_color":
                State.TextRuntime.DefaultTextOutlineColor = GetString(values[0]);
                ApplyCurrentTextDecoration();
                break;
            case "effect":
            case "text_effect":
                State.TextRuntime.DefaultTextEffect = GetString(values[0]).Trim().ToLowerInvariant();
                if (values.Count > 1) State.TextRuntime.DefaultTextEffectStrength = GetFloat(values[1], inst, State.TextRuntime.DefaultTextEffectStrength);
                if (values.Count > 2) State.TextRuntime.DefaultTextEffectSpeed = GetFloat(values[2], inst, State.TextRuntime.DefaultTextEffectSpeed);
                ApplyCurrentTextDecoration();
                break;
            case "effect_strength":
                State.TextRuntime.DefaultTextEffectStrength = GetFloat(values[0], inst, State.TextRuntime.DefaultTextEffectStrength);
                ApplyCurrentTextDecoration();
                break;
            case "effect_speed":
                State.TextRuntime.DefaultTextEffectSpeed = GetFloat(values[0], inst, State.TextRuntime.DefaultTextEffectSpeed);
                ApplyCurrentTextDecoration();
                break;
            default:
                ReportUnsupportedProperty("text", prop, inst);
                break;
        }
    }

    private void ApplySkipProperty(string prop, IReadOnlyList<string> values, Instruction inst)
    {
        switch (prop)
        {
            case "speed":
            case "steps":
            case "per_frame":
                State.Playback.SkipAdvancePerFrame = Math.Clamp(GetVal(values[0]), 1, 64);
                break;
            case "force_speed":
            case "force_steps":
            case "force_per_frame":
                State.Playback.ForceSkipAdvancePerFrame = Math.Clamp(GetVal(values[0]), 1, 128);
                break;
            default:
                ReportUnsupportedProperty("skip", prop, inst);
                break;
        }
    }

    private void ApplyMenuProperty(string target, string prop, IReadOnlyList<string> values, Instruction inst)
    {
        switch (prop)
        {
            case "w":
            case "width":
                if (target == "rmenu") State.MenuRuntime.RightMenuWidth = GetVal(values[0]);
                else if (target is "save" or "load") State.MenuRuntime.SaveLoadWidth = GetVal(values[0]);
                else if (target == "backlog") State.MenuRuntime.BacklogWidth = GetVal(values[0]);
                else if (target == "settings") State.MenuRuntime.SettingsWidth = GetVal(values[0]);
                break;
            case "columns":
                State.MenuRuntime.SaveLoadColumns = Math.Clamp(GetVal(values[0]), 1, 4);
                break;
            case "align": State.MenuRuntime.RightMenuAlign = GetString(values[0]).ToLowerInvariant(); break;
            case "fill": State.MenuRuntime.MenuFillColor = GetString(values[0]); break;
            case "fill_alpha": State.MenuRuntime.MenuFillAlpha = GetVal(values[0]); break;
            case "text_color": State.MenuRuntime.MenuTextColor = GetString(values[0]); break;
            case "border_color": State.MenuRuntime.MenuLineColor = GetString(values[0]); break;
            case "radius": State.MenuRuntime.MenuCornerRadius = GetVal(values[0]); break;
            default: ReportUnsupportedProperty(target, prop, inst); break;
        }
    }

    private void ApplyCursorProperty(string prop, IReadOnlyList<string> values, Instruction inst)
    {
        switch (prop)
        {
            case "size": State.UiRuntime.ClickCursorSize = Math.Clamp(GetFloat(values[0], inst), 6f, 48f); break;
            case "color":
            case "fill": State.UiRuntime.ClickCursorColor = GetString(values[0]); break;
            case "visible": State.UiRuntime.ShowClickCursor = IsTruthy(values[0]); break;
            case "mode":
                State.UiRuntime.ClickCursorMode = GetString(values[0]).ToLowerInvariant();
                if (State.UiRuntime.ClickCursorMode is "engine" or "builtin" or "default") State.UiRuntime.ClickCursorPath = "";
                break;
            default: ReportUnsupportedProperty("cursor", prop, inst); break;
        }
    }

    private void SetTextAdvance(IReadOnlyList<string> values, Instruction inst)
    {
        string mode = GetString(values[0]).Trim().ToLowerInvariant();
        if (mode is "any" or "immediate")
        {
            State.TextRuntime.TextAdvanceMode = "any";
            State.TextRuntime.TextAdvanceRatio = 0f;
        }
        else if (mode == "ratio")
        {
            State.TextRuntime.TextAdvanceMode = "ratio";
            State.TextRuntime.TextAdvanceRatio = values.Count > 1 ? Math.Clamp(GetFloat(values[1], inst, 1f), 0f, 1f) : State.TextRuntime.TextAdvanceRatio;
        }
        else
        {
            State.TextRuntime.TextAdvanceMode = "complete";
            State.TextRuntime.TextAdvanceRatio = 1f;
        }
    }

    private void ApplyCurrentTextDecoration()
    {
        if (State.TextWindow.TextTargetSpriteId < 0 ||
            !State.Render.Sprites.TryGetValue(State.TextWindow.TextTargetSpriteId, out var sp) ||
            sp.Type != SpriteType.Text)
        {
            return;
        }

        sp.TextShadowColor = State.TextRuntime.DefaultTextShadowColor;
        sp.TextShadowX = State.TextRuntime.DefaultTextShadowX;
        sp.TextShadowY = State.TextRuntime.DefaultTextShadowY;
        sp.TextOutlineColor = State.TextRuntime.DefaultTextOutlineColor;
        sp.TextOutlineSize = State.TextRuntime.DefaultTextOutlineSize;
        sp.TextEffect = State.TextRuntime.DefaultTextEffect;
        sp.TextEffectStrength = State.TextRuntime.DefaultTextEffectStrength;
        sp.TextEffectSpeed = State.TextRuntime.DefaultTextEffectSpeed;
    }

    private void ApplySpriteProperty(Sprite sp, string prop, IReadOnlyList<string> values, Instruction inst)
    {
        switch (prop)
        {
            case "x": sp.X = GetVal(values[0]); break;
            case "y": sp.Y = GetVal(values[0]); break;
            case "w":
            case "width": sp.Width = GetVal(values[0]); break;
            case "h":
            case "height": sp.Height = GetVal(values[0]); break;
            case "z": sp.Z = GetVal(values[0]); break;
            case "fill": sp.FillColor = GetString(values[0]); break;
            case "fill_alpha": sp.FillAlpha = GetVal(values[0]); break;
            case "text":
            case "content": sp.Text = GetString(values[0]); break;
            case "text_color":
            case "color": sp.Color = GetString(values[0]); break;
            case "font_size": sp.FontSize = GetVal(values[0]); break;
            case "align": { var s = GetString(values[0]); if (Enum.TryParse<TextAlignment>(s, ignoreCase: true, out var v)) sp.TextAlign = v; break; }
            case "valign": { var s = GetString(values[0]); if (Enum.TryParse<TextVerticalAlignment>(s, ignoreCase: true, out var v)) sp.TextVAlign = v; break; }
            case "radius": sp.CornerRadius = GetVal(values[0]); break;
            case "border": sp.BorderWidth = GetVal(values[0]); if (values.Count > 1) sp.BorderColor = GetString(values[1]); break;
            case "border_color": sp.BorderColor = GetString(values[0]); break;
            case "border_alpha": sp.BorderOpacity = GetVal(values[0]); break;
            case "shadow": sp.ShadowOffsetX = GetVal(values[0]); if (values.Count > 1) sp.ShadowOffsetY = GetVal(values[1]); if (values.Count > 2) sp.ShadowColor = GetString(values[2]); break;
            case "shadow_alpha": sp.ShadowAlpha = GetVal(values[0]); break;
            case "visible": sp.Visible = IsTruthy(values[0]); break;
            case "enabled": sp.IsButton = IsTruthy(values[0]); break;
            case "opacity": sp.Opacity = NormalizeOpacity(GetFloat(values[0], inst)); break;
            case "scale": sp.ScaleX = GetFloat(values[0], inst); sp.ScaleY = values.Count > 1 ? GetFloat(values[1], inst) : sp.ScaleX; break;
            case "hover_fill": sp.HoverFillColor = GetString(values[0]); break;
            case "hover_scale": sp.HoverScale = GetFloat(values[0], inst, 1f); break;
            case "text_shadow": sp.TextShadowX = GetVal(values[0]); if (values.Count > 1) sp.TextShadowY = GetVal(values[1]); if (values.Count > 2) sp.TextShadowColor = GetString(values[2]); break;
            case "text_outline": sp.TextOutlineSize = GetVal(values[0]); if (values.Count > 1) sp.TextOutlineColor = GetString(values[1]); break;
            case "text_effect": sp.TextEffect = GetString(values[0]).Trim().ToLowerInvariant(); if (values.Count > 1) sp.TextEffectStrength = GetFloat(values[1], inst, sp.TextEffectStrength); if (values.Count > 2) sp.TextEffectSpeed = GetFloat(values[2], inst, sp.TextEffectSpeed); break;
            case "button": SetButton(sp.Id, GetVal(values[0])); break;
            default: ReportUnsupportedProperty($"sprite:{sp.Id}", prop, inst); break;
        }
    }

    private void ApplyStyle(int id, string style)
    {
        if (!State.Render.Sprites.TryGetValue(id, out var sp)) return;
        switch (style.Trim().ToLowerInvariant())
        {
            case "mono":
                sp.FillColor = "#000000";
                sp.FillAlpha = 0;
                sp.BorderColor = "#ffffff";
                sp.BorderWidth = 1;
                sp.BorderOpacity = 120;
                sp.Color = "#ffffff";
                sp.HoverFillColor = "#151515";
                break;
            case "panel":
                sp.FillColor = State.MenuRuntime.MenuFillColor;
                sp.FillAlpha = State.MenuRuntime.MenuFillAlpha;
                sp.BorderColor = State.MenuRuntime.MenuLineColor;
                sp.BorderWidth = 1;
                sp.CornerRadius = State.MenuRuntime.MenuCornerRadius;
                break;
        }
    }

    private void ApplyState(int id, string state)
    {
        if (!State.Render.Sprites.TryGetValue(id, out var sp)) return;
        string normalized = state.Trim().ToLowerInvariant();
        if (normalized == "disabled")
        {
            sp.IsButton = false;
            sp.Opacity = 0.45f;
        }
        else if (normalized == "hidden")
        {
            sp.Visible = false;
        }
        else
        {
            sp.Visible = true;
            if (normalized is "normal" or "hover" or "pressed") sp.Opacity = 1f;
        }
    }

    private void ApplyStateStyle(int id, string stateRaw, string propRaw, IReadOnlyList<string> values, Instruction inst)
    {
        if (!State.Render.Sprites.TryGetValue(id, out var sp) || values.Count == 0) return;

        string state = stateRaw.Trim().ToLowerInvariant();
        string prop = propRaw.Trim().ToLowerInvariant();
        if (state == "normal")
        {
            ApplySpriteProperty(sp, prop, values, inst);
            return;
        }

        if (state == "hover")
        {
            switch (prop)
            {
                case "fill":
                case "hover_fill":
                    sp.HoverFillColor = GetString(values[0]);
                    return;
                case "scale":
                case "hover_scale":
                    sp.HoverScale = GetFloat(values[0], inst, 1f);
                    return;
                case "opacity":
                    sp.Opacity = NormalizeOpacity(GetFloat(values[0], inst));
                    return;
            }
        }

        if (state == "disabled")
        {
            if (prop == "opacity")
            {
                sp.Opacity = NormalizeOpacity(GetFloat(values[0], inst));
                return;
            }
            if (prop == "enabled")
            {
                sp.IsButton = IsTruthy(values[0]);
                return;
            }
        }

        Reporter.Report(new AriaError($"ui_state_style は state='{state}', prop='{prop}' の組み合わせをまだサポートしていません。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning, "UI_STATE_STYLE_UNSUPPORTED"));
    }

    private void AnimateProperty(int id, string property, float to, int durationMs, string easeName, Instruction inst)
    {
        if (!State.Render.Sprites.TryGetValue(id, out var sp)) return;
        if (!TryNormalizeTweenProperty(property, out var prop))
        {
            ReportUnsupportedProperty($"sprite:{id}", property, inst);
            return;
        }

        float from = prop switch
        {
            TweenProperty.X => sp.X,
            TweenProperty.Y => sp.Y,
            TweenProperty.ScaleX => sp.ScaleX,
            TweenProperty.ScaleY => sp.ScaleY,
            TweenProperty.Opacity => sp.Opacity,
            _ => 0f
        };
        if (prop == TweenProperty.Opacity) to = NormalizeOpacity(to);
        Tweens.Add(new Tween { SpriteId = id, Property = prop, From = from, To = to, DurationMs = durationMs, Ease = ParseEase(easeName) });
    }

    private void PackGroup(int groupId)
    {
        if (!State.UiComposition.Groups.TryGetValue(groupId, out var children) || children.Count == 0) return;
        string layout = State.UiComposition.Layouts.TryGetValue(groupId, out var value) ? value : "free";
        if (layout == "free") return;
        float x = State.Render.Sprites.TryGetValue(children[0], out var first) ? first.X : 0f;
        float y = State.Render.Sprites.TryGetValue(children[0], out first) ? first.Y : 0f;
        int gap = State.ChoiceStyle.ChoiceSpacing;
        foreach (int id in children)
        {
            if (!State.Render.Sprites.TryGetValue(id, out var sp)) continue;
            sp.X = x;
            sp.Y = y;
            if (layout == "row") x += Math.Max(sp.Width, 1) + gap;
            if (layout == "column") y += Math.Max(sp.Height, State.ChoiceStyle.ChoiceHeight) + gap;
        }
    }

    private void EnsureGroup(int groupId)
    {
        if (!State.UiComposition.Groups.ContainsKey(groupId)) State.UiComposition.Groups[groupId] = new List<int>();
    }

    private void AddToGroup(int groupId, int childId)
    {
        EnsureGroup(groupId);
        if (!State.UiComposition.Groups[groupId].Contains(childId)) State.UiComposition.Groups[groupId].Add(childId);
    }

    private void ClearGroup(int groupId)
    {
        if (!State.UiComposition.Groups.TryGetValue(groupId, out var children)) return;
        foreach (int id in children.ToArray())
        {
            State.Render.Sprites.Remove(id);
            State.Interaction.SpriteButtonMap.Remove(id);
        }
        children.Clear();
    }

    private void SetGroupVisible(int groupId, bool visible)
    {
        if (!State.UiComposition.Groups.TryGetValue(groupId, out var children)) return;
        foreach (int id in children)
        {
            if (State.Render.Sprites.TryGetValue(id, out var sprite)) sprite.Visible = visible;
        }
    }

    private void SetButton(int spriteId, int result)
    {
        if (State.Render.Sprites.TryGetValue(spriteId, out var sprite)) sprite.IsButton = true;
        State.Interaction.SpriteButtonMap[spriteId] = result;
    }

    private bool TryParseTargetId(string target, string prefix, out int id)
    {
        id = 0;
        if (!target.StartsWith(prefix, StringComparison.Ordinal)) return false;
        string value = target[prefix.Length..];
        if (value.StartsWith("%", StringComparison.Ordinal))
        {
            id = GetVal(value);
            return true;
        }
        return int.TryParse(value, out id);
    }

    private bool IsTruthy(string token)
    {
        string value = GetString(token).Trim().ToLowerInvariant();
        return value is "on" or "true" or "1" or "yes" or "show";
    }

    private static float NormalizeOpacity(float value) => value > 1f ? Math.Clamp(value / 255f, 0f, 1f) : Math.Clamp(value, 0f, 1f);

    private static bool TryNormalizeTweenProperty(string property, out TweenProperty result)
    {
        result = property.Trim().ToLowerInvariant() switch
        {
            "scale_x" or "scalex" => TweenProperty.ScaleX,
            "scale_y" or "scaley" => TweenProperty.ScaleY,
            "alpha" => TweenProperty.Opacity,
            "x" => TweenProperty.X,
            "y" => TweenProperty.Y,
            "opacity" => TweenProperty.Opacity,
            _ => TweenProperty.X
        };
        return property.Trim().ToLowerInvariant() is "scale_x" or "scalex" or "scale_y" or "scaley" or "alpha" or "x" or "y" or "opacity";
    }

    private void ReportUnsupportedTarget(string target, string prop, Instruction inst)
    {
        Reporter.Report(new AriaError(
            $"ui target='{target}', prop='{prop}' はサポートされていません。",
            inst.SourceLine,
            CurrentScriptFile,
            AriaErrorLevel.Warning,
            "UI_PROPERTY_UNSUPPORTED"));
    }

    private void ReportUnsupportedProperty(string target, string prop, Instruction inst)
    {
        Reporter.Report(new AriaError(
            $"ui target='{target}', prop='{prop}' はサポートされていません。",
            inst.SourceLine,
            CurrentScriptFile,
            AriaErrorLevel.Warning,
            "UI_PROPERTY_UNSUPPORTED"));
    }

    private static EaseType ParseEase(string easeName)
    {
        string value = easeName.Trim().ToLowerInvariant().Replace("_", "");
        return value switch
        {
            "in" or "easein" => EaseType.EaseIn,
            "out" or "easeout" or "outcubic" => EaseType.EaseOut,
            "inout" or "easeinout" => EaseType.EaseInOut,
            _ => EaseType.Linear
        };
    }

    private void CreateSlider(int id, int x, int y, int width, int min, int max, int value)
    {
        int trackH = 6;
        int thumbR = 8;
        int height = thumbR * 2 + 4;

        State.Render.Sprites[id] = new Sprite
        {
            Id = id,
            Type = SpriteType.Rect,
            X = x,
            Y = y + (height - trackH) / 2,
            Width = width,
            Height = trackH,
            FillColor = "#444444",
            FillAlpha = 220,
            CornerRadius = trackH / 2,
            IsButton = true,
            SliderMin = min,
            SliderMax = max,
            SliderValue = Math.Clamp(value, min, max)
        };
        State.Interaction.SpriteButtonMap[id] = id;

        int fillId = id + 1;
        float ratio = max > min ? (float)(Math.Clamp(value, min, max) - min) / (max - min) : 0f;
        State.Render.Sprites[fillId] = new Sprite
        {
            Id = fillId,
            Type = SpriteType.Rect,
            X = x,
            Y = y + (height - trackH) / 2,
            Width = (int)(width * ratio),
            Height = trackH,
            FillColor = "#f5f5f5",
            FillAlpha = 200,
            CornerRadius = trackH / 2
        };

        int thumbId = id + 2;
        int thumbX = x + (int)(width * ratio) - thumbR;
        State.Render.Sprites[thumbId] = new Sprite
        {
            Id = thumbId,
            Type = SpriteType.Rect,
            X = thumbX,
            Y = y + height / 2 - thumbR,
            Width = thumbR * 2,
            Height = thumbR * 2,
            FillColor = "#ffffff",
            FillAlpha = 255,
            CornerRadius = thumbR
        };

        int valueId = id + 3;
        State.Render.Sprites[valueId] = new Sprite
        {
            Id = valueId,
            Type = SpriteType.Text,
            X = x + width + 12,
            Y = y + 2,
            Width = 60,
            Height = 20,
            Text = value.ToString(),
            FontSize = 14,
            Color = "#cccccc"
        };
    }

    private void CreateCheckbox(int id, int x, int y, string label, bool value)
    {
        int boxSize = 18;
        State.Render.Sprites[id] = new Sprite
        {
            Id = id,
            Type = SpriteType.Rect,
            X = x,
            Y = y,
            Width = boxSize,
            Height = boxSize,
            FillColor = value ? "#f5f5f5" : "#000000",
            FillAlpha = value ? 255 : 0,
            BorderColor = "#f5f5f5",
            BorderWidth = 1,
            BorderOpacity = 180,
            CornerRadius = 3,
            IsButton = true,
            CheckboxValue = value,
            CheckboxLabel = label
        };
        State.Interaction.SpriteButtonMap[id] = id;

        int checkId = id + 1;
        State.Render.Sprites[checkId] = new Sprite
        {
            Id = checkId,
            Type = SpriteType.Text,
            X = x + 2,
            Y = y - 2,
            Width = boxSize,
            Height = boxSize,
            Text = value ? "v" : "",
            FontSize = 14,
            Color = "#000000",
            TextAlign = TextAlignment.Center,
            TextVAlign = TextVerticalAlignment.Center
        };

        int labelId = id + 2;
        State.Render.Sprites[labelId] = new Sprite
        {
            Id = labelId,
            Type = SpriteType.Text,
            X = x + boxSize + 10,
            Y = y,
            Width = 200,
            Height = boxSize,
            Text = label,
            FontSize = 16,
            Color = "#f5f5f5"
        };
    }
}
