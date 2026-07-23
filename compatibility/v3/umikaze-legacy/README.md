# umikaze legacy source boundary

The unmodified legacy corpus is kept at
`src/AriaEngine/assets/scripts` together with the original `init.aria`, asset
tree, and C# runtime. This directory is the named compatibility boundary for
tools and release reviews; it is intentionally a pointer rather than a second
copy of the large binary asset tree.

Run the migration against a clean copy of the C# project:

```sh
tmp=$(mktemp -d)
cp -a src/AriaEngine/. "$tmp/umikaze"
cargo run --locked -p aria-cli -- migrate "$tmp/umikaze" --game-id jp.example.umikaze
```

The committed runnable V3.1 vertical slice is [examples/umikaze](../../../examples/umikaze).
