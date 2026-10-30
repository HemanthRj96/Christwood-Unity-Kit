using System.Runtime.CompilerServices;

// Developer regression tests build small scenarios at runtime (for example a non-latching switch).
[assembly: InternalsVisibleTo("UnityWorkshop.Tests.PlayMode")]
