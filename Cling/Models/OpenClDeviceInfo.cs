namespace Cling.Models;

public sealed class OpenClDeviceInfo
{
    public string StatusMessage { get; init; } = string.Empty;
    public string Name { get; init; } = "Unavailable";
    public string Platform { get; init; } = "Unavailable";
    public string Version { get; init; } = "Unavailable";
    public string Vendor { get; init; } = "Unavailable";
    public string Type { get; init; } = "Unavailable";
    public string ComputeUnits { get; init; } = "Unavailable";
    public string Clock { get; init; } = "Unavailable";
    public string AddressSpace { get; init; } = "Unavailable";
    public string MaxWorkGroupSize { get; init; } = "Unavailable";
    public string WorkItemSizes { get; init; } = "Unavailable";
    public string SinglePrecisionFma { get; init; } = "Unavailable";
    public string DoublePrecisionSupport { get; init; } = "Unavailable";
    public string GlobalMemory { get; init; } = "Unavailable";
    public string LocalMemory { get; init; } = "Unavailable";
    public string LocalMemoryType { get; init; } = "Unavailable";
    public string UnifiedMemoryWithHost { get; init; } = "Unavailable";
    public string Endianness { get; init; } = "Unavailable";
    public string EccSupport { get; init; } = "Unavailable";
    public string NativeCharWidth { get; init; } = "Unavailable";
    public string NativeShortWidth { get; init; } = "Unavailable";
    public string NativeIntWidth { get; init; } = "Unavailable";
    public string NativeLongWidth { get; init; } = "Unavailable";
    public string NativeFloatWidth { get; init; } = "Unavailable";
    public string NativeDoubleWidth { get; init; } = "Unavailable";
    public string NativeHalfWidth { get; init; } = "Unavailable";
    public string PreferredCharWidth { get; init; } = "Unavailable";
    public string PreferredShortWidth { get; init; } = "Unavailable";
    public string PreferredIntWidth { get; init; } = "Unavailable";
    public string PreferredLongWidth { get; init; } = "Unavailable";
    public string PreferredFloatWidth { get; init; } = "Unavailable";
    public string PreferredDoubleWidth { get; init; } = "Unavailable";
    public string PreferredHalfWidth { get; init; } = "Unavailable";

    public static OpenClDeviceInfo Unavailable(string statusMessage) => new()
    {
        StatusMessage = statusMessage
    };
}
