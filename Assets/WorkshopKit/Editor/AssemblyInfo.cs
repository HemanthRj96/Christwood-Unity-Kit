using System.Runtime.CompilerServices;

// Developer regression tests simulate adoption failures through an internal hook.
[assembly: InternalsVisibleTo("UnityWorkshop.Tests.Editor")]
