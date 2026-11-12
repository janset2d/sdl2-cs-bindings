# Target Frameworks

Date: 2026-11-05

Target `net10.0`, `net11.0`, `netstandard2.0` and `net472`, retaining the Compat `DllImport` codegen pass.
Drop `net8.0` and `net9.0` as support ends on 2026-11-10, and replace `net462` with `net472` because
.NET Framework 4.6.2 support ends on 2027-01-12.

## Consequences

[Directory.Build.props](../../Directory.Build.props) owns the framework values through
`AllTargetFrameworks` and `ExecutableTargetFrameworks`.
This is the decided target policy; those properties and generated outputs still require migration.
