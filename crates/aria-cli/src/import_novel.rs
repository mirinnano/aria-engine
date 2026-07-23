//! Faithful Markdown chapter import for prose-first visual-novel projects.
//!
//! This command deliberately does not invent dialogue, backgrounds, or scene
//! direction. It turns each authored, non-empty Markdown line into one Aria
//! reading beat followed by an explicit advance. That makes the source text
//! the authority while keeping pacing deterministic and reviewable.

use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result, bail};
use aria_core::modern::parse as parse_modern;
use atomic_write_file::AtomicWriteFile;
use serde::Serialize;

const BACKGROUND_TONES: [&str; 9] = [
    "#102b38", // tide
    "#284b59", // rooftop
    "#1f3b4d", // platform
    "#3d4655", // photograph
    "#244e5a", // shore
    "#394857", // rain
    "#17253b", // night
    "#315565", // wind
    "#6d6b57", // autumn
];

// An attribution is metadata only when the author used an explicit character
// label. Keeping this finite list intentionally conservative prevents prose
// such as '小さく「…」' or '俺は「…」' from losing its authored prefix.
const EXPLICIT_SPEAKERS: [&str; 8] = [
    "俺",
    "ミオ",
    "老婆",
    "フロント",
    "駅員",
    "親父",
    "管理人",
    "店員",
];

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct NovelImportReport {
    pub source_directory: String,
    pub output: String,
    pub chapters: Vec<NovelChapterReport>,
    pub reading_beats: usize,
    pub structural_breaks: usize,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct NovelChapterReport {
    pub source: String,
    pub scene: String,
    pub label: String,
    pub reading_beats: usize,
}

#[derive(Debug, Clone, PartialEq, Eq)]
struct NovelChapter {
    source_name: String,
    scene: String,
    chapter_id: String,
    label: String,
    beats: Vec<NovelBeat>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
enum NovelBeat {
    Reading {
        speaker: Option<String>,
        text: String,
    },
    StructuralBreak,
}

/// Executes the command-line form of the importer.
pub fn command(source: &Path, out: &Path, chapter_select: &str, locale: &str) -> Result<u8> {
    let report = import_novel(source, out, chapter_select, locale)?;
    println!("{}", serde_json::to_string_pretty(&report)?);
    Ok(0)
}

/// Imports a canonical Markdown source directory into a standalone Aria
/// library source. The generated module is intentionally source-only: callers
/// import it from their own entry module and retain ownership of title/setup.
pub fn import_novel(
    source: &Path,
    out: &Path,
    chapter_select: &str,
    locale: &str,
) -> Result<NovelImportReport> {
    if !is_aria_identifier(chapter_select) {
        bail!("chapter selector scene must be an Aria identifier: '{chapter_select}'");
    }
    if locale.trim().is_empty() {
        bail!("locale must not be empty");
    }

    let source_directory = source.canonicalize().with_context(|| {
        format!(
            "cannot resolve Markdown source directory {}",
            source.display()
        )
    })?;
    if !source_directory.is_dir() {
        bail!(
            "Markdown source is not a directory: {}",
            source_directory.display()
        );
    }

    let chapters = discover_chapters(&source_directory)?;
    if chapters.is_empty() {
        bail!(
            "no Markdown chapter files found in {}",
            source_directory.display()
        );
    }

    let generated = render_module(&chapters, chapter_select, locale);
    let parsed = parse_modern(out.to_string_lossy(), &generated);
    if parsed.has_errors() {
        let diagnostics = parsed
            .diagnostics
            .iter()
            .map(|diagnostic| diagnostic.message.as_str())
            .collect::<Vec<_>>()
            .join("; ");
        bail!("generated Aria source did not parse: {diagnostics}");
    }

    write_atomic(out, generated.as_bytes())?;

    let reading_beats = chapters
        .iter()
        .map(|chapter| {
            chapter
                .beats
                .iter()
                .filter(|beat| matches!(beat, NovelBeat::Reading { .. }))
                .count()
        })
        .sum();
    let structural_breaks = chapters
        .iter()
        .flat_map(|chapter| &chapter.beats)
        .filter(|beat| matches!(beat, NovelBeat::StructuralBreak))
        .count();

    Ok(NovelImportReport {
        source_directory: source_directory.display().to_string(),
        output: out.display().to_string(),
        chapters: chapters
            .iter()
            .map(|chapter| NovelChapterReport {
                source: chapter.source_name.clone(),
                scene: chapter.scene.clone(),
                label: chapter.label.clone(),
                reading_beats: chapter
                    .beats
                    .iter()
                    .filter(|beat| matches!(beat, NovelBeat::Reading { .. }))
                    .count(),
            })
            .collect(),
        reading_beats,
        structural_breaks,
    })
}

fn discover_chapters(source_directory: &Path) -> Result<Vec<NovelChapter>> {
    let mut paths = fs::read_dir(source_directory)
        .with_context(|| format!("cannot list {}", source_directory.display()))?
        .map(|entry| entry.map(|entry| entry.path()))
        .collect::<std::io::Result<Vec<_>>>()?;
    paths.retain(|path| {
        path.is_file()
            && path
                .extension()
                .is_some_and(|extension| extension.eq_ignore_ascii_case("md"))
    });
    paths.sort_by(|left, right| {
        left.file_name()
            .unwrap_or_default()
            .cmp(right.file_name().unwrap_or_default())
    });

    paths
        .into_iter()
        .enumerate()
        .map(|(index, source_path)| parse_chapter(index, source_path))
        .collect()
}

fn parse_chapter(index: usize, source_path: PathBuf) -> Result<NovelChapter> {
    let source = fs::read_to_string(&source_path)
        .with_context(|| format!("cannot read authored chapter {}", source_path.display()))?;
    let source_name = source_path
        .file_name()
        .and_then(|name| name.to_str())
        .context("Markdown chapter filename must be valid UTF-8")?
        .to_owned();
    let stem = source_path
        .file_stem()
        .and_then(|name| name.to_str())
        .context("Markdown chapter stem must be valid UTF-8")?;
    let beats = parse_beats(&source);
    if !beats
        .iter()
        .any(|beat| matches!(beat, NovelBeat::Reading { .. }))
    {
        bail!(
            "authored chapter has no readable prose: {}",
            source_path.display()
        );
    }

    Ok(NovelChapter {
        source_name,
        scene: format!("novel_chapter_{index:02}"),
        chapter_id: format!("canonical_chapter_{index:02}"),
        label: chapter_label(stem),
        beats,
    })
}

fn parse_beats(source: &str) -> Vec<NovelBeat> {
    source
        .lines()
        .filter_map(|source_line| {
            let line = source_line.trim_end_matches('\r');
            let trimmed = line.trim();
            if trimmed.is_empty() || is_day_end_marker(trimmed) {
                return None;
            }
            if trimmed == "* * *" {
                return Some(NovelBeat::StructuralBreak);
            }

            let text = strip_scene_heading(line).unwrap_or_else(|| line.to_owned());
            let (speaker, text) = split_attributed_dialogue(&text)
                .map_or_else(|| (None, text), |(speaker, text)| (Some(speaker), text));
            Some(NovelBeat::Reading { speaker, text })
        })
        .collect()
}

fn is_day_end_marker(line: &str) -> bool {
    let lower = line.to_ascii_lowercase();
    (line.starts_with("# ") && lower.ends_with(" end"))
        || (line.starts_with(';') && lower.contains(" end"))
}

fn strip_scene_heading(line: &str) -> Option<String> {
    let trimmed = line.trim();
    let inner = trimmed.strip_prefix("**")?.strip_suffix("**")?.trim();
    (!inner.is_empty()).then(|| inner.to_owned())
}

/// Recognizes only the deliberately compact, explicit character-label form.
/// Lines starting directly with a quote intentionally stay unattributed
/// because the canonical prose uses context-dependent speakers throughout
/// most chapters.
fn split_attributed_dialogue(line: &str) -> Option<(String, String)> {
    let (speaker, spoken) = line.split_once('「')?;
    if speaker.is_empty() || !EXPLICIT_SPEAKERS.contains(&speaker) || !spoken.ends_with('」') {
        return None;
    }
    Some((speaker.to_owned(), format!("「{spoken}")))
}

fn chapter_label(stem: &str) -> String {
    match stem {
        "00_init" => "序章".to_owned(),
        "ex" | "epilogue" => "後日談".to_owned(),
        _ => stem
            .split_once('_')
            .and_then(|(day, _)| day.parse::<u16>().ok())
            .map_or_else(|| stem.replace('_', " "), |day| format!("DAY {day}")),
    }
}

fn render_module(chapters: &[NovelChapter], chapter_select: &str, locale: &str) -> String {
    let mut output = String::new();
    output.push_str("// Generated by aria import-novel. Do not edit this file by hand.\n");
    output.push_str("// The Markdown source remains the canonical prose authority.\n");
    output.push_str("aria;\n");
    output.push_str("module novel.imported;\n\n");

    output.push_str(&format!("scene {chapter_select} {{\n"));
    output.push_str("  screen chapter_select;\n");
    output.push_str("  background asset(\"#102b38\") with wipe(260ms);\n");
    output.push_str("  choice {\n");
    for chapter in chapters {
        output.push_str(&format!(
            "    \"{}\" => {};\n",
            escape_string(&chapter.label),
            chapter.scene
        ));
    }
    output.push_str("  }\n");
    output.push_str("}\n\n");

    for (index, chapter) in chapters.iter().enumerate() {
        output.push_str(&format!(
            "// Source: {}\nscene {} {{\n",
            chapter.source_name, chapter.scene
        ));
        output.push_str(&format!("  locale \"{}\";\n", escape_string(locale)));
        output.push_str(&format!(
            "  persistent flag \"{}_seen\" = true;\n",
            chapter.chapter_id
        ));
        output.push_str(&format!(
            "  unlock chapter \"{}\" progress 1;\n",
            chapter.chapter_id
        ));
        output.push_str("  screen dialogue;\n");
        output.push_str(&format!(
            "  background asset(\"{}\") with fade(260ms);\n",
            BACKGROUND_TONES[index % BACKGROUND_TONES.len()]
        ));

        for beat in &chapter.beats {
            match beat {
                NovelBeat::Reading { speaker, text } => {
                    if let Some(speaker) = speaker {
                        output.push_str(&format!(
                            "  say {}: \"{}\";\n",
                            speaker,
                            escape_string(text)
                        ));
                    } else {
                        output.push_str(&format!("  narrate \"{}\";\n", escape_string(text)));
                    }
                    output.push_str("  await advance;\n");
                }
                NovelBeat::StructuralBreak => {
                    output.push_str("  clear dialogue;\n");
                    output.push_str("  wait 550ms;\n");
                }
            }
        }

        output.push_str(&format!(
            "  chapter \"{}\" progress 100;\n",
            chapter.chapter_id
        ));
        output.push_str("  clear dialogue;\n");
        output.push_str(&format!("  jump {chapter_select};\n"));
        output.push_str("}\n\n");
    }
    output
}

fn escape_string(value: &str) -> String {
    let mut escaped = String::with_capacity(value.len());
    for character in value.chars() {
        match character {
            '\\' => escaped.push_str("\\\\"),
            '"' => escaped.push_str("\\\""),
            '\n' => escaped.push_str("\\n"),
            '\r' => escaped.push_str("\\r"),
            '\t' => escaped.push_str("\\t"),
            other => escaped.push(other),
        }
    }
    escaped
}

fn is_aria_identifier(value: &str) -> bool {
    let mut characters = value.chars();
    matches!(characters.next(), Some(first) if first == '_' || first.is_ascii_alphabetic())
        && characters.all(|character| character == '_' || character.is_ascii_alphanumeric())
}

fn write_atomic(path: &Path, bytes: &[u8]) -> Result<()> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)
            .with_context(|| format!("cannot create {}", parent.display()))?;
    }
    let mut file = AtomicWriteFile::open(path)
        .with_context(|| format!("cannot open generated output {}", path.display()))?;
    file.write_all(bytes)
        .with_context(|| format!("cannot write generated output {}", path.display()))?;
    file.as_file()
        .sync_all()
        .with_context(|| format!("cannot sync generated output {}", path.display()))?;
    file.commit()
        .with_context(|| format!("cannot commit generated output {}", path.display()))?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use aria_core::compiler::{CompileInput, SourceUnit, compile};

    #[test]
    fn imports_canonical_beats_without_inventing_prose() {
        let temp = tempfile::tempdir().unwrap();
        let source = temp.path().join("src");
        fs::create_dir_all(&source).unwrap();
        fs::write(
            source.join("00_init.md"),
            "**9月18日　保健室**\n\n俺「行こう」\n「うん」\n小さく「声」\n* * *\n# 9/18 END\n",
        )
        .unwrap();
        fs::write(
            source.join("01_start.md"),
            "朝の駅。\n\nミオ「\"海\"へ」\n;day1 end\n",
        )
        .unwrap();
        let output = temp.path().join("generated/ja-JP.aria");

        let report = import_novel(&source, &output, "chapter_select_ja", "ja-JP").unwrap();
        assert_eq!(report.chapters.len(), 2);
        assert_eq!(report.reading_beats, 6);
        assert_eq!(report.structural_breaks, 1);
        assert_eq!(report.chapters[0].label, "序章");
        assert_eq!(report.chapters[1].label, "DAY 1");

        let generated = fs::read_to_string(&output).unwrap();
        assert!(generated.contains("narrate \"9月18日　保健室\";"));
        assert!(generated.contains("say 俺: \"「行こう」\";"));
        assert!(generated.contains("narrate \"「うん」\";"));
        assert!(generated.contains("say ミオ: \"「\\\"海\\\"へ」\";"));
        assert!(generated.contains("narrate \"小さく「声」\";"));
        assert!(generated.contains("clear dialogue;\n  wait 550ms;"));
        assert!(!generated.contains("9/18 END"));
        assert!(!generated.contains("day1 end"));

        let output = compile(CompileInput {
            game_id: "jp.example.import".to_owned(),
            entry: "scripts/main.aria".to_owned(),
            sources: vec![
                SourceUnit {
                    logical_path: "scripts/main.aria".to_owned(),
                    source: "aria;\nuse \"scenario/ja-JP.aria\";\nentry start;\nscene start { jump chapter_select_ja; }\n".to_owned(),
                },
                SourceUnit {
                    logical_path: "scripts/scenario/ja-JP.aria".to_owned(),
                    source: generated,
                },
            ],
        });
        assert!(!output.has_errors(), "{:#?}", output.diagnostics);
    }

    #[test]
    fn rejects_an_identifier_that_could_inject_story_source() {
        let temp = tempfile::tempdir().unwrap();
        let source = temp.path().join("src");
        fs::create_dir_all(&source).unwrap();
        fs::write(source.join("00_init.md"), "本文。\n").unwrap();

        let error = import_novel(
            &source,
            &temp.path().join("out.aria"),
            "chapter; end",
            "ja-JP",
        )
        .unwrap_err();
        assert!(error.to_string().contains("Aria identifier"));
    }
}
