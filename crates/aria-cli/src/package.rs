//! `aria package` — bundles a built `aria build` output directory into a
//! distributable zip (deterministic, fixed timestamps, sorted entries).
//!
//! On windows-x64 with `makensis` on PATH, also generates an NSIS installer
//! from a generalized `installer/aria-game.nss` template. macOS produces a
//! zip only; code signing and notarization are explicitly out of scope.

use std::fs;
use std::io::{self, Read, Write};
use std::path::{Path, PathBuf};
use std::sync::LazyLock;

use anyhow::{Context, Result, bail};
use flate2::Compression;
use flate2::write::DeflateEncoder;

use crate::build::{BuildManifest, BuildTarget};

// ── Minimal zip writer ──────────────────────────────────────────────
// The `zip` crate is not a workspace dependency; this implements the
// subset we need: DEFLATE + STORE, fixed timestamps, sorted entries.

/// DOS date/time packed into a u32 (2024-01-01 00:00:00).
const FIXED_DOS_TIME: u32 = ((2024 - 1980) << 25) | (1 << 21) | (1 << 16);
const FIXED_DOS_DATE: u32 = 0;

const SIG_LOCAL: u32 = 0x04034b50;
const SIG_CENTRAL: u32 = 0x02014b50;
const SIG_EOCD: u32 = 0x06054b50;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum CompressionMethod {
    Store = 0,
    Deflate = 8,
}

struct ZipEntry {
    name: String,
    compressed: Vec<u8>,
    uncompressed_size: u32,
    crc32: u32,
    method: CompressionMethod,
    local_header_offset: u32,
}

struct ZipWriter {
    buf: Vec<u8>,
    entries: Vec<ZipEntry>,
}

impl ZipWriter {
    fn new() -> Self {
        Self {
            buf: Vec::new(),
            entries: Vec::new(),
        }
    }

    fn add_file(&mut self, name: &str, data: &[u8]) -> io::Result<()> {
        let name_bytes = name.as_bytes();
        let crc = crc32_fast(data);
        let uncompressed_size = u32::try_from(data.len()).unwrap_or(u32::MAX);

        let mut encoder = DeflateEncoder::new(Vec::new(), Compression::default());
        encoder.write_all(data)?;
        let compressed = encoder.finish()?;

        let (method, compressed_data) = if compressed.len() < data.len() {
            (CompressionMethod::Deflate, compressed)
        } else {
            (CompressionMethod::Store, data.to_vec())
        };

        let local_header_offset = u32::try_from(self.buf.len()).unwrap_or(u32::MAX);

        let name_len = u16::try_from(name_bytes.len()).unwrap_or(u16::MAX);
        self.buf.extend_from_slice(&SIG_LOCAL.to_le_bytes());
        self.buf.extend_from_slice(&20u16.to_le_bytes());
        self.buf.extend_from_slice(&0u16.to_le_bytes());
        self.buf.extend_from_slice(&(method as u16).to_le_bytes());
        self.buf.extend_from_slice(&FIXED_DOS_TIME.to_le_bytes());
        self.buf.extend_from_slice(&FIXED_DOS_DATE.to_le_bytes());
        self.buf.extend_from_slice(&crc.to_le_bytes());
        self.buf.extend_from_slice(
            &u32::try_from(compressed_data.len())
                .unwrap_or(u32::MAX)
                .to_le_bytes(),
        );
        self.buf.extend_from_slice(&uncompressed_size.to_le_bytes());
        self.buf.extend_from_slice(&name_len.to_le_bytes());
        self.buf.extend_from_slice(&0u16.to_le_bytes());
        self.buf.extend_from_slice(name_bytes);
        self.buf.extend_from_slice(&compressed_data);

        self.entries.push(ZipEntry {
            name: name.to_owned(),
            compressed: compressed_data,
            uncompressed_size,
            crc32: crc,
            method,
            local_header_offset,
        });

        Ok(())
    }

