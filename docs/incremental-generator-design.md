# Incremental generator design

## Compilation invalidation tradeoff

Service discovery scans assemblies and, depending on the configured filters, walks base types and implemented interfaces. The scan depends on the Roslyn `Compilation`, which changes on virtually every typed character in an IDE. Treating every new compilation as a changed input would therefore repeat the full discovery scan continuously and could significantly affect editor responsiveness.

To avoid that cost, `CombinedProviderComparer` intentionally compares only the parsed attributed-method model and ignores the accompanying `Compilation`. The discovery pipeline is rerun when the method or its `GenerateServiceRegistrations` or `ScanForTypes` configuration changes, but not for a compilation-only change.

This is an explicit performance-consistency tradeoff:

- Editing only a candidate type may leave IntelliSense and the IDE's generated-code view temporarily stale. For example, a newly added implementation of a scanned interface might not appear immediately.
- Modifying the attributed method or its scan attribute invalidates the cached model and reruns discovery.
- Reloading the project creates fresh IDE generator state.
- A command-line or MSBuild compilation creates a fresh generator driver and scans the current compilation, so build output is not based on the IDE's cached result.

Do not remove `CombinedProviderComparer` or include `Compilation` in its equality comparison without accounting for the cost of running whole-assembly discovery for each intermediate IDE compilation.
