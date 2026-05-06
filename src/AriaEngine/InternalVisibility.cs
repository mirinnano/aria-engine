using System.Runtime.CompilerServices;

// Expose internal members to the test project for unit testing (e.g., override entry limits)
[assembly: InternalsVisibleTo("AriaEngine.Tests")]
