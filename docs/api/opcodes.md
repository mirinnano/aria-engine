# Opcode API Index

この索引は `src/AriaEngine/Core/CommandRegistry.cs` を一次ソースにした、v1.0.0時点のコマンド登録状況です。

## Current Inventory

| 項目 | 件数 |
| --- | ---: |
| Script-visible canonical commands | 240 |
| Script-visible token names including aliases | 253 |
| Registered executable opcodes | 239 |
| Internal parser opcodes | 3 |

Internal parser opcodes are `Text`, `JumpIfFalse`, and `Let`。`Text` は会話行パーサー、`JumpIfFalse` は制御構文展開、`Let` は `let x = y` 形式の内部表現に使われます。

## Authoritative Sources

| Source | Role |
| --- | --- |
| `src/AriaEngine/Core/OpCode.cs` | enum definition |
| `src/AriaEngine/Core/CommandRegistry.cs` | command names, aliases, categories, minimum argument counts |
| `src/AriaEngine/Core/Commands/*CommandHandler.cs` | runtime behavior |
| `docs/reference/opcodes/` | author-facing command details |

## Category Index

### Audio (15)
`bgmfade` (BgmFade), `bgmvol` (BgmVol), `dwave` (Dwave), `dwaveloop` (DwaveLoop), `dwavestop` (DwaveStop), `mp3fadeout` (Mp3FadeOut), `mp3vol` (Mp3Vol), `play_bgm` (PlayBgm; alias: bgm), `play_mp3` (PlayMp3; alias: mp3loop), `play_se` (PlaySe), `sevol` (SeVol), `stop_bgm` (StopBgm), `voice` (Voice), `voice_stop` (VoiceStop), `voice_wait` (VoiceWait)

### Compatibility (33)
`bg` (Bg), `change_scene` (ChangeScene), `chapter_card` (ChapterCard), `chapter_desc` (ChapterDesc), `chapter_id` (ChapterId), `chapter_progress` (ChapterProgress), `chapter_script` (ChapterScript), `chapter_scroll` (ChapterScroll), `chapter_select` (ChapterSelect), `chapter_thumbnail` (ChapterThumbnail), `chapter_title` (ChapterTitle), `char_expression` (CharExpression), `char_hide` (CharHide), `char_load` (CharLoad), `char_move` (CharMove), `char_pose` (CharPose), `char_scale` (CharScale), `char_show` (CharShow), `char_z` (CharZ), `clr` (Clr), `compat_mode` (CompatMode), `defchapter` (DefChapter), `effect` (Effect), `endchapter` (EndChapter), `get_scene_data` (GetSceneData), `hide_ch` (HideCh), `load_bg` (LoadBg), `load_ch` (LoadCh), `print` (Print), `return_scene` (ReturnScene), `set_scene_data` (SetSceneData), `show_ch` (ShowCh), `unlock_chapter` (UnlockChapter)

### Core (32)
`add` (Add), `assert` (Assert), `beq` (Beq), `bgt` (Bgt), `blt` (Blt), `bne` (Bne), `break` (Break), `cmp` (Cmp), `continue` (Continue), `dec` (Dec), `defer` (Defer), `delay` (Wait), `div` (Div), `for` (For), `getarray` (GetArray), `gettimer` (GetTimer), `inc` (Inc), `jmp` (Jmp; alias: goto), `mod` (Mod), `mov` (Mov; alias: let), `mul` (Mul), `next` (Next), `panic` (Panic), `resettimer` (ResetTimer), `rnd` (Rnd), `setarray` (SetArray), `sub` (Sub), `throw` (Throw), `wait` (Wait), `waittimer` (WaitTimer), `wend` (Wend), `while` (While)

### Flags (20)
`clear_flag` (ClearFlag), `clear_pflag` (ClearPFlag), `clear_sflag` (ClearSFlag), `clear_vflag` (ClearVFlag), `dec_counter` (DecCounter), `get_counter` (GetCounter), `get_flag` (GetFlag), `get_pflag` (GetPFlag), `get_sflag` (GetSFlag), `get_vflag` (GetVFlag), `inc_counter` (IncCounter), `set_counter` (SetCounter), `set_flag` (SetFlag), `set_pflag` (SetPFlag), `set_sflag` (SetSFlag), `set_vflag` (SetVFlag), `toggle_flag` (ToggleFlag), `toggle_pflag` (TogglePFlag), `toggle_sflag` (ToggleSFlag), `toggle_vflag` (ToggleVFlag)

### Input (8)
`btn` (Btn), `btn_area` (BtnArea), `btn_clear` (BtnClear), `btn_clear_all` (BtnClearAll; alias: btndef), `btntime` (BtnTime), `btnwait` (BtnWait; alias: textbtnwait), `rmenu` (RightMenu), `spbtn` (SpBtn)

