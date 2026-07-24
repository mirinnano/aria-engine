declare global {
  interface ImportMetaEnv {
    readonly VITE_ARIA_PAK_VERIFICATION_KEY_ID?: string;
    readonly VITE_ARIA_PAK_VERIFICATION_KEY_HEX?: string;
  }

  interface ImportMeta {
    readonly env: ImportMetaEnv;
  }

  var ariaPakKeyProvider:
    | ((_bundle: unknown) => Promise<{
        verification_key_id: string;
        verification_key_hex: string;
        encryption_key_id?: string;
        encryption_key_hex?: string;
      }>)
    | undefined;
}

export {};