    fn finish(mut self) -> Vec<u8> {
        let central_dir_offset = u32::try_from(self.buf.len()).unwrap_or(u32::MAX);

        for entry in &self.entries {
            let name_bytes = entry.name.as_bytes();
            let name_len = u16::try_from(name_bytes.len()).unwrap_or(u16::MAX);
            self.buf.extend_from_slice(&SIG_CENTRAL.to_le_bytes());
            self.buf.extend_from_slice(&20u16.to_le_bytes());
            self.buf.extend_from_slice(&20u16.to_le_bytes());
            self.buf.extend_from_slice(&0u16.to_le_bytes());
            self.buf
                .extend_from_slice(&(entry.method as u16).to_le_bytes());
            self.buf.extend_from_slice(&FIXED_DOS_TIME.to_le_bytes());
            self.buf.extend_from_slice(&FIXED_DOS_DATE.to_le_bytes());
            self.buf.extend_from_slice(&entry.crc32.to_le_bytes());
            self.buf.extend_from_slice(
                &u32::try_from(entry.compressed.len())
                    .unwrap_or(u32::MAX)
                    .to_le_bytes(),
            );
            self.buf
                .extend_from_slice(&entry.uncompressed_size.to_le_bytes());
            self.buf.extend_from_slice(&name_len.to_le_bytes());
            self.buf.extend_from_slice(&0u16.to_le_bytes());
            self.buf.extend_from_slice(&0u16.to_le_bytes());
            self.buf.extend_from_slice(&0u16.to_le_bytes());
            self.buf.extend_from_slice(&0u16.to_le_bytes());
            self.buf.extend_from_slice(&0u32.to_le_bytes());
            self.buf
                .extend_from_slice(&entry.local_header_offset.to_le_bytes());
            self.buf.extend_from_slice(name_bytes);
        }

        let central_dir_size =
            u32::try_from(self.buf.len()).unwrap_or(u32::MAX) - central_dir_offset;
        let entry_count = u16::try_from(self.entries.len()).unwrap_or(u16::MAX);

        self.buf.extend_from_slice(&SIG_EOCD.to_le_bytes());
        self.buf.extend_from_slice(&0u16.to_le_bytes());
        self.buf.extend_from_slice(&0u16.to_le_bytes());
        self.buf.extend_from_slice(&entry_count.to_le_bytes());
        self.buf.extend_from_slice(&entry_count.to_le_bytes());
        self.buf.extend_from_slice(&central_dir_size.to_le_bytes());
        self.buf
            .extend_from_slice(&central_dir_offset.to_le_bytes());
        self.buf.extend_from_slice(&0u16.to_le_bytes());

        self.buf
    }
}

/// Fast CRC32 implementation (pure Rust, table-based).
fn crc32_fast(data: &[u8]) -> u32 {
    const TABLE_SIZE: usize = 256;
    static TABLE: LazyLock<[u32; TABLE_SIZE]> = LazyLock::new(|| {
        let mut t = [0u32; TABLE_SIZE];
        for (index, slot) in t.iter_mut().enumerate() {
            let mut crc = index as u32;
            for _ in 0..8 {
                crc = if crc & 1 != 0 {
                    0xEDB88320 ^ (crc >> 1)
                } else {
                    crc >> 1
                };
            }
            *slot = crc;
        }
        t
    });
    let mut crc = 0xFFFF_FFFFu32;
    for &byte in data {
        crc = TABLE[((crc ^ u32::from(byte)) & 0xFF) as usize] ^ (crc >> 8);
    }
    !crc
}

// ── Bundle reading ──────────────────────────────────────────────────

fn read_build_manifest(bundle: &Path) -> Result<(BuildManifest, serde_json::Value)> {
    let manifest_path = bundle.join("build-manifest.json");
    let bytes = fs::read(&manifest_path).with_context(|| {
        format!(
            "bundle directory missing build-manifest.json: {}",
            bundle.display()
        )
    })?;
    let manifest: BuildManifest =
        serde_json::from_slice(&bytes).context("invalid build-manifest.json")?;
    let bundle_json: serde_json::Value = serde_json::from_slice(
        &fs::read(bundle.join("bundle.aria.json")).context("missing bundle.aria.json")?,
    )
    .context("invalid bundle.aria.json")?;
    Ok((manifest, bundle_json))
}

fn collect_entries(root: &Path) -> Result<Vec<PathBuf>> {
    let mut entries = Vec::new();
    for entry in walkdir::WalkDir::new(root)
        .follow_links(false)
        .into_iter()
        .filter_map(|e| e.ok())
    {
        if entry.file_type().is_file() {
            let rel = entry
                .path()
                .strip_prefix(root)
                .context("strip prefix from bundle entry")?
                .to_owned();
            entries.push(rel);
        }
    }
    entries.sort();
    Ok(entries)
}

