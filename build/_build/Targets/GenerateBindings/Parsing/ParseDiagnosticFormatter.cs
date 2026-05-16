using CppAst;

namespace Build.Targets.GenerateBindings.Parsing;

public sealed class ParseDiagnosticFormatter
{
    private readonly string _diagnosticLineSeparator = Environment.NewLine;

    public string FormatErrors(string parseViewName, CppCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var messages = compilation.Diagnostics.Messages
            .Where(message => message.Type == CppLogMessageType.Error)
            .Select(message => "  " + message)
            .ToArray();

        return messages.Length == 0
            ? $"CppAst parse failed for {parseViewName} without diagnostics."
            : $"CppAst parse failed for {parseViewName}:{_diagnosticLineSeparator}{string.Join(_diagnosticLineSeparator, messages)}";
    }
}
