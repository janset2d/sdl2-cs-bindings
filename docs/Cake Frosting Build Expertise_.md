# **Report: Mastering Cake Frosting for Robust.NET Build Automation**

## **1\. Introduction to Cake Frosting**

Build automation is a cornerstone of modern software development, particularly within the.NET ecosystem. It ensures consistency, reduces manual errors, and accelerates the delivery pipeline. Cake (C\# Make) has emerged as a prominent cross-platform build automation system designed specifically with.NET developers in mind.1

### **1.1. What is Cake Build?**

Cake is a build automation system that utilizes a C\# Domain Specific Language (DSL) or standard C\# to define and execute build tasks.1 Its primary goal is to orchestrate common development workflows such as compiling code, running unit tests, copying files, creating NuGet packages, and deploying applications.1

Key characteristics make Cake a compelling choice for.NET projects:

* **Cross-Platform:** Cake runs seamlessly on Windows, Linux, and macOS, leveraging the modern.NET platform.1  
* **Familiar Syntax:** Build scripts are written in C\#, a language familiar to.NET developers.2  
* **IDE Integration:** Cake offers integration with popular IDEs like Visual Studio, Visual Studio Code, and Rider, providing features like IntelliSense, debugging, and refactoring support.1  
* **Reliability:** Cake is designed to execute builds consistently, whether on a local developer machine or a Continuous Integration (CI) server like Azure Pipelines, GitHub Actions, or TeamCity.2  
* **Extensibility:** While Cake includes built-in support for essential tools like MSBuild, the.NET CLI, NuGet, xUnit, and NUnit, its functionality can be vastly extended through a rich ecosystem of over 300 community-contributed Addins and Modules.1

### **1.2. Introducing Cake Frosting: The C\# Native Approach**

Within the Cake ecosystem, different "runners" exist to execute build scripts.1 Cake Frosting is a specific runner that distinguishes itself by allowing developers to write their build logic as standard.NET console applications using pure C\# classes, rather than a specialized DSL.2

This represents a significant design choice compared to the more traditional Cake.NET Tool runner, which relies on a C\# DSL (.cake script files).2 The DSL aims for conciseness by removing some standard C\# syntax.7 However, Frosting embraces standard C\# conventions, offering several advantages, particularly for complex or team-based projects. By using standard C\# classes, Frosting fully leverages the power of the.NET development environment, including robust IntelliSense, advanced refactoring capabilities, integrated debugging, and, crucially, compile-time type safety.2 This strong typing catches errors earlier in the development cycle, before the build script is even executed, leading to more reliable and maintainable build processes. While this C\#-native approach can sometimes lead to slightly more verbose code compared to the DSL 7, this trade-off is often considered beneficial for the gains in maintainability, tooling support, and the ability for C\# developers to apply their existing skills directly to the build process without learning a separate DSL syntax. The availability of both Frosting and the.NET Tool runner indicates that Cake accommodates different project needs and developer preferences.1

To use Cake Frosting, projects must target a compatible.NET version. Current documentation and package requirements specify.NET 8.0 or newer.4 It's important to note that historical versions of Cake and Frosting supported older frameworks like.NET Core 3.1 or.NET 5/6, but adhering to the latest requirements ensures access to current features and support.7

### **1.3. Getting Started: Project Setup**

Initiating a Cake Frosting project is streamlined through.NET templates.

1. **Install the Template:** The first step is to install the Cake Frosting project template globally using the.NET CLI 7:  
   Bash  
   dotnet new install Cake.Frosting.Template

2. **Create the Project:** Navigate to the desired directory for your build project and create a new project using the installed template 7:  
   Bash  
   dotnet new cakefrosting  
   This command scaffolds a basic Frosting project structure.

The generated project typically includes:

* A build directory (or similar) containing the C\# project file (.csproj) and source files (.cs) for the build logic.7  
* Program.cs: The entry point for the console application, responsible for configuring and running the Cake host.7  
* BuildContext.cs: A class inheriting from FrostingContext, used to pass data and provide access to Cake functionalities to tasks.9  
* Example Task Files: Demonstrating basic task definition (e.g., HelloTask, WorldTask).9  
* Bootstrapper Scripts (build.ps1, build.sh): Convenience scripts located in the repository root to simplify running the build on different operating systems.7 These scripts typically handle ensuring the.NET SDK is available and then execute the compiled Frosting console application (dotnet run \--project build/build.csproj \--...). Historically, Cake bootstrappers might have also handled downloading tools like nuget.exe 17, but with Frosting, the primary role is executing the.NET application.

To run the default build defined in the template:

* On Windows (PowerShell): .\\build.ps1 \--target=Default 7  
* On Linux/macOS (Bash): ./build.sh \--target=Default 7

This executes the task named "Default" and any tasks it depends on.

## **2\. Core Concepts: Tasks, Dependencies, and Context**

Cake Frosting organizes build logic around three fundamental concepts: Tasks, Dependencies, and the Build Context.

### **2.1. Defining Tasks in C\#**

Tasks represent the individual units of work within a build process.1 In Frosting, each task is implemented as a C\# class.

* **Inheritance:** Task classes must inherit from either FrostingTask\<T\> for synchronous operations or AsyncFrostingTask\<T\> for asynchronous operations, where T represents the type of the build context (typically a custom class inheriting from FrostingContext).7  
* **Naming:** The \`\` attribute is mandatory and assigns a unique, executable name to the task, which is used for targeting and dependency management.9  
* **Logic Implementation:** The core logic of the task resides within the Run method (for FrostingTask\<T\>) or the RunAsync method (for AsyncFrostingTask\<T\>). These methods receive an instance of the build context (T) as a parameter, providing access to build state and Cake functionalities.7

For example, a simple task to clean a directory might look like this 9:

C\#

using Cake.Core;  
using Cake.Core.IO;  
using Cake.Frosting;  
using Cake.Common.IO; // Required for CleanDirectory alias

public class BuildContext : FrostingContext  
{  
    public string MsBuildConfiguration { get; set; }  
    public DirectoryPath ArtifactsDirectory { get; set; }

    public BuildContext(ICakeContext context) : base(context)  
    {  
        MsBuildConfiguration \= context.Argument("configuration", "Release");  
        ArtifactsDirectory \= context.Directory("./artifacts");  
    }  
}

public sealed class CleanTask : FrostingTask\<BuildContext\>  
{  
    public override void Run(BuildContext context)  
    {  
        // Access context property  
        var cleanPath \= context.ArtifactsDirectory.Combine("bin").Combine(context.MsBuildConfiguration);  
        context.Log.Information($"Cleaning directory: {cleanPath.FullPath}");

        // Use a Cake alias via the context  
        context.CleanDirectory(cleanPath);  
    }  
}

### **2.2. Managing Dependencies**

Dependencies define the execution order of tasks, forming a directed acyclic graph (DAG) that represents the build pipeline.

* **Defining Dependencies:** The \`\` attribute is applied to a task class to specify that it must run *after* OtherTaskClass has successfully completed.7 A task can have multiple \[IsDependentOn\] attributes.  
* **Reverse Dependencies:** While less common, the \`\` attribute indicates that AnotherTaskClass depends on the decorated task.18 Note that some older dependency attributes like DependencyAttribute and ReverseDependencyAttribute are obsolete or removed and should not be used.20  
* **Execution:** When a target task is invoked, Cake's engine analyzes the dependency graph and executes all prerequisite tasks in the correct order, ensuring each task runs only once per build execution.7  
* **Targeting:** The \--target \<TASK\_NAME\> command-line switch specifies the final task to execute in the pipeline. If omitted, Cake defaults to running the task named "Default".7  
* **Visualization:** The \--tree switch prints the task dependency tree to the console, which is helpful for understanding the execution flow.8  
* **Exclusive Execution:** The \-e or \--exclusive switch runs only the specified target task, ignoring all its dependencies.8

A typical Clean \-\> Build \-\> Test pipeline definition 9:

C\#

public sealed class CleanTask : FrostingTask\<BuildContext\> { /\*... \*/ }

 // Build runs after Clean  
public sealed class BuildTask : FrostingTask\<BuildContext\> { /\*... \*/ }

 // Test runs after Build  
public sealed class TestTask : FrostingTask\<BuildContext\> { /\*... \*/ }

 // Default target runs Test (and implicitly Build and Clean)  
public sealed class Default : FrostingTask { } // Default task often has no logic itself

### **2.3. The Central Role of BuildContext (IFrostingContext)**

The Build Context is the cornerstone of state management and functionality access within a Cake Frosting build.

* **Interface and Implementation:** IFrostingContext defines the contract for the build context.18 FrostingContext is the default base class implementation provided by Cake.7  
* **Purpose:** The context serves as the primary interface for tasks to interact with the Cake runtime environment. It aggregates access to:  
  * Logging (context.Log).9  
  * Command-line arguments (context.Arguments, context.Argument\<T\>).9  
  * Environment variables (context.Environment).  
  * File system operations (context.File, context.Directory, context.CleanDirectory, etc.).7  
  * Process execution (context.StartProcess, tool aliases like context.DotNetBuild).9  
  * Tool location resolution (context.Tools).28  
  * Underlying ICakeContext features.1  
* **Customization:** Projects typically define their own context class by inheriting from FrostingContext. This allows adding custom properties to hold build-specific configuration or state that needs to be shared across tasks.7 These properties are often initialized within the custom context's constructor, frequently parsing command-line arguments or reading environment variables.9  
* **Registration:** The specific custom context type to be used for the build run must be registered with the CakeHost in Program.cs using the UseContext\<TContext\>() extension method.9  
* **Usage in Tasks:** An instance of the registered context type is passed as an argument to the Run or RunAsync method of each task, allowing the task logic to access both standard Cake features and custom properties.7

### **2.4. Setup and Teardown Lifecycle**

Frosting provides hooks to execute logic at specific points in the build lifecycle, enabling resource management and consistent setup/cleanup actions.

* **Global Lifetime (Setup/Teardown):** Executes code once before the *first* task begins and once after the *last* task completes (including in case of errors during task execution, provided teardown logic is reached).  
  * **Implementation:** Create a class implementing IFrostingLifetime or, more commonly, inheriting from the base class FrostingLifetime\<TContext\> (where TContext is your build context type).18 Override the Setup and Teardown methods.  
  * **Registration:** Register the lifetime class with the CakeHost using .UseLifetime\<YourLifetimeClass\>().29  
  * **Use Cases:** Ideal for initializing shared resources at the start of the build (e.g., starting a test server, establishing a database connection) and ensuring their proper disposal at the end, regardless of build success or failure.30 The Setup method receives an ISetupContext containing information about the tasks to be executed, while Teardown receives an ITeardownContext with information about the build run, including any exceptions.30  
* **Task Lifetime (TaskSetup/TaskTeardown):** Executes code before and after *each individual task* runs.  
  * **Implementation:** Create a class implementing IFrostingTaskLifetime or inheriting from FrostingTaskLifetime\<TContext\>.18 Override the Setup and Teardown methods.  
  * **Registration:** Register the task lifetime class with the CakeHost using .UseTaskLifetime\<YourTaskLifetimeClass\>().29  
  * **Use Cases:** Suitable for actions specific to each task, such as logging entry/exit points, setting up task-specific state, or cleaning up resources used only by that task.30 The Setup method receives an ITaskSetupContext and Teardown receives an ITaskTeardownContext, both containing information about the specific task being executed.30 Note that some modules might interact with or potentially reserve the single IFrostingTaskSetup hook.35  
* **Error Handling:**  
  * **Task-Level:** Individual tasks can override the OnError(Exception exception, TContext context) method to perform specific actions when an unhandled exception occurs within that task's Run/RunAsync method.33  
  * **Continue on Error:** Applying the \[ContinueOnError\] attribute to a task class instructs the Cake engine to log the error but continue executing subsequent independent tasks, rather than halting the entire build.18

## **3\. Interacting with the Build Environment: Process and File System Abstractions**

A core function of any build system is interacting with the underlying operating system, primarily through executing external tools and manipulating the file system. Cake Frosting provides robust and type-safe abstractions for these operations, primarily through its concept of Aliases.

### **3.1. Cake Aliases: The Gateway to Functionality**

Cake Aliases are the primary mechanism for accessing built-in and extended functionalities within a Frosting build script.

* **Definition:** Aliases are essentially C\# extension methods that operate on the ICakeContext interface (and thus are available via the BuildContext instance passed to tasks).1 They provide high-level, convenient wrappers around common build operations, tool executions, and system interactions.  
* **Usage in Frosting:** Unlike Cake Script where aliases can often be called directly (e.g., CleanDirectory("./temp")), in Frosting, they must be invoked as extension methods on the context object (e.g., context.CleanDirectory("./temp")).7 This explicit invocation reinforces the role of the context as the central hub for build operations.  
* **Benefits:** Aliases offer a consistent, discoverable, and strongly-typed C\# API for build tasks. This improves code readability and leverages C\# compiler checks, reducing the likelihood of runtime errors compared to manually constructing command-line arguments or using raw system APIs.7  
* **Extensibility:** While Cake provides numerous built-in aliases (especially in Cake.Common), the ecosystem relies heavily on Addins to provide aliases for specific external tools or services (e.g., Git, Docker, AWS, Azure).1

### **3.2. Executing External Processes**

Cake excels at orchestrating external command-line tools, abstracting away the complexities of process management.

* **Core Principle:** Cake aliases act as wrappers around command-line tools.2 When an alias like context.DotNetBuild(...) is called, Cake constructs the appropriate dotnet build command line, executes it, and handles its output and exit code.  
* **Tool Availability:** A critical prerequisite is that the underlying tool (e.g., dotnet.exe, msbuild.exe, nuget.exe, git.exe, docker.exe) must be installed on the build agent machine and typically needs to be accessible via the system's PATH environment variable.17 Cake itself usually does not bundle these external tools; it merely provides the orchestration layer.17 Some aliases allow specifying an explicit tool path if it's not on the PATH.28  
* **Example \-.NET CLI Aliases:** The Cake.Common assembly (included by default) and potentially the Cake.DotNetTool.Module 4 provide numerous aliases for interacting with the dotnet CLI:  
  * context.DotNetBuild(projectPath, settings): Compiles a.NET project or solution.9 Configuration is done via DotNetBuildSettings (e.g., setting Configuration, Framework, NoRestore).  
  * context.DotNetTest(projectPath, settings): Runs tests using the specified test project or solution.9 Configuration via DotNetTestSettings (e.g., Configuration, NoBuild, Loggers, Filter).  
  * context.DotNetRun(projectPath, settings): Runs a.NET project.23 Configuration via DotNetRunSettings.  
  * context.DotNetPack(projectPath, settings): Creates a NuGet package from a project.26 Configuration via DotNetPackSettings (e.g., Configuration, OutputDirectory, VersionSuffix).  
  * context.DotNetPublish(projectPath, settings): Publishes a.NET application.20 Configuration via DotNetPublishSettings (e.g., Configuration, Framework, OutputDirectory, Runtime).  
  * context.DotNetClean(projectPath, settings): Cleans build outputs.26 Configuration via DotNetCleanSettings.  
  * context.DotNetTool(command, settings): Executes.NET global or local tools.26  
  * context.DotNetNuGetPush(packagePath, settings): Pushes a NuGet package.26 Configuration via DotNetNuGetPushSettings (e.g., Source, ApiKey, SkipDuplicate).  
  * *Naming Convention:* Note that older DotNetCore\*\*\* aliases (e.g., DotNetCoreBuild) are being phased out in favor of the shorter DotNet\*\*\* names (e.g., DotNetBuild) since.NET 5 dropped the "Core" branding. While the older aliases might still work via forwarding for backward compatibility, using the newer DotNet\*\*\* names is recommended.51  
* **Example \- MSBuild Aliases:** Provided by Cake.Common, these aliases interact directly with MSBuild.exe:  
  * context.MSBuild(solutionOrProjectPath, settings): Builds a solution or project file using MSBuild.27  
  * MSBuildSettings: Allows detailed configuration, including setting the target framework, configuration, platform, verbosity, custom properties (WithProperty("PropertyName", "Value")), specific targets (WithTarget("TargetName")), and crucially, the MSBuild tool version or path (UseToolVersion, WithToolPath).28  
* **Tool Resolution and Reproducibility:** Cake includes logic to locate required tools like MSBuild, often searching standard installation paths associated with Visual Studio or.NET SDKs.20 However, build environments can be complex, containing multiple versions of these tools. Relying solely on automatic discovery can introduce variability, potentially causing a build to use a different MSBuild version on the CI server than on a local machine.28 This undermines build reproducibility, a key goal of build automation.1 To ensure consistency, explicitly configuring the path to the desired executable using the ToolPath property or the WithToolPath extension method on the relevant settings object (e.g., MSBuildSettings) is a best practice.28 This removes ambiguity and guarantees the same tool version is used across different environments.  
* **Handling Arguments and Output:** Aliases typically provide properties within their settings objects to pass common command-line arguments. For less common arguments or custom tools, aliases like StartProcess might be used, potentially requiring manual argument construction using ProcessArgumentBuilder.26 Aliases also manage capturing standard output/error and handling tool exit codes, often providing options to ignore errors or customize behavior.13 Custom command-line arguments passed to the Frosting application itself (not recognized by Cake) are available via context.Arguments for use within task logic.8

**Table 3.1: Common Tool Aliases in Cake Frosting**

| Tool Category | Example Alias (context.\<Alias\>) | Settings Class | Provided By | Description |
| :---- | :---- | :---- | :---- | :---- |
| .NET CLI | DotNetBuild | DotNetBuildSettings | Cake.Common | Compiles a.NET project/solution using dotnet build. |
| .NET CLI | DotNetTest | DotNetTestSettings | Cake.Common | Runs tests using dotnet test. |
| .NET CLI | DotNetPack | DotNetPackSettings | Cake.Common | Creates NuGet packages using dotnet pack. |
| .NET CLI | DotNetPublish | DotNetPublishSettings | Cake.Common | Publishes a.NET application using dotnet publish. |
| .NET CLI | DotNetNuGetPush | DotNetNuGetPushSettings | Cake.Common | Pushes NuGet packages using dotnet nuget push. |
| MSBuild | MSBuild | MSBuildSettings | Cake.Common | Builds using MSBuild.exe. |
| NuGet CLI | NuGetPack | NuGetPackSettings | Cake.Common | Creates NuGet packages using nuget.exe pack. Requires nuget.exe. |
| NuGet CLI | NuGetPush | NuGetPushSettings | Cake.Common | Pushes NuGet packages using nuget.exe push. Requires nuget.exe. |
| xUnit | XUnit2 | XUnit2Settings | Cake.Common | Runs xUnit tests. Requires xunit.runner.console tool. |
| NUnit | NUnit3 | NUnit3Settings | Cake.Common | Runs NUnit tests. Requires NUnit.ConsoleRunner tool. |
| Git | GitClone | GitCloneSettings | Cake.Git Addin | Clones a Git repository using LibGit2Sharp. |
| Git | GitCommit | N/A (args in alias) | Cake.Git Addin | Creates a Git commit using LibGit2Sharp. |
| GitVersion | GitVersion | GitVersionSettings | Cake.GitVersion | Determines semantic version from Git history. Requires GitVersion.Tool. |
| Docker | DockerBuild | DockerImageBuildSettings | Cake.Docker Addin | Builds a Docker image using docker build. |
| Docker | DockerRun | DockerContainerRunSettings | Cake.Docker Addin | Runs a Docker container using docker run. |
| Docker | DockerPush | DockerImagePushSettings | Cake.Docker Addin | Pushes a Docker image using docker push. |
| Process Execution | StartProcess | ProcessSettings | Cake.Common | Starts an arbitrary external process. |

*(Note: This table lists common examples. Many more aliases exist within Cake.Common and various community Addins. Refer to official documentation and Addin pages for complete lists.)*

### **3.3. File System Operations**

Manipulating files and directories is fundamental to most build processes. Cake provides a comprehensive set of aliases for these operations, primarily through the Cake.Common.IO namespace, accessible via the build context.7

* **Common Operations:**  
  * **Cleaning/Deleting:**  
    * context.CleanDirectory(path): Deletes the contents of a directory, but not the directory itself.7 Handles read-only files.20  
    * context.DeleteDirectory(path, recursive: true): Deletes a directory and its contents.  
    * context.DeleteFile(path): Deletes a specific file.23  
  * **Copying/Moving:**  
    * context.CopyFile(sourcePath, destinationPath): Copies a file.23  
    * context.CopyDirectory(sourcePath, destinationPath): Copies a directory and its contents.  
    * context.MoveFile(sourcePath, destinationPath): Moves a file.  
    * context.MoveDirectory(sourcePath, destinationPath): Moves a directory.  
  * **Creating/Ensuring Existence:**  
    * context.EnsureDirectoryExists(path): Creates a directory if it doesn't already exist.41  
    * context.DirectoryExists(path) / context.FileExists(path): Checks for the existence of a directory or file.  
* **Working with Paths:** Cake uses specific types, DirectoryPath and FilePath, to represent paths, providing type safety and helper methods.  
  * **Creation:** Paths are typically created using context.Directory("./relative/path/") or context.File("./relative/path/file.txt").7 Absolute paths can also be used.  
  * **Combination:** Paths can be combined to create new paths. While the \+ operator might seem intuitive 7, it's generally safer and more explicit to use combination methods like sourceDirectory.Combine("subdir") or sourceDirectory.CombineWithFilePath("file.txt").19  
* **Path Combination Nuances:** It's important to be mindful when combining paths in Frosting. A subtle issue can arise when using the \+ operator with different path-related types (e.g., DirectoryPath and ConvertableDirectoryPath). Due to implicit conversions to string and potentially missing operator overloads, the \+ operator might perform simple string concatenation instead of proper path joining, leading to incorrect paths (e.g., "../" \+ "temp" becoming "..temp" instead of "../temp") and subsequent runtime errors.25 Using explicit methods like .Combine() or .CombineWithFilePath() avoids this ambiguity by directly invoking Cake's path manipulation logic, resulting in more robust and predictable behavior.19 This highlights the need to understand Cake's specific path types even when working within the familiar C\# environment of Frosting.7  
* **Globbing:** Cake provides powerful pattern matching (globbing) to find multiple files or directories:  
  * context.GetFiles(pattern): Returns an IEnumerable\<FilePath\> matching the pattern.23  
  * context.GetDirectories(pattern): Returns an IEnumerable\<DirectoryPath\> matching the pattern.  
  * **Syntax:** Globbing patterns use wildcards like \* (matches any characters except path separators within a single directory level), \*\* (matches any characters across multiple directory levels), and ? (matches a single character). Examples: "./src/\*\*/\*.cs" finds all C\# files in src and its subdirectories; "./artifacts/\*.nupkg" finds all NuGet packages directly within the artifacts directory.7  
* **Advanced Operations:** For more complex file manipulations like searching text within files, performing regex replacements, or appending lines, the Cake.FileHelpers community Addin offers specialized aliases.38

## **4\. Managing State and Configuration: The Build Context Revisited**

The BuildContext is the primary vehicle for passing configuration and sharing state between tasks in Cake Frosting. As build complexity increases, managing the context effectively becomes crucial.

### **4.1. Passing Data via BuildContext Properties**

The most straightforward method for managing configuration is adding public properties to a custom BuildContext class.9

* **Initialization:** These properties are typically initialized within the BuildContext constructor. Common sources for initialization values include:  
  * Command-line arguments parsed using context.Argument\<T\>("argumentName", "defaultValue") or context.Arguments.HasArgument("argumentName").9  
  * Environment variables read using context.Environment.GetEnvironmentVariable("VAR\_NAME").  
  * Hardcoded defaults or values derived from the build environment (e.g., detecting the OS using context.Environment.Platform.IsUnix()).  
* **Access:** Tasks access these values directly through the context instance passed to their Run or RunAsync methods (e.g., context.MsBuildConfiguration, context.ArtifactsDirectory).9

This approach works well for a limited number of simple configuration values.

### **4.2. Single vs. Multiple Contexts: Clarifying the Structure**

A potential point of confusion is whether Frosting supports using multiple different BuildContext *types* within a single build run. The design of Cake Frosting centers around registering *one specific context type* for the entire build execution via the CakeHost.UseContext\<TContext\>() method.9 The system is not designed to dynamically switch between different classes inheriting from FrostingContext during a single invocation of CakeHost.Run().

Therefore, the practical challenge is not about using multiple context *types*, but rather how to effectively structure and manage the potentially large amount of state and configuration *within* that single chosen BuildContext type as the build grows in complexity.

### **4.3. Structuring Complex Contexts: Beyond Simple Properties**

As the number of build parameters, configurations, and shared state elements increases, placing them all directly as properties on the main BuildContext class can lead to a large, difficult-to-manage class (sometimes referred to as the "God Object" anti-pattern). Frosting offers better patterns leveraging standard C\# practices.

* **Solution 1: Composition:** Instead of adding every property directly to BuildContext, group related settings into separate plain C\# classes. Then, include instances of these settings classes as properties within the main BuildContext. This promotes better organization. For example, a PublishSettings class could hold NuGetApiKey, TargetFeedUrl, etc., and the BuildContext would have a public PublishSettings PublishConfig { get; set; } property. This pattern is implicitly suggested in examples where helper classes like BuildParameters are used to encapsulate arguments.31  
* **Solution 2: Dependency Injection (DI):** Frosting integrates with standard.NET dependency injection. This provides a powerful mechanism for managing complex configurations and services.  
  * **Registration:** Use the CakeHost.ConfigureServices(services \=\> {... }) method in Program.cs to register custom configuration objects or services, typically as singletons.31 These objects can encapsulate parsed arguments, environment settings, or instances of utility classes.  
  * **Injection:** Add parameters for these registered services to the constructor of your custom BuildContext class. The DI container managed by CakeHost will automatically provide the registered instances when creating the BuildContext.31  
  * **Access:** Tasks access the injected services and their properties through the BuildContext instance (e.g., context.BuildParameters.Configuration, context.MyCustomService.DoSomething()).31  
* **DI as the Recommended Scalability Pattern:** For non-trivial builds, Dependency Injection emerges as the preferred pattern for managing context complexity in Frosting. It avoids cluttering the BuildContext class itself with numerous properties. By registering and injecting dedicated configuration objects or services, it promotes modularity – related settings are grouped logically. Furthermore, it significantly improves the testability of the build logic, as these injected dependencies can be easily mocked or replaced in unit tests for tasks or the context itself. Examples clearly show this pattern being used to inject pre-parsed command-line arguments (using external libraries like System.CommandLine) 31 or even core Cake services like ICakeEngine.58 This effectively addresses the concern of a "single large context" by providing a structured way to compose the context's capabilities from smaller, focused, injectable components, leveraging familiar.NET DI practices.  
* **Solution 3: Typed Context (Cake Script Feature):** Cake Script (.cake files) offers a different mechanism called "Typed Context" using Setup\<T\>(...) to return a data object and Does\<T\>(data \=\>...) or WithCriteria\<T\>((context, data) \=\>...) to access it in tasks.22 While this achieves a similar goal of sharing strongly-typed state, it's a distinct feature of the DSL runner. Frosting achieves state sharing primarily through the single BuildContext class, enhanced by composition and dependency injection.

### **4.4. Advanced Context Scenarios**

The build context facilitates more advanced build logic:

* **Conditional Task Execution:** Tasks can override the ShouldRun(TContext context) method. This method returns a boolean indicating whether the task's Run method should execute based on logic evaluated against the context (e.g., checking a configuration flag, environment variable, or build system property).21 This provides more dynamic control than static dependency attributes alone.  
* **Sharing State Across Tasks:** The context can act as a shared blackboard. For instance, tasks could add information (like paths to generated artifacts or error details) to collections within the context, which subsequent tasks or the teardown logic can then read and act upon.33  
* **Integrating External Parsing:** As mentioned, DI allows seamless integration with sophisticated argument parsing libraries like System.CommandLine. The main application entry point can parse arguments using such a library, register the results as a service, and inject them into the BuildContext for type-safe access within tasks.31

## **5\. Structuring Cake Frosting Projects**

Organizing a Cake Frosting project effectively is key to maintainability, especially as the build logic grows. Frosting encourages standard.NET project structuring practices.

### **5.1. Basic Project Layout**

A typical Frosting project follows a standard.NET Console Application structure 7:

* **Build Project Directory:** Often named build or Build, located within the solution structure (e.g., at the root or in a dedicated build folder).7 This directory contains:  
  * The C\# project file (.csproj) defining dependencies (Cake.Frosting, Addins) and build settings.  
  * C\# source files (.cs) containing the Program class, BuildContext class, and task definitions.  
* **Bootstrapper Scripts:** build.ps1 (PowerShell) and build.sh (Bash) scripts are typically placed in the repository root.7 These provide a consistent entry point for invoking the build across different environments.  
* **Entry Point (Program.cs):** This file contains the Main method, which is the application entry point. Its primary responsibility is to configure and run the CakeHost.7 Configuration typically involves:  
  * Specifying the BuildContext type (.UseContext\<BuildContext\>()).  
  * Registering lifetime hooks (.UseLifetime\<T\>, .UseTaskLifetime\<T\>).  
  * Registering services for DI (.ConfigureServices(...)).  
  * Installing necessary tools (.InstallTool(...)).  
  * Registering assemblies containing shared tasks (.AddAssembly(...)).  
  * Registering CI integration modules (.UseModule\<T\>()).  
  * Finally, calling .Run(args) to start the build execution engine.  
* **Task Files:** While tasks can be defined in Program.cs, it's best practice to place each task or groups of related tasks in separate .cs files for better organization.

### **5.2. Organizing Tasks**

Within the build project:

* **Namespaces/Folders:** Use C\# namespaces and corresponding folder structures to group tasks logically (e.g., Tasks.Compile, Tasks.Test, Tasks.Deploy).  
* **Naming:** Employ clear and descriptive names for tasks using the \`\` attribute. Follow consistent naming conventions (e.g., Verb-Noun like Compile-Solution, Run-Unit-Tests).  
* **Default Target:** Define a task named Default (or configure a different default via settings). This task typically depends on the final task of the main build pipeline (e.g., Test or Package) and often has no execution logic itself.9 This provides a simple entry point (./build.sh) to run the entire standard build.

### **5.3. Reusing Build Logic Across Projects**

One of the significant advantages of Frosting's C\#-native approach is the ability to easily share and reuse build logic across multiple repositories or projects.

* **Shared Task Libraries:** Common build tasks (e.g., standardized compilation, testing, code analysis, deployment steps) can be implemented as FrostingTask\<TContext\> classes within a separate.NET class library project.34 This library can define its own specific context requirements if needed, potentially inheriting from a base context.  
* **Distribution:** These shared libraries are packaged and distributed using standard.NET practices, typically as NuGet packages hosted on NuGet.org, GitHub Packages, or internal feeds like Azure Artifacts.34  
* **Consumption:** Consuming projects add a \<PackageReference\> to the shared task library's NuGet package in their Frosting build project's .csproj file. Then, in Program.cs, they register the assembly containing the shared tasks with the CakeHost using the .AddAssembly(Assembly.GetAssembly(typeof(SharedTaskType))) extension method.19 This makes the tasks defined in the library discoverable and usable within the consuming build project's dependency graph.  
* **Modularity and Maintainability Benefits:** This capability for packaging and reusing tasks via standard.NET libraries and NuGet is a powerful feature for maintainability, particularly in organizations with multiple software projects.34 It allows development teams to define and enforce standardized build steps across different applications, promoting the DRY (Don't Repeat Yourself) principle. Updates to common build logic can be made in the central library, versioned, and distributed via NuGet, simplifying maintenance compared to managing potentially duplicated code across multiple Cake DSL scripts.7 Real-world examples like ap0llo/shared-build demonstrate this pattern, encapsulating complex build logic (versioning, testing, packaging, release management) into a reusable NuGet package (Grynwald.SharedBuild) consumed by other projects.61 This leverages the strengths of the.NET ecosystem for code sharing, a distinct advantage over DSL-based approaches where reuse often involves less robust methods like file copying or \#load directives.

### **5.4. Integrating with CI/CD Systems**

Cake Frosting projects integrate well with various Continuous Integration and Continuous Delivery (CI/CD) platforms due to Cake's cross-platform nature.2

* **Basic Integration:** Frosting projects are standard.NET console applications, so CI pipelines simply need to restore dependencies (dotnet restore), build the Frosting project (dotnet build), and then execute the compiled application (dotnet run \--project build/build.csproj \-- \--target=CI-Target...) or use the bootstrapper scripts.  
* **Enhanced Integration via Modules:** For richer integration, the Cake.BuildSystems.Module package provides specific modules for platforms like Azure Pipelines, TeamCity, AppVeyor, Travis CI, and GitLab CI.60 These modules automatically detect the CI environment and enhance the build output and integration without requiring script changes. Features include:  
  * Logging task execution as distinct steps in the CI system's UI.  
  * Reporting build progress.  
  * Integrating Cake's warning/error logs with the CI system's build issue reporting.  
  * Providing build summary information specific to Cake tasks.60  
  * **Frosting Setup:** Unlike Cake Script which loads modules automatically from the tools/Modules folder, Frosting requires explicit registration. First, add a \<PackageReference\> to Cake.BuildSystems.Module in the build project's .csproj. Then, in Program.cs, register the desired modules using CakeHost.UseModule\<AzurePipelinesModule\>(), UseModule\<TeamCityModule\>(), etc. All modules can be registered; they will only activate if the corresponding CI environment is detected.60  
* **GitHub Actions:** A dedicated action, cake-build/cake-action, simplifies running Cake builds (both Script and Frosting) in GitHub Actions workflows.62 It handles:  
  * Setting up the required.NET SDK.  
  * Locating the script (script-path) or Frosting project (project-path).  
  * Specifying the target task (target).  
  * Setting verbosity (verbosity).  
  * Passing custom arguments (arguments).  
  * Restoring.NET local tools (including Cake itself) if a tool manifest (dotnet-tools.json) is present (cake-version: tool-manifest).62  
* **Complex Pipeline Example:** The ap0llo/shared-build project 61 serves as an advanced example of a reusable Frosting build pipeline designed for CI/CD. It integrates:  
  * Semantic versioning using GitVersion.  
  * Automated changelog generation.  
  * Compilation, testing, and code coverage reporting (using ReportGenerator).  
  * NuGet packaging and publishing to multiple feeds (Azure Artifacts).  
  * GitHub Release creation (including draft releases for main branch builds).  
  * GitHub milestone management (creating milestones, assigning PRs).  
  * Integration with both Azure Pipelines and GitHub Actions, demonstrating conditional logic based on the CI environment.61

## **6\. Common Tools and Addins**

The power of Cake lies not only in its core engine but also in its vast ecosystem of built-in aliases and community-provided Addins that integrate external tools.

### **6.1. Essential Built-in Aliases (Recap)**

These aliases are part of Cake.Common and are available by default in Frosting projects:

* **.NET CLI (Cake.Common.Tools.DotNet):** DotNetBuild, DotNetTest, DotNetPack, DotNetPublish, DotNetClean, DotNetRun, DotNetNuGetPush, etc..9  
* **MSBuild (Cake.Common.Tools.MSBuild):** MSBuild.27  
* **NuGet CLI (Cake.Common.Tools.NuGet):** NuGetPack, NuGetPush, NuGetRestore, NuGetInstall. **Important:** These aliases require the nuget.exe command-line tool to be available, which is often not present by default, especially in CI environments or with only the.NET SDK installed. Using the DotNet\*\*\* equivalents (like DotNetPack, DotNetNuGetPush) is generally recommended for modern.NET projects as they rely only on the dotnet CLI.17  
* **Test Runners (Cake.Common.Tools.XUnit, Cake.Common.Tools.NUnit):** XUnit2, NUnit3. These require the respective console runner tools (xunit.runner.console, NUnit.ConsoleRunner) to be installed, often via CakeHost.InstallTool or a tool manifest.50  
* **Process Execution (Cake.Common.Tools):** StartProcess for running arbitrary external commands.  
* **File System (Cake.Common.IO):** CleanDirectory, CopyFile, DeleteDirectory, GetFiles, etc..7

### **6.2. Popular Community Addins**

The Cake ecosystem thrives on community contributions. Addins provide aliases for a vast array of tools and services. Some popular examples include:

* **Git (Cake.Git / Cake.Frosting.Git):** Provides aliases for interacting with Git repositories directly using LibGit2Sharp (e.g., GitClone, GitCommit, GitTag, GitBranch, GitLog, GitCheckout, GitFetch, GitPull, GitPush).39 Note that the Cake.Git addin historically had limitations running only on x64 processors.39 Projects like ap0llo/shared-build utilize Cake.Frosting.Git.61  
* **GitVersion (Cake.GitVersion):** Integrates the GitVersion tool to calculate semantic version numbers based on Git history and branching strategies.63 Requires the GitVersion.Tool.NET tool to be installed.73  
* **Docker (Cake.Docker):** Offers extensive aliases for managing Docker images and containers (DockerBuild, DockerRun, DockerPush, DockerPull, DockerTag, DockerComposeUp, etc.).44  
* **File Helpers (Cake.FileHelpers):** Extends file system operations with aliases for text/regex searching and replacement within files.38  
* **CI/CD Integration (Cake.BuildSystems.Module, Cake.AzureDevOps, etc.):** Provide deeper integration with specific CI/CD platforms beyond basic execution.60  
* **Code Coverage (Cake.Coverlet, Cake.ReportGenerator):** Aliases to run code coverage analysis (e.g., using Coverlet) and generate reports (often invoking the ReportGenerator tool).13  
* **Code Analysis (Cake.Sonar, Cake.ReSharperReports):** Aliases for integrating with static analysis tools like SonarQube or JetBrains ReSharper Command Line Tools.19 These often require the corresponding external tool to be installed.

### **6.3. Installing Tools and Addins in Frosting**

Integrating external dependencies follows standard.NET practices in Frosting:

* **Addins (Provide Aliases):** Addins are typically distributed as NuGet packages. To use an Addin, add a \<PackageReference\> to the Addin's NuGet package in the Frosting build project's .csproj file.3 For example:  
  XML  
  \<ItemGroup\>  
    \<PackageReference Include\="Cake.Frosting" Version\="5.0.0" /\>  
    \<PackageReference Include\="Cake.Git" Version\="5.0.1" /\>  
    \<PackageReference Include\="Cake.Docker" Version\="1.2.1" /\>  
    \</ItemGroup\>  
  After restoring packages, the aliases provided by the Addin become available as extension methods on the BuildContext.  
* **Tools (External Executables):** Many aliases wrap external command-line tools that need to be installed separately. Frosting provides mechanisms to manage these:  
  * **CakeHost.InstallTool:** In Program.cs, use the .InstallTool(new Uri("nuget:?package=PackageName\&version=X.Y.Z")) method on the CakeHost builder. This instructs Cake to ensure the specified.NET tool (or potentially a NuGet package containing an executable) is installed and available during the build execution.7  
  * **.NET Tool Manifest:** Alternatively, define tool dependencies in a standard .config/dotnet-tools.json manifest file within the repository. The tools can then be restored using dotnet tool restore. CI integrations like cake-build/cake-action can automatically handle this restoration step if configured (cake-version: tool-manifest).62 This approach aligns with standard.NET tool management practices.

## **7\. Conclusion and Best Practices**

Cake Frosting offers a robust, modern, and maintainable approach to automating.NET build processes. By leveraging the power and familiarity of C\# and integrating seamlessly with the extensive Cake ecosystem, it provides a compelling alternative to traditional DSL-based build scripts, especially for complex projects and teams prioritizing standard development practices.

### **7.1. Summary of Cake Frosting Advantages**

* **Leverages C\# Ecosystem:** Utilizes existing C\# developer skills, IDEs (Visual Studio, Rider, VS Code) for IntelliSense, debugging, and refactoring, and standard.NET libraries.2  
* **Compile-Time Safety:** Strong typing catches errors during compilation, reducing runtime failures in the build process.7  
* **Maintainability & Modularity:** C\# classes and standard project structures enhance organization and maintainability, particularly for large or evolving build scripts.7  
* **Code Reuse:** Facilitates excellent code reuse through the creation of shared task libraries distributed via NuGet, promoting DRY principles across projects.34  
* **Extensibility:** Full access to the rich Cake ecosystem of Aliases, Addins, and Modules for integrating a wide range of tools and services.

### **7.2. Key Recommendations**

Based on the analysis of Frosting's features and common practices:

1. **Manage Context Complexity with DI:** For builds with significant configuration or shared state, structure the BuildContext using Dependency Injection. Register configuration objects or services with ConfigureServices and inject them into the BuildContext constructor. Avoid creating overly large context classes with numerous direct properties.  
2. **Organize Tasks Logically:** Place task classes in separate files and use namespaces or folders for grouping within the build project. Use clear \`\` attributes.  
3. **Embrace Reusability:** Identify common build patterns or tasks used across multiple projects and extract them into shared.NET class libraries distributed as NuGet packages. Consume these using \<PackageReference\> and AddAssembly.  
4. **Utilize Aliases:** Prefer using Cake's built-in and Addin-provided aliases for interacting with tools and the file system over manual process execution or file IO, leveraging their type safety and abstraction.  
5. **Ensure Tool Reproducibility:** If build environment consistency is critical, explicitly specify tool paths using WithToolPath or ToolPath in the relevant settings objects (e.g., MSBuildSettings) rather than relying solely on automatic tool discovery.  
6. **Enhance CI Feedback:** Use CI integration modules like Cake.BuildSystems.Module (registered via UseModule\<T\>) for more detailed and integrated build reporting in platforms like Azure Pipelines or TeamCity.  
7. **Combine Paths Safely:** Prefer explicit path combination methods like .Combine() or .CombineWithFilePath() over the \+ operator to avoid potential subtle bugs related to implicit string conversions.25

### **7.3. When to Choose Frosting**

Cake Frosting is particularly well-suited for:

* Development teams primarily working with C\# who prefer standard language features and tooling over learning a separate DSL.  
* Complex, long-lived build processes where maintainability, testability, and robust error checking are paramount.  
* Scenarios requiring tight integration with existing C\# libraries or frameworks within the build logic.  
* Organizations aiming to standardize and share build logic components across multiple projects or teams using familiar.NET packaging (NuGet).

### **7.4. Final Thoughts**

Cake Frosting represents a mature and powerful evolution in.NET build automation. By combining the flexibility of Cake's task running engine and alias ecosystem with the robustness and familiarity of native C\# development, it empowers teams to create sophisticated, reliable, and highly maintainable build pipelines tailored to the demands of modern software delivery.

## **8\. Appendix**

### **8.1. Comparison Table: Cake Frosting vs. Cake Script (.NET Tool)**

| Feature | Cake Frosting | Cake Script (.NET Tool) |
| :---- | :---- | :---- |
| **Syntax** | Standard C\# classes | C\# DSL (.cake files) |
| **Tooling/IDE Support** | Full (IntelliSense, Debugging, Refactoring) | Good (Extensions available, but can be less robust) |
| **Type Safety** | Strong (Compile-time checks) | Weaker (More reliance on runtime checks) |
| **Verbosity** | Potentially higher | Generally lower (more concise DSL) |
| **Maintainability (Complex)** | High (Standard C\# structure, DI) | Moderate (Can become harder to manage) |
| **Code Reuse** | Excellent (Standard.NET Libraries, NuGet) | Moderate (\#load directive, file copying) |
| **Learning Curve (C\# Devs)** | Low (Familiar C\#) | Moderate (Requires learning DSL nuances) |

*2*

### **8.2. Common Command-Line Switches for Frosting**

| Switch | Description |
| :---- | :---- |
| \-t, \--target \<TARGET\> | Sets the build task to run (Default: "Default"). |
| \-v, \--verbosity \<LEVEL\> | Sets the log level (Quiet, Minimal, Normal, Verbose, Diagnostic). Default: Normal. |
| \--dryrun | Performs a dry run, showing tasks without executing them. |
| \--tree | Shows the task dependency tree. |
| \-e, \--exclusive | Executes only the specified target task, ignoring dependencies. |
| \--description | Shows descriptions for tasks (requires TaskDescriptionAttribute on task classes). |
| \-w, \--working \<PATH\> | Sets the working directory for the build execution. |
| \--version | Displays the Cake.Frosting version. |
| \-h, \--help | Prints help information. |
| *(Custom Arguments)* | Any unrecognized switches (e.g., \--myArg="value") are passed to the BuildContext. |

*8*

### **8.3. Further Resources**

* **Official Cake Documentation:** [https://cakebuild.net/docs/](https://cakebuild.net/docs/) 1  
* **Cake GitHub Repository:** [https://github.com/cake-build/cake](https://github.com/cake-build/cake) 13  
* **Cake GitHub Discussions:** [https://github.com/cake-build/cake/discussions](https://github.com/cake-build/cake/discussions) 17  
* **Cake NuGet Packages:** [https://www.nuget.org/packages?q=Cake](https://www.nuget.org/packages?q=Cake) 3  
* **Cake Frosting API Reference:** [https://cakebuild.net/api/Cake.Frosting/](https://cakebuild.net/api/Cake.Frosting/) 18  
* **Cake Alias Reference (DSL):** [https://cakebuild.net/dsl/](https://cakebuild.net/dsl/) (Useful for discovering available functionality, even if accessed via context in Frosting) 1

#### **Works cited**

1. Documentation \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/docs/](https://cakebuild.net/docs/)  
2. Cake Build, accessed May 1, 2025, [https://cakebuild.net/](https://cakebuild.net/)  
3. Cake 1.3.0 \- NuGet, accessed May 1, 2025, [https://www.nuget.org/packages/Cake](https://www.nuget.org/packages/Cake)  
4. Cake.Frosting 5.0.0 \- NuGet, accessed May 1, 2025, [https://www.nuget.org/packages/Cake.Frosting](https://www.nuget.org/packages/Cake.Frosting)  
5. GitHub Actions DevOps Pipelines as code using C\# \- Mattias Karlsson \- YouTube, accessed May 1, 2025, [https://www.youtube.com/watch?v=ZP4dJjtKeKA](https://www.youtube.com/watch?v=ZP4dJjtKeKA)  
6. Getting Started \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/docs/getting-started/](https://cakebuild.net/docs/getting-started/)  
7. Cake Frosting: More Maintainable C\# DevOps, accessed May 1, 2025, [http://www.leerichardson.com/2021/02/cake-frosting-more-maintainable-c-devops.html](http://www.leerichardson.com/2021/02/cake-frosting-more-maintainable-c-devops.html)  
8. Cake Frosting \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/docs/running-builds/runners/cake-frosting](https://cakebuild.net/docs/running-builds/runners/cake-frosting)  
9. Setting Up A New Cake Frosting Project, accessed May 1, 2025, [https://cakebuild.net/docs/getting-started/setting-up-a-new-frosting-project](https://cakebuild.net/docs/getting-started/setting-up-a-new-frosting-project)  
10. Setting Up A New Cake .NET Tool Project \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/docs/getting-started/setting-up-a-new-scripting-project](https://cakebuild.net/docs/getting-started/setting-up-a-new-scripting-project)  
11. website/input/docs/getting-started/setting-up-a-new-frosting-project.md at master · cake-build/website \- GitHub, accessed May 1, 2025, [https://github.com/cake-build/website/blob/master/input/docs/getting-started/setting-up-a-new-frosting-project.md](https://github.com/cake-build/website/blob/master/input/docs/getting-started/setting-up-a-new-frosting-project.md)  
12. Upgrade instructions \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/docs/getting-started/upgrade](https://cakebuild.net/docs/getting-started/upgrade)  
13. website/input/blog/2021-02-07-cake-v1.0.0-released.md at master \- GitHub, accessed May 1, 2025, [https://github.com/cake-build/website/blob/master/input/blog/2021-02-07-cake-v1.0.0-released.md](https://github.com/cake-build/website/blob/master/input/blog/2021-02-07-cake-v1.0.0-released.md)  
14. Available templates for dotnet new \- GitHub, accessed May 1, 2025, [https://github.com/dotnet/templating/wiki/Available-templates-for-dotnet-new](https://github.com/dotnet/templating/wiki/Available-templates-for-dotnet-new)  
15. Example for using Cake Frosting \- GitHub, accessed May 1, 2025, [https://github.com/pascalberger/cake-frosting-example](https://github.com/pascalberger/cake-frosting-example)  
16. frosting/src/Cake.Frosting.Example/Program.cs at develop · cake-archive/frosting · GitHub, accessed May 1, 2025, [https://github.com/cake-build/frosting/blob/develop/src/Cake.Frosting.Example/Program.cs](https://github.com/cake-build/frosting/blob/develop/src/Cake.Frosting.Example/Program.cs)  
17. NuGet.exe related documentation · cake-build · Discussion \#3271 \- GitHub, accessed May 1, 2025, [https://github.com/orgs/cake-build/discussions/3271](https://github.com/orgs/cake-build/discussions/3271)  
18. Cake.Frosting Namespace \- Cake \- API, accessed May 1, 2025, [https://cakebuild.net/api/Cake.Frosting](https://cakebuild.net/api/Cake.Frosting)  
19. Using Cake.Frosting.Issues.Recipe, accessed May 1, 2025, [https://cakeissues.net/5.1.1/documentation/usage/recipe/using-cake-frosting-issues-recipe/](https://cakeissues.net/5.1.1/documentation/usage/recipe/using-cake-frosting-issues-recipe/)  
20. cake/ReleaseNotes.md at develop · cake-build/cake \- GitHub, accessed May 1, 2025, [https://github.com/cake-build/cake/blob/develop/ReleaseNotes.md](https://github.com/cake-build/cake/blob/develop/ReleaseNotes.md)  
21. Frosting: How to get a behavior similar to ".WithCriteria()"? · cake-build · Discussion \#2951, accessed May 1, 2025, [https://github.com/orgs/cake-build/discussions/2951](https://github.com/orgs/cake-build/discussions/2951)  
22. Sharing Build State \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/docs/writing-builds/sharing-build-state](https://cakebuild.net/docs/writing-builds/sharing-build-state)  
23. Aliases \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/docs/fundamentals/aliases](https://cakebuild.net/docs/fundamentals/aliases)  
24. website/input/docs/fundamentals/aliases.md at master \- GitHub, accessed May 1, 2025, [https://github.com/cake-build/website/blob/master/input/docs/fundamentals/aliases.md](https://github.com/cake-build/website/blob/master/input/docs/fundamentals/aliases.md)  
25. Cake Frosting Parent DirectoryPath Fails To Combine with Slash · Issue \#3352 \- GitHub, accessed May 1, 2025, [https://github.com/cake-build/cake/issues/3352](https://github.com/cake-build/cake/issues/3352)  
26. DotNetAliases.DotNetTool(ICakeContext, string, DotNetToolSettings) Method \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/api/Cake.Common.Tools.DotNet/DotNetAliases/5BC1D3CF](https://cakebuild.net/api/Cake.Common.Tools.DotNet/DotNetAliases/5BC1D3CF)  
27. Reference \- MSBuild \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/dsl/msbuild/](https://cakebuild.net/dsl/msbuild/)  
28. How to set absolute path to MSBuild.exe in Cake scripts \- Stack Overflow, accessed May 1, 2025, [https://stackoverflow.com/questions/77529122/how-to-set-absolute-path-to-msbuild-exe-in-cake-scripts](https://stackoverflow.com/questions/77529122/how-to-set-absolute-path-to-msbuild-exe-in-cake-scripts)  
29. API \- CakeHostExtensions.UseContext  
30. Setup And Teardown \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/docs/writing-builds/setup-and-teardown](https://cakebuild.net/docs/writing-builds/setup-and-teardown)  
31. How to pass command line arguments to Cake (Frosting), when these arguments are already collected with System.CommandLine? \- Stack Overflow, accessed May 1, 2025, [https://stackoverflow.com/questions/69160190/how-to-pass-command-line-arguments-to-cake-frosting-when-these-arguments-are](https://stackoverflow.com/questions/69160190/how-to-pass-command-line-arguments-to-cake-frosting-when-these-arguments-are)  
32. Frosting \- Manually running tasks ignores IsDependentOn? · cake-build · Discussion \#3541, accessed May 1, 2025, [https://github.com/orgs/cake-build/discussions/3541](https://github.com/orgs/cake-build/discussions/3541)  
33. Cake.Frosting: How to run a task even though a previous task failed? \#3809 \- GitHub, accessed May 1, 2025, [https://github.com/orgs/cake-build/discussions/3809](https://github.com/orgs/cake-build/discussions/3809)  
34. Cake Frosting Tasks in Library · cake-build · Discussion \#3812 \- GitHub, accessed May 1, 2025, [https://github.com/orgs/cake-build/discussions/3812](https://github.com/orgs/cake-build/discussions/3812)  
35. Cake.Sprinkles.Module 2.0.0 \- NuGet, accessed May 1, 2025, [https://www.nuget.org/packages/Cake.Sprinkles.Module](https://www.nuget.org/packages/Cake.Sprinkles.Module)  
36. Cake.Sprinkles.Module 1.0.0 \- NuGet, accessed May 1, 2025, [https://www.nuget.org/packages/Cake.Sprinkles.Module/1.0.0](https://www.nuget.org/packages/Cake.Sprinkles.Module/1.0.0)  
37. Reference \- DotNetCore Global Tool \- Cake, accessed May 1, 2025, [https://cakebuild.net/dsl/dotnetcore-global-tool/](https://cakebuild.net/dsl/dotnetcore-global-tool/)  
38. File Helpers aliases \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/dsl/file-helpers/](https://cakebuild.net/dsl/file-helpers/)  
39. Reference \- Git \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/dsl/git/](https://cakebuild.net/dsl/git/)  
40. Reference \- Git Versioning \- Cake, accessed May 1, 2025, [https://cakebuild.net/dsl/git-versioning/](https://cakebuild.net/dsl/git-versioning/)  
41. How can I do a git clone operation using Cake \- Stack Overflow, accessed May 1, 2025, [https://stackoverflow.com/questions/41048037/how-can-i-do-a-git-clone-operation-using-cake](https://stackoverflow.com/questions/41048037/how-can-i-do-a-git-clone-operation-using-cake)  
42. API \- Cake.Git Namespace \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/api/Cake.Git/](https://cakebuild.net/api/Cake.Git/)  
43. Cake.Git 5.0.1 \- NuGet, accessed May 1, 2025, [https://www.nuget.org/packages/Cake.Git](https://www.nuget.org/packages/Cake.Git)  
44. API \- DockerAliases.DockerBuild(ICakeContext, string) Method \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/api/Cake.Docker/DockerAliases/4AE54A45](https://cakebuild.net/api/Cake.Docker/DockerAliases/4AE54A45)  
45. Reference \- Docker \- Cake, accessed May 1, 2025, [https://cakebuild.net/dsl/docker/](https://cakebuild.net/dsl/docker/)  
46. MihaMarkic/Cake.Docker: Cake AddIn that extends Cake with Docker \- GitHub, accessed May 1, 2025, [https://github.com/MihaMarkic/Cake.Docker](https://github.com/MihaMarkic/Cake.Docker)  
47. cakebuild \- has anyone created a docker container using cake? \- Stack Overflow, accessed May 1, 2025, [https://stackoverflow.com/questions/44232612/has-anyone-created-a-docker-container-using-cake](https://stackoverflow.com/questions/44232612/has-anyone-created-a-docker-container-using-cake)  
48. Cake build NuGetPush throws permission denied \- Stack Overflow, accessed May 1, 2025, [https://stackoverflow.com/questions/52539377/cake-build-nugetpush-throws-permission-denied](https://stackoverflow.com/questions/52539377/cake-build-nugetpush-throws-permission-denied)  
49. Getting errors in Cake.Sonar on Cake Frosting \- Stack Overflow, accessed May 1, 2025, [https://stackoverflow.com/questions/67583415/getting-errors-in-cake-sonar-on-cake-frosting](https://stackoverflow.com/questions/67583415/getting-errors-in-cake-sonar-on-cake-frosting)  
50. Reference \- xUnit \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/dsl/xunit/](https://cakebuild.net/dsl/xunit/)  
51. Epic: Introduce DotNet aliases (synonyms to DotNetCore aliases) · Issue \#3341 · cake-build/cake \- GitHub, accessed May 1, 2025, [https://github.com/cake-build/cake/issues/3341](https://github.com/cake-build/cake/issues/3341)  
52. Change verbosity for cake frosting project \- Stack Overflow, accessed May 1, 2025, [https://stackoverflow.com/questions/73425538/change-verbosity-for-cake-frosting-project](https://stackoverflow.com/questions/73425538/change-verbosity-for-cake-frosting-project)  
53. API \- DotNetAliases.DotNetNuGetPush(ICakeContext, FilePath) Method \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/api/Cake.Common.Tools.DotNet/DotNetAliases/139862C0](https://cakebuild.net/api/Cake.Common.Tools.DotNet/DotNetAliases/139862C0)  
54. MSBuild(ICakeContext, FilePath, MSBuildSettings) Method \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/api/Cake.Common.Tools.MSBuild/MSBuildAliases/C240F0FB](https://cakebuild.net/api/Cake.Common.Tools.MSBuild/MSBuildAliases/C240F0FB)  
55. Passing MSBuild Arguments to Cake Build Script to produce \_PublishedWebsites, accessed May 1, 2025, [https://stackoverflow.com/questions/36745900/passing-msbuild-arguments-to-cake-build-script-to-produce-publishedwebsites](https://stackoverflow.com/questions/36745900/passing-msbuild-arguments-to-cake-build-script-to-produce-publishedwebsites)  
56. Build solution with projects in Net 5/6 and Framework 4.8 (WPF) \#3875 \- GitHub, accessed May 1, 2025, [https://github.com/orgs/cake-build/discussions/3875](https://github.com/orgs/cake-build/discussions/3875)  
57. How do I pass my own custom arguments to build.ps1? \- Stack Overflow, accessed May 1, 2025, [https://stackoverflow.com/questions/40046752/how-do-i-pass-my-own-custom-arguments-to-build-ps1](https://stackoverflow.com/questions/40046752/how-do-i-pass-my-own-custom-arguments-to-build-ps1)  
58. Access to ITaskSetupContext in Frosting · Issue \#3946 · cake-build/cake \- GitHub, accessed May 1, 2025, [https://github.com/cake-build/cake/issues/3946](https://github.com/cake-build/cake/issues/3946)  
59. Call multiple tasks from CLI and pass them to RunTarget · Issue \#2470 · cake-build/cake, accessed May 1, 2025, [https://github.com/cake-build/cake/issues/2470](https://github.com/cake-build/cake/issues/2470)  
60. cake-contrib/Cake.BuildSystems.Module: :cake: A simple Cake module to enhance running from a TF Build environment \- GitHub, accessed May 1, 2025, [https://github.com/cake-contrib/Cake.BuildSystems.Module](https://github.com/cake-contrib/Cake.BuildSystems.Module)  
61. Shared build logic based on Cake.Frosting \- GitHub, accessed May 1, 2025, [https://github.com/ap0llo/shared-build](https://github.com/ap0llo/shared-build)  
62. Cake Action \- GitHub Marketplace, accessed May 1, 2025, [https://github.com/marketplace/actions/cake-action](https://github.com/marketplace/actions/cake-action)  
63. How to use Cake with Github Actions | Gary Woodfine, accessed May 1, 2025, [https://garywoodfine.com/how-to-use-cake-with-github-actions/](https://garywoodfine.com/how-to-use-cake-with-github-actions/)  
64. Reference \- NuGet \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/dsl/nuget/](https://cakebuild.net/dsl/nuget/)  
65. NuGetAliases.NuGetPush(ICakeContext, FilePath, NuGetPushSettings) Method \- Cake \- API, accessed May 1, 2025, [https://cakebuild.net/api/Cake.Common.Tools.NuGet/NuGetAliases/08163C34](https://cakebuild.net/api/Cake.Common.Tools.NuGet/NuGetAliases/08163C34)  
66. API \- NuGetAliases.NuGetPush(ICakeContext, IEnumerable  
67. API \- NuGetAliases Class \- Cake Build, accessed May 1, 2025, [https://www.cakebuild.net/api/Cake.Common.Tools.NuGet/NuGetAliases/](https://www.cakebuild.net/api/Cake.Common.Tools.NuGet/NuGetAliases/)  
68. Pushing Packages From Azure Pipelines To Azure Artifacts Using Cake \- Dave Glick, accessed May 1, 2025, [https://www.daveaglick.com/posts/pushing-packages-from-azure-pipelines-to-azure-artifacts-using-cake](https://www.daveaglick.com/posts/pushing-packages-from-azure-pipelines-to-azure-artifacts-using-cake)  
69. Cake.Common.Tools.XUnit Namespace \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/api/Cake.Common.Tools.XUnit/](https://cakebuild.net/api/Cake.Common.Tools.XUnit/)  
70. Add example documentation to aliases · Issue \#750 · cake-build/cake \- GitHub, accessed May 1, 2025, [https://github.com/cake-build/cake/issues/750](https://github.com/cake-build/cake/issues/750)  
71. Reference \- NUnit \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/dsl/nunit/](https://cakebuild.net/dsl/nunit/)  
72. Reference \- NUnit v3 \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/dsl/nunit-v3/](https://cakebuild.net/dsl/nunit-v3/)  
73. Reference \- GitVersion \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/dsl/gitversion/](https://cakebuild.net/dsl/gitversion/)  
74. API \- DockerAliases.DockerComposeBuild(ICakeContext, DockerComposeBuildSettings, string\[\]) Method \- Cake Build, accessed May 1, 2025, [https://cakebuild.net/api/Cake.Docker/DockerAliases/06F37ACC](https://cakebuild.net/api/Cake.Docker/DockerAliases/06F37ACC)  
75. API \- DockerAliases.DockerComposeUp(ICakeContext, string\[\]) Method \- Cake Build, accessed May 1, 2025, [https://www.cakebuild.net/api/Cake.Docker/DockerAliases/07DC0034](https://www.cakebuild.net/api/Cake.Docker/DockerAliases/07DC0034)  
76. Detecting cancellation in Azure Pipelines · cake-build · Discussion \#3616 \- GitHub, accessed May 1, 2025, [https://github.com/orgs/cake-build/discussions/3616](https://github.com/orgs/cake-build/discussions/3616)