fn create_deterministic_zip(bundle: &Path, output: &Path) -> Result<u64> {
    if let Some(parent) = output.parent() {
        fs::create_dir_all(parent)
            .with_context(|| format!("cannot create output directory: {}", parent.display()))?;
    }

    let entries = collect_entries(bundle)?;
    if entries.is_empty() {
        bail!("bundle directory contains no files: {}", bundle.display());
    }

    let mut zip = ZipWriter::new();
    let mut buffer = Vec::new();

    for rel in &entries {
        let full = bundle.join(rel);
        let name = rel
            .to_str()
            .with_context(|| format!("non-UTF-8 path in bundle: {}", rel.display()))?;
        let name = name.replace('\\', "/");

        buffer.clear();
        fs::File::open(&full)
            .with_context(|| format!("cannot open {}", full.display()))?
            .read_to_end(&mut buffer)?;

        zip.add_file(&name, &buffer)
            .with_context(|| format!("cannot add {name} to zip"))?;
    }

    let bytes = zip.finish();
    let len = bytes.len() as u64;
    fs::write(output, &bytes)
        .with_context(|| format!("cannot write zip to {}", output.display()))?;
    Ok(len)
}

// ── NSIS installer generation ───────────────────────────────────────

fn generate_nsis_installer(
    bundle: &Path,
    manifest: &BuildManifest,
    bundle_json: &serde_json::Value,
    output_dir: &Path,
) -> Result<()> {
    let repo_root = find_repo_root(bundle)?;
    let template = repo_root.join("installer/aria-game.nss");
    if !template.is_file() {
        bail!(
            "NSIS template not found at {}; cannot generate installer",
            template.display()
        );
    }

    let game_id = bundle_json["game_id"].as_str().unwrap_or("unknown");
    let game_version = bundle_json["game_version"].as_str().unwrap_or("dev");
    let game_title = bundle_json["game_title"].as_str().unwrap_or("Aria Game");
    let publisher = bundle_json
        .get("publisher")
        .and_then(|v| v.as_str())
        .unwrap_or("Ponkotusoft");

    let player_filename = match manifest.target {
        BuildTarget::WindowsX64 => "aria-player.exe",
        _ => "aria-player",
    };

    fs::create_dir_all(output_dir)?;
    let nsi_path = output_dir.join("installer.nsi");
    let mut script = fs::read_to_string(&template)
        .with_context(|| format!("cannot read NSIS template: {}", template.display()))?;

    let appdir = bundle.display().to_string().replace('/', "\\");
    let outfile = output_dir
        .join(format!("{game_id}-setup.exe"))
        .display()
        .to_string()
        .replace('/', "\\");

    script = script.replace("{{APPDIR}}", &appdir);
    script = script.replace("{{OUTFILE}}", &outfile);
    script = script.replace("{{VERSION}}", game_version);
    script = script.replace("{{PRODUCT_NAME}}", game_title);
    script = script.replace("{{PUBLISHER}}", publisher);
    script = script.replace("{{PLAYER_FILENAME}}", player_filename);

    fs::write(&nsi_path, &script)?;

    let status = std::process::Command::new("makensis")
        .arg(&nsi_path)
        .status()
        .context("makensis not found on PATH; install NSIS to generate a Windows installer")?;
    if !status.success() {
        bail!("makensis exited with status {status}");
    }

    let installer_path = output_dir.join(format!("{game_id}-setup.exe"));
    if !installer_path.is_file() {
        bail!("makensis did not produce the expected installer");
    }

    println!("  NSIS installer: {}", installer_path.display());
    Ok(())
}

fn find_repo_root(start: &Path) -> Result<PathBuf> {
    let mut current = start
        .canonicalize()
        .with_context(|| format!("cannot canonicalize: {}", start.display()))?;
    loop {
        let cargo_toml = current.join("Cargo.toml");
        if cargo_toml.is_file()
            && let Ok(contents) = fs::read_to_string(&cargo_toml)
            && (contents.contains("aria-core") || contents.contains("aria-engine"))
        {
            return Ok(current);
        }
        if !current.pop() {
            break;
        }
    }
    bail!(
        "cannot locate repository root from {}; place installer/aria-game.nss in the repo root",
        start.display()
    );
}

// ── Command entry ───────────────────────────────────────────────────