### Render (42)
`acolor` (Acolor), `afade` (Afade), `amsp` (Amsp), `ascale` (Ascale), `await` (Await), `bgfade` (BgFade; alias: bg_fade), `bgtime` (BgTime; alias: bg_time), `bgtime_map` (BgTimeMap; alias: bg_time_map), `camera` (Camera), `csp` (Csp), `ease` (Ease), `fade_in` (FadeIn), `fade_out` (FadeOut), `fx` (Fx), `lsp` (Lsp), `lsp_rect` (LspRect), `lsp_text` (LspText), `msp` (Msp), `msp_rel` (MspRel), `quake` (Quake; alias: quakex), `screen` (Screen), `sp_alpha` (SpAlpha), `sp_border` (SpBorder), `sp_color` (SpColor), `sp_cursor` (SpCursor), `sp_fill` (SpFill), `sp_fontsize` (SpFontsize), `sp_gradient` (SpGradient), `sp_hover_color` (SpHoverColor), `sp_hover_scale` (SpHoverScale), `sp_rotation` (SpRotation), `sp_round` (SpRound), `sp_scale` (SpScale), `sp_shadow` (SpShadow), `sp_text_align` (SpTextAlign), `sp_text_outline` (SpTextOutline), `sp_text_shadow` (SpTextShadow), `sp_text_valign` (SpTextVAlign), `sp_z` (SpZ), `sync` (Sync), `transition` (Transition), `vsp` (Vsp)

### Save (5)
`load` (Load), `save` (Save), `saveinfo` (SaveInfo), `saveoff` (SaveOff), `saveon` (SaveOn)

### Script (8)
`defsub` (Defsub), `getparam` (Getparam), `gosub` (Gosub; alias: call), `include` (Include), `numalias` (Alias; alias: alias), `return` (Return; alias: ret), `returnvalue` (ReturnValue), `script` (Script; alias: include)

### System (21)
`automode_time` (AutoModeTime), `backlog_count` (BacklogCount), `backlog_entry` (BacklogEntry), `caption` (Caption), `cgunlock` (CgUnlock), `debug` (Debug), `end` (End), `end_scope` (ScopeExit), `gallery_count` (GalleryCount), `gallery_entry` (GalleryEntry), `gallery_info` (GalleryInfo), `getconfig` (GetConfig), `mesbox` (MesBox), `saveconfig` (SaveConfig), `scope` (ScopeEnter), `setconfig` (SetConfig), `system_button` (SystemButton), `systemcall` (SystemCall), `window` (Window), `window_title` (WindowTitle), `yesnobox` (YesNoBox)

### Text (32)
`@` (WaitClick), `\` (WaitClickClear), `backlog` (Backlog), `br` (Br), `choice` (Choice), `choice_style` (ChoiceStyle), `clickcursor` (ClickCursor), `defaultspeed` (DefaultSpeed), `erasetextwindow` (EraseTextWindow), `font` (Font), `font_atlas_size` (FontAtlasSize), `font_filter` (FontFilter), `fontsize` (Fontsize), `kidokumode` (KidokuMode), `lookback_off` (LookbackOff), `lookback_on` (LookbackOn), `setwindow` (SetWindow), `skipmode` (SkipMode), `text_target` (TextTarget), `textbox` (Textbox), `textbox_color` (TextboxColor), `textbox_hide` (TextboxHide), `textbox_show` (TextboxShow), `textbox_style` (TextboxStyle), `textclear` (TextClear), `textcolor` (Textcolor), `textfx` (TextFx), `textmode` (TextMode), `textspeed` (TextSpeed), `ui_motion` (UiMotion), `ui_quality` (UiQuality), `ui_theme` (UiTheme)

### Ui (24)
`ui` (Ui), `ui_anchor` (UiAnchor), `ui_button` (UiButton), `ui_checkbox` (UiCheckbox), `ui_fade` (UiFade), `ui_group` (UiGroup), `ui_group_add` (UiGroupAdd), `ui_group_clear` (UiGroupClear), `ui_group_hide` (UiGroupHide), `ui_group_show` (UiGroupShow), `ui_hotkey` (UiHotkey), `ui_image` (UiImage), `ui_layout` (UiLayout), `ui_move` (UiMove), `ui_on` (UiOn), `ui_pack` (UiPack), `ui_rect` (UiRect), `ui_scale` (UiScale), `ui_slider` (UiSlider), `ui_state` (UiState), `ui_state_style` (UiStateStyle), `ui_style` (UiStyle), `ui_text` (UiText), `ui_tween` (UiTween)
