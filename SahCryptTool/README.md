# SahCryptTool

This folder contains the C++ ESAH helper tool source.

Format:

```txt
ESAH + AES-256-GCM
```

## Key alignment (required)

`SahCryptTool` and updater runtime must use the same 32-byte key.

Key locations:

- Tool key: `SahCryptTool/main.cpp` (`kFixedKey`)
- Runtime key: `ServerSide/Updater/launcher.ini` -> `[SahCrypt] KeyHex`

If keys are different, files encrypted by `sahcrypt.exe` may not be decryptable by updater runtime logic.

## Automatic key generation on `SahCryptTool` build (Visual Studio)

You can run the shared key-generation script before every `SahCryptTool` build.

> Important: configure the pre-build script in **only one** project (`Updater` or `SahCryptTool`) for normal solution builds.
> If both projects execute it in the same build batch, the key is regenerated twice.
> The second execution overwrites the first key, which can cause confusion when testing/deploying synchronized runtime/tool keys.

1. Right-click project `SahCryptTool` -> `Properties`.
2. Open `Build Events` -> `Pre-Build Event` -> `Command Line`.
3. Add:

    ```cmd
    powershell -ExecutionPolicy Bypass -File "$(SolutionDir)scripts\generate-sahcrypt-key.ps1"
    ```

4. Ensure **Use In Build** is set to **Yes**.
5. Save and build.

What the script updates:

- `SahCryptTool/main.cpp` (`kFixedKey`)
- `ServerSide/Updater/launcher.ini` (`[SahCrypt] KeyHex`)

## Example build output

```txt
SahCrypt key generated and applied.
KeyHex: <generated-64-hex>
Updated: ...\SahCryptTool\main.cpp
Updated: ...\ServerSide\Updater\launcher.ini
```

## CLI usage

`SahCryptTool` supports interactive mode and CLI mode:

```txt
sahcrypt.exe encrypt <path-to-data.sah>
sahcrypt.exe decrypt <path-to-data.sah>
