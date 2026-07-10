# Roslyn (MCP execute_code)

Vendored NuGet DLLs for Coplay MCP `execute_code` with C# 12+ (Roslyn compiler).

## Files (do not rename)

| DLL | NuGet package | Version |
|-----|---------------|---------|
| `Microsoft.CodeAnalysis.dll` | microsoft.codeanalysis.common | 4.12.0 |
| `Microsoft.CodeAnalysis.CSharp.dll` | microsoft.codeanalysis.csharp | 4.12.0 |
| `System.Collections.Immutable.dll` | system.collections.immutable | 8.0.0 |
| `System.Reflection.Metadata.dll` | system.reflection.metadata | 8.0.0 |
| `System.Runtime.CompilerServices.Unsafe.dll` | system.runtime.compilerservices.unsafe | 6.0.0 |

## Reinstall (maintainers)

In Unity Editor with MCP package loaded:

```csharp
MCPForUnity.Editor.Setup.RoslynInstaller.Install(interactive: false);
```

Or MCP For Unity → Deps → Roslyn → Install.

## Hub template

Ship this folder **with** `.dll` files (not metas only). DLLs are not gitignored.

## Verify

MCP For Unity → Deps → Roslyn should show green. `execute_code` with `compiler: roslyn` should compile modern C#.
