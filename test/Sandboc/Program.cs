using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            // Path to vswhere.exe (standard location in Visual Studio installations)
            string vswherePath = @"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe";
            if (!File.Exists(vswherePath))
            {
                throw new FileNotFoundException("vswhere.exe not found at the expected location.", vswherePath);
            }

            // Step 1: Get Visual Studio installation path using vswhere.exe
            var vswhereStartInfo = new ProcessStartInfo
            {
                FileName = vswherePath,
                Arguments = "-latest -property installationPath",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string installationPath;
            using (Process vswhereProcess = Process.Start(vswhereStartInfo))
            {
                installationPath = vswhereProcess.StandardOutput.ReadToEnd().Trim();
                vswhereProcess.WaitForExit();

                if (vswhereProcess.ExitCode != 0 || string.IsNullOrEmpty(installationPath))
                {
                    throw new Exception("Failed to retrieve Visual Studio installation path using vswhere.exe.");
                }
            }

            // Step 2: Construct path to vcvarsall.bat
            string vcvarsallPath = Path.Combine(installationPath, @"VC\Auxiliary\Build\vcvarsall.bat");
            if (!File.Exists(vcvarsallPath))
            {
                throw new FileNotFoundException("vcvarsall.bat not found in the Visual Studio installation.", vcvarsallPath);
            }

            // Step 3: Run dumpbin.exe with arguments
            string dumpbinArgs = "/DEPENDENTS somefile.dll"; // Replace with your actual arguments
            string command = $"/c \"call \\\"{vcvarsallPath}\\\" x64 && dumpbin.exe {dumpbinArgs}\"";

            var dumpbinStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process dumpbinProcess = Process.Start(dumpbinStartInfo))
            {
                string output = dumpbinProcess.StandardOutput.ReadToEnd();
                string error = dumpbinProcess.StandardError.ReadToEnd();
                dumpbinProcess.WaitForExit();

                if (dumpbinProcess.ExitCode != 0)
                {
                    Console.WriteLine("Error running dumpbin.exe:");
                    Console.WriteLine(error);
                }
                else
                {
                    Console.WriteLine("dumpbin.exe output:");
                    Console.WriteLine(output);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}
