# Extraction Guidelines — Private Methods and Collaborator Design

Guidance for deciding when a private method should stay private and when it should become a named collaborator. This knowledge-base copy is the durable home for the rule set that survived the ADR-002 refactor; it applies project-wide even though the initial pressure came from the build host.

## The rule in one sentence

Private methods are for **local mechanics and narrative flow**. Extract when code carries **behavior, policy, algorithm, or a concept that deserves a name**.

## Private methods are good when

### 1. Narrative helpers — making a public method read like a story

```csharp
public async Task SyncAsync()
{
    var customers = await FetchCustomersAsync();
    var eligible = FilterEligible(customers);
    await PublishChangesAsync(eligible);
}
```

The private methods are chapter headings. The orchestration story is clear without jumping to other files.

### 2. Local invariants — internal hygiene that no other class should care about

```csharp
private void EnsureNotDisposed()
{
    if (_disposed)
        throw new ObjectDisposedException(nameof(Connection));
}
```

This belongs to the class. Extracting it would spread internal state knowledge.

### 3. Small technical details — repeated mechanics with no domain meaning

```csharp
private static string NormalizePath(string path)
{
    return path.Replace('\\', '/').TrimEnd('/');
}
```

But watch the threshold: if this becomes a domain concept (e.g., `RepositoryPath` value object), extract it.

### 4. Public API hygiene — not everything should be a promise

`private` means "I make no backwards-compatibility guarantees about this." That is valuable. Not every extracted class needs to be `public`.

## Private methods are a smell when

### 1. They call each other in chains

```csharp
public Task Handle() => ProcessAsync();
private async Task ProcessAsync() { var data = await LoadDataAsync(); await ProcessDataAsync(data); }
private Task ProcessDataAsync(Data data) { var enriched = Enrich(data); return SaveAsync(enriched); }
```

This is a call-stack labyrinth. The class has become a workflow engine. Extract the workflow steps into named collaborators or explicit stages; do not introduce a generic `Pipeline` wrapper.

### 2. They contain business rules, build policy, or branching algorithms

```csharp
private bool IsEligibleForLicenseSkip(string libraryName)
{
    return libraryName.StartsWith("sdl") &&
           !libraryName.Contains("gfx") &&
           _manifest.Exclusions.Contains(libraryName);
}
```

This is build-domain policy hiding in a private method. It cannot be tested independently, reused, or discovered. Extract to a named policy class.

### 3. They take many parameters

```csharp
private Price CalculatePrice(Customer c, Product p, Coupon co, Region r, DateTime now, bool includeTax)
```

This is a class asking to be born. The parameter list IS the constructor.

### 4. They touch too much internal state

```csharp
private void ApplyDiscount()
{
    _total -= _discount;
    _discountApplied = true;
    _events.Add(...);
    _auditLog.Add(...);
}
```

Temporal coupling: "call this before that, then that." The class is a state machine without explicit states.

### 5. You want to test them separately

If you think "this algorithm is important enough to deserve its own tests," it is not a private method. Extract it.

The test is not "can I test it through the public API?" The test is "do I feel the urge to test this independently?" If yes, extract.

### 6. They have generic, meaningless names

`Process`, `Handle`, `Do`, `Execute`, `Manage`, `Prepare`, `Build`, `Run`, `Validate` (without a noun) — these are folding regions, not abstractions. They hide code without naming a concept.

Good names name a concept: `CollectLicenses`, `NormalizeDependencyRange`, `ResolveBinaryClosure`.

## Decision tree

The numeric thresholds below are smell thresholds, not hard rules. They are prompts to stop and think, not automatic extraction commands.

```text
Private method:
|
+- Contains business rule / build policy / branching algorithm?
|  -> Extract to named class (policy, validator, service)
|
+- Takes 5+ parameters?
|  -> Extract to class; parameters are the constructor
|
+- Mutates 3+ fields?
|  -> Suspicious. Consider extracting a state object or collaborator
|
+- Called from multiple public methods in the same class?
|  -> Fine if pure mechanics. Suspicious if it carries behavior.
|
+- Would I write independent tests for this?
|  -> Extract. Private methods are tested through their public caller.
|
+- Generic name (Process, Handle, Do, Execute)?
|  -> Rename or extract. Names should earn their place.
|
+- Under ~15 lines, no branching, no dependencies?
|  -> Fine as private. It is local mechanics.
|
+- Makes the public method read like a story outline?
   -> Fine as private. It is a narrative helper.
```

## What to extract to

| What it is | Extract to |
|---|---|
| Build-domain rule or decision | `*Policy` or `*Validator` |
| Algorithm with branching | `*Calculator`, `*Normalizer`, `*Resolver` |
| IO or tool boundary | `*Adapter`, `*Invoker`, `*Client` |
| File-backed state | `*Repository` |
| Multi-step workflow within a task | Named collaborator(s), not a chain of private methods |
| Data shape with validation | `record` or `readonly record struct` |

## Anti-pattern: enterprise cosplay

Not every extracted class needs an interface. Not every helper needs to be a class.

Do not create:

```text
IEmailNormalizer
EmailNormalizer
IEmailNormalizationStrategy
DefaultEmailNormalizationStrategy
EmailNormalizationOptions
EmailNormalizationResultFactory
```

This is a one-liner (`Trim().ToLowerInvariant()`) cosplaying as a NATO mission. A private method or at most a concrete `sealed class` is fine.

## Interface rule (restated from ADR-002)

Create an interface when:

- Multiple production implementations exist
- The seam is expensive or process/tool-backed
- The contract is important enough that tests and tasks should depend on it
- The interface expresses a real axis of change

Otherwise, `sealed class` without an interface is the default.

## LLM-specific note

LLMs tend to generate "private method soup" — many small private methods with generic names that act as folding regions rather than abstractions. This creates classes that look clean on first read but hide behavior in un-testable, un-discoverable private call chains.

Prompt guidance for LLMs working in this repo:

> Prefer small appropriately scoped sealed classes with explicit collaborators over large classes with many private helper methods. Use private methods only for local implementation details or to improve readability of a single public operation. If a private method contains business rules, branching-heavy logic, algorithmic behavior, or needs separate tests, extract it into a named class, policy, or service instead. Do not create interfaces unless there are multiple implementations, external boundaries, decorators, or clear testability benefits.

## Task-specific default

For build-host task classes specifically:

- Task classes own orchestration. They should read like a build story.
- Private methods inside a task are acceptable for narrative flow and local mechanics.
- When orchestration steps grow beyond ~15 lines or contain branching, extract them.
- A task with 5+ private methods that each contain logic is a signal the task body should be collaborating with named services.