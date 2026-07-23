use std::path::PathBuf;

use aria_cli::project::LoadedProject;
use aria_core::bytecode::{ByteOp, LanguageVersion};

#[test]
fn umikaze_sample_compiles_as_declarative_v32_without_host_opcodes() {
    let root = PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../../examples/umikaze");
    let project = LoadedProject::load(&root).expect("sample manifest should load");
    let output = project
        .compile()
        .expect("sample assets should be inspectable");
    assert!(!output.has_errors(), "{:#?}", output.diagnostics);
    let program = output.program.expect("sample should produce bytecode");
    assert_eq!(program.language_version, LanguageVersion::CURRENT);
    assert_eq!(project.manifest.presentation.frontend, "ui");
    let entry = std::fs::read_to_string(root.join("scripts/main.aria")).unwrap();
    assert!(!entry.contains("ui_theme"));
    assert!(!entry.contains("ui_screen"));
    assert!(
        program
            .instructions
            .iter()
            .any(|instruction| instruction.op == ByteOp::SetLocale)
    );
    assert!(
        program
            .instructions
            .iter()
            .any(|instruction| instruction.op == ByteOp::SetChapterProgress)
    );
}