pub fn command(bundle: &Path, out: Option<&Path>) -> Result<u8> {
    let bundle = bundle
        .canonicalize()
        .with_context(|| format!("bundle directory not found: {}", bundle.display()))?;

    if !bundle.is_dir() {
        bail!("bundle path is not a directory: {}", bundle.display());
    }

    let (manifest, bundle_json) = read_build_manifest(&bundle)?;

    let game_id = bundle_json["game_id"]
        .as_str()
        .context("bundle.aria.json missing game_id")?;
    let game_version = bundle_json["game_version"]
        .as_str()
        .context("bundle.aria.json missing game_version")?;
    let target_str = manifest.target.as_str();

    let zip_name = format!("{game_id}-{game_version}-{target_str}.zip");

    let output_dir = out
        .map(PathBuf::from)
        .unwrap_or_else(|| bundle.parent().expect("bundle has no parent").join("dist"));
    fs::create_dir_all(&output_dir)
        .with_context(|| format!("cannot create output directory: {}", output_dir.display()))?;
    let zip_path = output_dir.join(&zip_name);

    println!("Packaging bundle: {}", bundle.display());
    println!("  Target: {}", target_str);
    println!("  Game: {game_id} v{game_version}");

    let zip_size = create_deterministic_zip(&bundle, &zip_path)?;
    println!(
        "  Zip: {} ({:.2} MB)",
        zip_path.display(),
        zip_size as f64 / 1_048_576.0
    );

    if manifest.target == BuildTarget::WindowsX64
        && let Err(error) = generate_nsis_installer(&bundle, &manifest, &bundle_json, &output_dir)
    {
        eprintln!("  Note: NSIS installer generation skipped: {error}");
        eprintln!("  Install NSIS (makensis) to generate a Windows installer.");
    }

    if manifest.target == BuildTarget::MacosUniversal {
        println!("  Note: macOS code signing and notarization are out of scope for this command.");
    }

    println!("Packaged to {}", zip_path.display());
    Ok(0)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn deterministic_zip_produces_identical_bytes() {
        let temp = tempfile::tempdir().unwrap();
        let bundle = temp.path().join("bundle");
        fs::create_dir_all(&bundle).unwrap();
        fs::write(bundle.join("game.ariac"), b"ariac-data").unwrap();
        fs::write(bundle.join("bundle.aria.json"), r#"{"schema_version":5}"#).unwrap();

        let zip1 = temp.path().join("out1.zip");
        let zip2 = temp.path().join("out2.zip");
        create_deterministic_zip(&bundle, &zip1).unwrap();
        create_deterministic_zip(&bundle, &zip2).unwrap();

        assert_eq!(
            fs::read(&zip1).unwrap(),
            fs::read(&zip2).unwrap(),
            "deterministic zip outputs must be byte-identical"
        );
    }

    #[test]
    fn deterministic_zip_is_sorted() {
        let temp = tempfile::tempdir().unwrap();
        let bundle = temp.path().join("bundle");
        fs::create_dir_all(bundle.join("z-dir")).unwrap();
        fs::create_dir_all(bundle.join("a-dir")).unwrap();
        fs::write(bundle.join("z-dir/z.txt"), b"zzzzzzzzzz").unwrap();
        fs::write(bundle.join("a-dir/a.txt"), b"aaaaaaaaaa").unwrap();
        fs::write(bundle.join("m.txt"), b"mmmmmmmmmm").unwrap();

        let zip_path = temp.path().join("out.zip");
        create_deterministic_zip(&bundle, &zip_path).unwrap();

        let bytes = fs::read(&zip_path).unwrap();
        let mut names = Vec::new();
        let mut pos = 0;
        while pos + 4 <= bytes.len() {
            let sig = u32::from_le_bytes(bytes[pos..pos + 4].try_into().unwrap());
            if sig == SIG_LOCAL {
                let name_len =
                    u16::from_le_bytes(bytes[pos + 26..pos + 28].try_into().unwrap()) as usize;
                let extra_len =
                    u16::from_le_bytes(bytes[pos + 28..pos + 30].try_into().unwrap()) as usize;
                let name =
                    String::from_utf8_lossy(&bytes[pos + 30..pos + 30 + name_len]).to_string();
                names.push(name);
                let comp_len =
                    u32::from_le_bytes(bytes[pos + 18..pos + 22].try_into().unwrap()) as usize;
                pos += 30 + name_len + extra_len + comp_len;
            } else if sig == SIG_CENTRAL || sig == SIG_EOCD {
                break;
            } else {
                pos += 1;
            }
        }
        let mut sorted = names.clone();
        sorted.sort();
        assert_eq!(names, sorted, "zip entries must be sorted");
    }

    #[test]
    fn crc32_fast_matches_known_vector() {
        assert_eq!(crc32_fast(b"123456789"), 0xCBF43926);
    }
}
