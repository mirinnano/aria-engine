# Historical migration note

The in-tree `aria migrate` command and compatibility runtime were removed.
Aria does not keep an executable source-migration bridge, a legacy parser, or
a source-language mode.

To move an older project, create a new `aria;` source tree using the
[current language specification](../spec/aria.md), preserve original sources
outside the runtime package, and validate the result with:

```sh
cargo run --locked -p aria-cli -- check path/to/project --release
```

Conversion is intentionally an authoring exercise, not a runtime feature:
implicit waits, legacy registers, line labels, custom UI layout, and old
resource IDs do not have a semantics-preserving one-line rewrite. The current
compiler rejects them with source-located diagnostics and packages no legacy
bytecode or host bridge.
