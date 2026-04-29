# オペコードリファレンス

AriaEngineで使用可能なすべてのスクリプトコマンド（オペコード）をカテゴリごとに整理したリファレンスです。

## カテゴリ概要

| カテゴリ | 説明 | 対象ファイル |
|---------|------|-------------|
| [Core](basic.md) | 基本演算、変数操作、ループ処理 | basic.md, script-control.md |
| [Script](script-control.md) | スクリプト制御、サブルーチン、フロー制御 | script-control.md |
| [Render](sprite.md) | スプライト表示・操作、装飾、エフェクト | sprite.md, animation.md |
| [Input](button.md) | ボタン作成、クリック判定、入力処理 | button.md |
| [Ui](ui.md) | UIコンポーネント、テキストボックス、レイアウト | ui.md |
| [Text](ui.md) | テキスト表示、書式設定、ページ制御 | ui.md |
| [Audio](audio.md) | BGM/SE再生、音量制御、フェード | audio.md |
| [System](system.md) | ウィンドウ、環境設定、右クリックメニュー | system.md |
| [Save](system.md) | セーブ/ロード機能 | system.md |
| [Flags](flag.md) | フラグ・カウンター管理 | flag.md |
| [Compatibility](init.md) | NScripter互換、チャプター/キャラクター操作 | init.md, chapter.md, character.md |

## ナビゲーション

### 基本コマンド
- [**基本演算・変数**](basic.md) — `mov`, `add`, `inc`, `rnd`, `for`, `while` など
- [**スクリプト制御**](script-control.md) — `gosub`, `return`, `defsub`, `if`, `goto` など

### スプライト・描画
- [**スプライト操作**](sprite.md) — `lsp`, `vsp`, `msp`, `sp_alpha` など
- [**アニメーション**](animation.md) — `amsp`, `afade`, `await`, `ease` など

### UI・テキスト
- [**UI・テキストウィンドウ**](ui.md) — `ui`, `textbox`, `choice`, `btnwait` など

### 入力
- [**ボタン・入力**](button.md) — `btn`, `btn_area`, `spbtn`, `btntime` など

### オーディオ
- [**オーディオ再生**](audio.md) — `play_bgm`, `play_se`, `bgmvol` など

### システム・セーブ
- [**システム・セーブ**](system.md) — `save`, `load`, `end`, `window` など

### フラグ管理
- [**フラグ・カウンター**](flag.md) — `set_flag`, `get_flag`, `inc_counter` など

### 互換性・拡張
- [**Init・互換性**](init.md) — `window`, `font`, `script`, `debug` など
- [**チャプター操作**](chapter.md) — `defchapter`, `chapter_select`, `unlock_chapter` など
- [**キャラクター操作**](character.md) — `char_load`, `char_show`, `char_move` など

## クイック検索テーブル

