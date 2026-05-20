using System.Text.Json;
using Build.Targets.GenerateBindings.Emit.PublicApi;
using Build.Targets.GenerateBindings.Emit.RawAbi;
using Build.Targets.GenerateBindings.Emit.Reports;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emit;

public sealed class BindingEmitter
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    public GeneratedFileSet Emit(BindingModel model, BindingEmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);

        var files = new List<GeneratedFile>();

        var libNameViewIndex = FindLibNameViewIndex(model.Views);
        for (var index = 0; index < model.Views.Count; index++)
        {
            var view = model.Views[index];
            files.Add(RawAbiCommandEmitter.Emit(view, options, includeLibName: index == libNameViewIndex));
        }

        if (model.Constants.Count > 0)
        {
            files.Add(ConstantEmitter.Emit(model.Constants, options));
        }

        if (model.Enums.Count > 0)
        {
            files.Add(EnumEmitter.Emit(model.Enums, options));
        }

        if (model.Handles.Count > 0)
        {
            files.Add(HandleEmitter.Emit(model.Handles, options));
        }

        if (model.Structs.Count > 0)
        {
            files.Add(StructEmitter.Emit(model.Structs, options));
        }

        if (model.Callbacks.Count > 0)
        {
            files.Add(CallbackEmitter.Emit(model.Callbacks, options));
        }

        var emittedFiles = files.Select(file => file.RelativePath)
            .Append("parse-views.json")
            .ToList();
        files.Add(new GeneratedFile(RelativePath: "parse-views.json", Content: EmitReportJson(model, emittedFiles)));

        return new GeneratedFileSet(files);
    }

    private static int FindLibNameViewIndex(IReadOnlyList<BindingParseView> views)
    {
        for (var index = 0; index < views.Count; index++)
        {
            if (string.Equals(views[index].Name, "Neutral", StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private string EmitReportJson(BindingModel model, IReadOnlyList<string> emittedFiles)
    {
        var report = BindingParseViewReportBuilder.Build(model, emittedFiles);
        return JsonSerializer.Serialize(report, _jsonOptions);
    }
}
