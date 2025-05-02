using System.CommandLine;

namespace Build.Context.Options;

public static class DumpbinOptions
{
    public static readonly Option<List<string>> DllOption = new(
        "--dll",
        "The list of DLLs to dump. If not specified, all DLLs in the current directory will be dumped.")
    {
        Arity = ArgumentArity.OneOrMore,
    };
}