| コマンド名 | カテゴリ | ファイル |
|-----------|---------|---------|
| `add` | Core | [basic.md](basic.md) |
| `alias` | Script | [script-control.md](script-control.md) |
| `amsp` | Render | [animation.md](animation.md) |
| `ascale` | Render | [animation.md](animation.md) |
| `assert` | Core | [basic.md](basic.md) |
| `await` | Render | [animation.md](animation.md) |
| `bg` | Compatibility | [init.md](init.md) |
| `blt` | Core | [basic.md](basic.md) |
| `bne` | Core | [basic.md](basic.md) |
| `break` | Core | [basic.md](basic.md) |
| `btn` | Input | [button.md](button.md) |
| `btn_area` | Input | [button.md](button.md) |
| `btn_clear` | Input | [button.md](button.md) |
| `btn_wait` | Input | [button.md](button.md) |
| `btntime` | Input | [button.md](button.md) |
| `caption` | System | [system.md](system.md) |
| `change_scene` | Compatibility | [chapter.md](chapter.md) |
| `char_expression` | Compatibility | [character.md](character.md) |
| `char_hide` | Compatibility | [character.md](character.md) |
| `char_load` | Compatibility | [character.md](character.md) |
| `char_move` | Compatibility | [character.md](character.md) |
| `char_pose` | Compatibility | [character.md](character.md) |
| `char_scale` | Compatibility | [character.md](character.md) |
| `char_show` | Compatibility | [character.md](character.md) |
| `char_z` | Compatibility | [character.md](character.md) |
| `chapter_id` | Compatibility | [chapter.md](chapter.md) |
| `chapter_progress` | Compatibility | [chapter.md](chapter.md) |
| `chapter_scroll` | Compatibility | [chapter.md](chapter.md) |
| `chapter_select` | Compatibility | [chapter.md](chapter.md) |
| `chapter_thumbnail` | Compatibility | [chapter.md](chapter.md) |
| `chapter_title` | Compatibility | [chapter.md](chapter.md) |
| `choice` | Text | [ui.md](ui.md) |
| `choice_style` | Text | [ui.md](ui.md) |
| `clear_flag` | Flags | [flag.md](flag.md) |
| `clr` | Compatibility | [init.md](init.md) |
| `cmp` | Core | [basic.md](basic.md) |
| `continue` | Core | [basic.md](basic.md) |
| `csp` | Render | [sprite.md](sprite.md) |
| `debug` | System | [init.md](init.md) |
| `dec` | Core | [basic.md](basic.md) |
| `defer` | Core | [basic.md](basic.md) |
| `defchapter` | Compatibility | [chapter.md](chapter.md) |
| `defsub` | Script | [script-control.md](script-control.md) |
| `div` | Core | [basic.md](basic.md) |
| `dwave` | Audio | [audio.md](audio.md) |
| `dwave_loop` | Audio | [audio.md](audio.md) |
| `dwave_stop` | Audio | [audio.md](audio.md) |
| `ease` | Render | [animation.md](animation.md) |
| `effect` | Compatibility | [init.md](init.md) |
| `end` | System | [system.md](system.md) |
| `endchapter` | Compatibility | [chapter.md](chapter.md) |
| `fade_in` | Render | [sprite.md](sprite.md) |
| `fade_out` | Render | [sprite.md](sprite.md) |
| `font` | System | [init.md](init.md) |
| `font_atlas_size` | System | [init.md](init.md) |
| `font_filter` | Text | [ui.md](ui.md) |
| `font_size` | Text | [ui.md](ui.md) |
| `for` | Core | [basic.md](basic.md) |
| `get_counter` | Flags | [flag.md](flag.md) |
| `get_flag` | Flags | [flag.md](flag.md) |
| `get_scene_data` | Compatibility | [chapter.md](chapter.md) |
| `get_timer` | Core | [basic.md](basic.md) |
| `getconfig` | System | [system.md](system.md) |
| `gosub` | Script | [script-control.md](script-control.md) |
| `goto` | Core | [basic.md](basic.md) |
| `if` | Script | [script-control.md](script-control.md) |
| `inc` | Core | [basic.md](basic.md) |
| `inc_counter` | Flags | [flag.md](flag.md) |
| `include` | Script | [script-control.md](script-control.md) |
| `jmp` | Core | [basic.md](basic.md) |
| `load` | Save | [system.md](system.md) |
| `load_bg` | Compatibility | [init.md](init.md) |
| `load_ch` | Compatibility | [character.md](character.md) |
| `lsp` | Render | [sprite.md](sprite.md) |
| `lsp_rect` | Render | [sprite.md](sprite.md) |
| `lsp_text` | Render | [sprite.md](sprite.md) |
| `mesbox` | System | [system.md](system.md) |
| `mod` | Core | [basic.md](basic.md) |
| `mov` | Core | [basic.md](basic.md) |
| `mp3vol` | Audio | [audio.md](audio.md) |
| `msp` | Render | [sprite.md](sprite.md) |
| `msp_rel` | Render | [sprite.md](sprite.md) |
| `mul` | Core | [basic.md](basic.md) |
| `next` | Core | [basic.md](basic.md) |
| `numalias` | Script | [script-control.md](script-control.md) |
| `play_bgm` | Audio | [audio.md](audio.md) |
| `play_mp3` | Audio | [audio.md](audio.md) |
| `play_se` | Audio | [audio.md](audio.md) |
| `print` | Compatibility | [init.md](init.md) |
| `quake` | Render | [sprite.md](sprite.md) |
| `rnd` | Core | [basic.md](basic.md) |
| `return` | Script | [script-control.md](script-control.md) |
| `return_scene` | Compatibility | [chapter.md](chapter.md) |
| `returnvalue` | Script | [script-control.md](script-control.md) |
| `rmenu` | System | [system.md](system.md) |
| `save` | Save | [system.md](system.md) |
| `saveinfo` | Save | [system.md](system.md) |
| `saveconfig` | System | [system.md](system.md) |
| `saveoff` | Save | [system.md](system.md) |
| `saveon` | Save | [system.md](system.md) |
| `script` | System | [init.md](init.md) |
| `set_array` | Core | [basic.md](basic.md) |
| `set_config` | System | [system.md](system.md) |
| `set_counter` | Flags | [flag.md](flag.md) |
| `set_flag` | Flags | [flag.md](flag.md) |
| `set_scene_data` | Compatibility | [chapter.md](chapter.md) |
| `setwindow` | Text | [ui.md](ui.md) |
| `sp_alpha` | Render | [sprite.md](sprite.md) |
| `sp_border` | Render | [sprite.md](sprite.md) |
| `sp_btn` | Input | [button.md](button.md) |
| `sp_color` | Render | [sprite.md](sprite.md) |
| `sp_fill` | Render | [sprite.md](sprite.md) |
| `sp_fontsize` | Render | [sprite.md](sprite.md) |
| `sp_gradient` | Render | [sprite.md](sprite.md) |
| `sp_hover_color` | Render | [sprite.md](sprite.md) |
| `sp_hover_scale` | Render | [sprite.md](sprite.md) |
| `sp_rotation` | Render | [sprite.md](sprite.md) |
| `sp_scale` | Render | [sprite.md](sprite.md) |
| `sp_shadow` | Render | [sprite.md](sprite.md) |
| `sp_text_align` | Render | [sprite.md](sprite.md) |
| `sp_text_outline` | Render | [sprite.md](sprite.md) |
| `sp_text_shadow` | Render | [sprite.md](sprite.md) |
| `sp_text_valign` | Render | [sprite.md](sprite.md) |
| `sp_z` | Render | [sprite.md](sprite.md) |
| `stop_bgm` | Audio | [audio.md](audio.md) |
| `sub` | Core | [basic.md](basic.md) |
| `system_call` | System | [system.md](system.md) |
| `text` | Text | [ui.md](ui.md) |
| `textbox` | Text | [ui.md](ui.md) |
| `textbox_color` | Text | [ui.md](ui.md) |
| `textbox_hide` | Text | [ui.md](ui.md) |
| `textbox_show` | Text | [ui.md](ui.md) |
| `textbox_style` | Text | [ui.md](ui.md) |
| `textclear` | Text | [ui.md](ui.md) |
| `textcolor` | Text | [ui.md](ui.md) |
| `textspeed` | Text | [ui.md](ui.md) |
| `toggle_flag` | Flags | [flag.md](flag.md) |
| `ui` | Ui | [ui.md](ui.md) |
| `ui_anchor` | Ui | [ui.md](ui.md) |
| `ui_button` | Ui | [ui.md](ui.md) |
| `ui_checkbox` | Ui | [ui.md](ui.md) |
| `ui_group` | Ui | [ui.md](ui.md) |
| `ui_group_add` | Ui | [ui.md](ui.md) |
| `ui_group_clear` | Ui | [ui.md](ui.md) |
| `ui_group_hide` | Ui | [ui.md](ui.md) |
| `ui_group_show` | Ui | [ui.md](ui.md) |
| `ui_hotkey` | Ui | [ui.md](ui.md) |
| `ui_image` | Ui | [ui.md](ui.md) |
| `ui_layout` | Ui | [ui.md](ui.md) |
| `ui_motion` | Text | [ui.md](ui.md) |
| `ui_on` | Ui | [ui.md](ui.md) |
| `ui_pack` | Ui | [ui.md](ui.md) |
| `ui_quality` | Text | [ui.md](ui.md) |
| `ui_rect` | Ui | [ui.md](ui.md) |
| `ui_scale` | Ui | [ui.md](ui.md) |
| `ui_slider` | Ui | [ui.md](ui.md) |
| `ui_state` | Ui | [ui.md](ui.md) |
| `ui_state_style` | Ui | [ui.md](ui.md) |
| `ui_style` | Ui | [ui.md](ui.md) |
| `ui_text` | Ui | [ui.md](ui.md) |
| `ui_theme` | Text | [ui.md](ui.md) |
| `ui_tween` | Ui | [ui.md](ui.md) |
| `unlock_chapter` | Compatibility | [chapter.md](chapter.md) |
| `vsp` | Render | [sprite.md](sprite.md) |
| `wait` | Core | [basic.md](basic.md) |
| `waittimer` | Core | [basic.md](basic.md) |
| `wend` | Core | [basic.md](basic.md) |
| `while` | Core | [basic.md](basic.md) |
| `window` | System | [init.md](init.md) |
| `window_title` | System | [system.md](system.md) |

## 備考

- コマンド名は大文字小文字を区別しません
- エイリアスを持つコマンドは異なる名前でも同一の機能を呼び出します（例: `goto` と `jmp`）