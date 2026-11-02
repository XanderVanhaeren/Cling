using Cling.Models;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Cling.Services;

public sealed class OpenClDeviceService
{
    private const string OpenClLibrary = "OpenCL.dll";

    private const int ClSuccess = 0;
    private const int ClDeviceNotFound = -1;
    private const int ClPlatformNotFoundKhr = -1001;

    private const ulong ClDeviceTypeDefault = 1UL << 0;
    private const ulong ClDeviceTypeCpu = 1UL << 1;
    private const ulong ClDeviceTypeGpu = 1UL << 2;
    private const ulong ClDeviceTypeAccelerator = 1UL << 3;
    private const ulong ClDeviceTypeCustom = 1UL << 4;
    private const ulong ClDeviceTypeAll = 0xFFFF_FFFFUL;

    private const ulong ClFpFma = 1UL << 4;

    private const uint ClPlatformName = 0x0902;

    private const uint ClDeviceType = 0x1000;
    private const uint ClDeviceMaxComputeUnits = 0x1002;
    private const uint ClDeviceMaxWorkItemDimensions = 0x1003;
    private const uint ClDeviceMaxWorkGroupSize = 0x1004;
    private const uint ClDeviceMaxWorkItemSizes = 0x1005;
    private const uint ClDevicePreferredVectorWidthChar = 0x1006;
    private const uint ClDevicePreferredVectorWidthShort = 0x1007;
    private const uint ClDevicePreferredVectorWidthInt = 0x1008;
    private const uint ClDevicePreferredVectorWidthLong = 0x1009;
    private const uint ClDevicePreferredVectorWidthFloat = 0x100A;
    private const uint ClDevicePreferredVectorWidthDouble = 0x100B;
    private const uint ClDeviceMaxClockFrequency = 0x100C;
    private const uint ClDeviceAddressBits = 0x100D;
    private const uint ClDeviceSingleFpConfig = 0x101B;
    private const uint ClDeviceGlobalMemorySize = 0x101F;
    private const uint ClDeviceLocalMemoryType = 0x1022;
    private const uint ClDeviceLocalMemorySize = 0x1023;
    private const uint ClDeviceErrorCorrectionSupport = 0x1024;
    private const uint ClDeviceLittleEndian = 0x1026;
    private const uint ClDeviceName = 0x102B;
    private const uint ClDeviceVendor = 0x102C;
    private const uint ClDeviceVersion = 0x102F;
    private const uint ClDeviceDoubleFpConfig = 0x1032;
    private const uint ClDevicePreferredVectorWidthHalf = 0x1034;
    private const uint ClDeviceHostUnifiedMemory = 0x1035;
    private const uint ClDeviceNativeVectorWidthChar = 0x1036;
    private const uint ClDeviceNativeVectorWidthShort = 0x1037;
    private const uint ClDeviceNativeVectorWidthInt = 0x1038;
    private const uint ClDeviceNativeVectorWidthLong = 0x1039;
    private const uint ClDeviceNativeVectorWidthFloat = 0x103A;
    private const uint ClDeviceNativeVectorWidthDouble = 0x103B;
    private const uint ClDeviceNativeVectorWidthHalf = 0x103C;
    private const uint ClDeviceOpenClCVersion = 0x103D;

    private const uint ClLocalMemory = 1;
    private const uint ClGlobalMemory = 2;

    public Task<IReadOnlyList<OpenClDeviceInfo>> GetAvailableDevices()
    {
        IReadOnlyList<OpenClDeviceInfo> result = GetAvailableDevicesCore();
        return Task.FromResult(result);
    }

    private static IReadOnlyList<OpenClDeviceInfo> GetAvailableDevicesCore()
    {
        try
        {
            var discoveredDevices = EnumerateDevices();

            var devices = discoveredDevices
                .Where(device => device.IsCpu || device.IsGpu)
                .OrderByDescending(device => device.IsGpu)
                .ThenBy(device => device.Info.Vendor)
                .ThenBy(device => device.Info.Name)
                .Select(device => device.Info)
                .ToList();

            if (!discoveredDevices.Any(device => device.IsCpu))
            {
                devices.Add(CreateHostCpuInfo());
            }

            return devices;
        }
        catch (DllNotFoundException)
        {
            return
            [
                CreateHostCpuInfo(
                    "OpenCL.dll could not be found. Install an OpenCL-capable GPU or CPU driver.")
            ];
        }
        catch (EntryPointNotFoundException)
        {
            return
            [
                CreateHostCpuInfo(
                    "The installed OpenCL loader does not expose the required entry points.")
            ];
        }
        catch (BadImageFormatException)
        {
            return
            [
                CreateHostCpuInfo(
                    "The OpenCL loader architecture does not match the application architecture.")
            ];
        }
        catch (OpenClException exception)
        {
            return
            [
                CreateHostCpuInfo(
                    $"OpenCL query failed with error {exception.ErrorCode}.")
            ];
        }
    }

    private static List<DiscoveredDevice> EnumerateDevices()
    {
        var status = clGetPlatformIDs(0, null, out var platformCount);

        if (status == ClPlatformNotFoundKhr || platformCount == 0)
        {
            return [];
        }

        Check(status, "clGetPlatformIDs");

        var platforms = new IntPtr[checked((int)platformCount)];
        Check(
            clGetPlatformIDs(platformCount, platforms, out _),
            "clGetPlatformIDs");

        var result = new List<DiscoveredDevice>();

        foreach (var platform in platforms)
        {
            var platformName = GetPlatformString(platform, ClPlatformName);

            status = clGetDeviceIDs(
                platform,
                ClDeviceTypeAll,
                0,
                null,
                out var deviceCount);

            if (status == ClDeviceNotFound || deviceCount == 0)
            {
                continue;
            }

            Check(status, "clGetDeviceIDs");

            var devices = new IntPtr[checked((int)deviceCount)];

            Check(
                clGetDeviceIDs(
                    platform,
                    ClDeviceTypeAll,
                    deviceCount,
                    devices,
                    out _),
                "clGetDeviceIDs");

            foreach (var device in devices)
            {
                try
                {
                    var type = GetValue<ulong>(device, ClDeviceType);

                    result.Add(new DiscoveredDevice(
                        IsGpu: (type & ClDeviceTypeGpu) != 0,
                        IsCpu: (type & ClDeviceTypeCpu) != 0,
                        Info: ReadDeviceInfo(platformName, device, type)));
                }
                catch (OpenClException exception)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Skipping an OpenCL device on platform '{platformName}': " +
                        $"{exception.Message}");
                }
            }
        }

        return result;
    }

    private static OpenClDeviceInfo ReadDeviceInfo(
        string platform,
        IntPtr device,
        ulong type)
    {
        var name = GetString(device, ClDeviceName);
        var vendor = GetString(device, ClDeviceVendor);
        var deviceType = GetDeviceTypeName(type);

        var workItemDimensions =
            GetValue<uint>(device, ClDeviceMaxWorkItemDimensions);

        var workItemSizes = workItemDimensions == 0
            ? []
            : GetArray<nuint>(
                device,
                ClDeviceMaxWorkItemSizes,
                checked((int)workItemDimensions));

        var singleFpConfig =
            GetOptionalValue<ulong>(device, ClDeviceSingleFpConfig);

        var doubleFpConfig =
            GetOptionalValue<ulong>(device, ClDeviceDoubleFpConfig);

        var version =
            GetOptionalString(device, ClDeviceVersion);

        var openClCVersion =
            GetOptionalString(device, ClDeviceOpenClCVersion);

        return new OpenClDeviceInfo
        {
            DisplayName = $"{deviceType} - {vendor} {name} ({platform})",
            Name = name,
            Platform = platform,
            Version = FirstNonEmpty(version, openClCVersion, "Unavailable"),
            Vendor = vendor,
            Type = deviceType,

            ComputeUnits =
                GetValue<uint>(device, ClDeviceMaxComputeUnits).ToString(),

            Clock =
                $"{GetValue<uint>(device, ClDeviceMaxClockFrequency)} MHz",

            AddressSpace =
                $"{GetValue<uint>(device, ClDeviceAddressBits)} bit",

            MaxWorkGroupSize =
                GetValue<nuint>(device, ClDeviceMaxWorkGroupSize).ToString(),

            WorkItemSizes = workItemSizes.Length == 0
                ? "Unavailable"
                : string.Join(" x ", workItemSizes),

            SinglePrecisionFma =
                FormatFlag(singleFpConfig, ClFpFma),

            DoublePrecisionSupport =
                FormatConfigurationSupport(doubleFpConfig),

            GlobalMemory =
                FormatBytes(
                    GetValue<ulong>(device, ClDeviceGlobalMemorySize)),

            LocalMemory =
                FormatBytes(
                    GetValue<ulong>(device, ClDeviceLocalMemorySize)),

            LocalMemoryType =
                FormatLocalMemoryType(
                    GetValue<uint>(device, ClDeviceLocalMemoryType)),

            UnifiedMemoryWithHost =
                FormatBoolean(
                    GetOptionalValue<uint>(
                        device,
                        ClDeviceHostUnifiedMemory)),

            Endianness =
                FormatBoolean(
                    GetOptionalValue<uint>(device, ClDeviceLittleEndian),
                    "little endian",
                    "big endian"),

            EccSupport =
                FormatBoolean(
                    GetOptionalValue<uint>(
                        device,
                        ClDeviceErrorCorrectionSupport)),

            NativeCharWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDeviceNativeVectorWidthChar),

            NativeShortWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDeviceNativeVectorWidthShort),

            NativeIntWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDeviceNativeVectorWidthInt),

            NativeLongWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDeviceNativeVectorWidthLong),

            NativeFloatWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDeviceNativeVectorWidthFloat),

            NativeDoubleWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDeviceNativeVectorWidthDouble),

            NativeHalfWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDeviceNativeVectorWidthHalf),

            PreferredCharWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDevicePreferredVectorWidthChar),

            PreferredShortWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDevicePreferredVectorWidthShort),

            PreferredIntWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDevicePreferredVectorWidthInt),

            PreferredLongWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDevicePreferredVectorWidthLong),

            PreferredFloatWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDevicePreferredVectorWidthFloat),

            PreferredDoubleWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDevicePreferredVectorWidthDouble),

            PreferredHalfWidth =
                FormatOptionalValue<uint>(
                    device,
                    ClDevicePreferredVectorWidthHalf)
        };
    }

    private static string GetPlatformString(IntPtr platform, uint property)
    {
        Check(
            clGetPlatformInfo(platform, property, 0, null, out var size),
            "clGetPlatformInfo");

        if (size == 0)
        {
            return string.Empty;
        }

        var buffer = new byte[checked((int)size)];

        Check(
            clGetPlatformInfo(
                platform,
                property,
                size,
                buffer,
                out var returnedSize),
            "clGetPlatformInfo");

        ValidateReturnedSize(returnedSize, size, "clGetPlatformInfo");

        return DecodeString(buffer);
    }

    private static string GetString(IntPtr device, uint property)
    {
        Check(
            clGetDeviceInfo(device, property, 0, null, out var size),
            "clGetDeviceInfo");

        if (size == 0)
        {
            return string.Empty;
        }

        var buffer = new byte[checked((int)size)];

        Check(
            clGetDeviceInfo(
                device,
                property,
                size,
                buffer,
                out var returnedSize),
            "clGetDeviceInfo");

        ValidateReturnedSize(returnedSize, size, "clGetDeviceInfo");

        return DecodeString(buffer);
    }

    private static string GetOptionalString(IntPtr device, uint property)
    {
        try
        {
            return GetString(device, property);
        }
        catch (OpenClException)
        {
            return string.Empty;
        }
    }

    private static T GetValue<T>(IntPtr device, uint property)
        where T : unmanaged
    {
        var expectedSize = (nuint)Marshal.SizeOf<T>();
        var buffer = ReadDeviceProperty(device, property, expectedSize);

        return MemoryMarshal.Read<T>(buffer);
    }

    private static T? GetOptionalValue<T>(IntPtr device, uint property)
        where T : unmanaged
    {
        try
        {
            return GetValue<T>(device, property);
        }
        catch (OpenClException)
        {
            return null;
        }
    }

    private static string FormatOptionalValue<T>(
        IntPtr device,
        uint property)
        where T : unmanaged
    {
        var value = GetOptionalValue<T>(device, property);
        return value?.ToString() ?? "Unavailable";
    }

    private static T[] GetArray<T>(
        IntPtr device,
        uint property,
        int count)
        where T : unmanaged
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0)
        {
            return [];
        }

        var elementSize = Marshal.SizeOf<T>();
        var totalSize = checked(elementSize * count);

        var buffer = ReadDeviceProperty(
            device,
            property,
            checked((nuint)totalSize));

        return MemoryMarshal.Cast<byte, T>(buffer).ToArray();
    }

    private static byte[] ReadDeviceProperty(
        IntPtr device,
        uint property,
        nuint expectedSize)
    {
        var buffer = new byte[checked((int)expectedSize)];

        Check(
            clGetDeviceInfo(
                device,
                property,
                expectedSize,
                buffer,
                out var returnedSize),
            "clGetDeviceInfo");

        ValidateReturnedSize(
            returnedSize,
            expectedSize,
            "clGetDeviceInfo");

        return buffer;
    }

    private static void ValidateReturnedSize(
        nuint returnedSize,
        nuint suppliedSize,
        string operation)
    {
        if (returnedSize > suppliedSize)
        {
            throw new OpenClException(
                errorCode: null,
                operation,
                $"The driver returned {returnedSize} bytes, but only " +
                $"{suppliedSize} bytes were supplied.");
        }
    }

    private static string DecodeString(byte[] buffer)
    {
        var terminatorIndex = Array.IndexOf(buffer, (byte)0);

        var length = terminatorIndex >= 0
            ? terminatorIndex
            : buffer.Length;

        return Encoding.UTF8
            .GetString(buffer, 0, length)
            .Trim();
    }

    private static string GetDeviceTypeName(ulong type)
    {
        if ((type & ClDeviceTypeGpu) != 0)
        {
            return "GPU";
        }

        if ((type & ClDeviceTypeCpu) != 0)
        {
            return "CPU";
        }

        if ((type & ClDeviceTypeAccelerator) != 0)
        {
            return "Accelerator";
        }

        if ((type & ClDeviceTypeCustom) != 0)
        {
            return "Custom device";
        }

        if ((type & ClDeviceTypeDefault) != 0)
        {
            return "Default device";
        }

        return "Unknown";
    }

    private static string FormatBytes(ulong bytes)
    {
        const double kibibyte = 1024d;
        const double mebibyte = kibibyte * 1024d;
        const double gibibyte = mebibyte * 1024d;

        return bytes switch
        {
            >= 1024UL * 1024UL * 1024UL =>
                $"{bytes / gibibyte:N2} GiB",

            >= 1024UL * 1024UL =>
                $"{bytes / mebibyte:N0} MiB",

            >= 1024UL =>
                $"{bytes / kibibyte:N0} KiB",

            _ =>
                $"{bytes:N0} B"
        };
    }

    private static OpenClDeviceInfo CreateHostCpuInfo(
        string? statusMessage = null)
    {
        var name = "Host CPU";
        var vendor = "Unavailable";
        var clock = "Unavailable";

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var processorKey = Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");

                name =
                    processorKey?.GetValue("ProcessorNameString") as string
                    ?? Environment.GetEnvironmentVariable(
                        "PROCESSOR_IDENTIFIER")
                    ?? name;

                var vendorIdentifier =
                    processorKey?.GetValue("VendorIdentifier") as string;

                vendor = FormatCpuVendor(vendorIdentifier);

                if (processorKey?.GetValue("~MHz") is int frequency &&
                    frequency > 0)
                {
                    clock = $"~{frequency} MHz (OS-reported)";
                }
            }
            catch (Exception exception)
                when (exception is SecurityException
                      or UnauthorizedAccessException
                      or IOException)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Could not read CPU information from the registry: " +
                    $"{exception.Message}");
            }
        }

        var totalMemory = TryGetSystemPhysicalMemoryBytes();

        return new OpenClDeviceInfo
        {
            DisplayName = $"CPU - {vendor} {name} (host)",
            StatusMessage = statusMessage
                ?? "Host CPU information. OpenCL-specific capabilities " +
                   "are unavailable.",

            Name = name,
            Platform = OperatingSystem.IsWindows()
                ? "Windows host"
                : "Host operating system",

            Version = "Not an OpenCL device",
            Vendor = vendor,
            Type = "CPU",
            ComputeUnitsLabel = "Logical processors",
            ComputeUnits = Environment.ProcessorCount.ToString(),
            Clock = clock,

            AddressSpace = Environment.Is64BitProcess
                ? "64 bit process"
                : "32 bit process",

            MaxWorkGroupSize = "N/A",
            WorkItemSizes = "N/A",
            SinglePrecisionFma = "N/A",
            DoublePrecisionSupport = "N/A",

            GlobalMemory = totalMemory is { } bytes
                ? FormatBytes(bytes)
                : "Unavailable",

            LocalMemory = "N/A",
            LocalMemoryType = "N/A",
            UnifiedMemoryWithHost = "yes",

            Endianness = BitConverter.IsLittleEndian
                ? "little endian"
                : "big endian",

            EccSupport = "Unavailable",
            NativeCharWidth = "N/A",
            NativeShortWidth = "N/A",
            NativeIntWidth = "N/A",
            NativeLongWidth = "N/A",
            NativeFloatWidth = "N/A",
            NativeDoubleWidth = "N/A",
            NativeHalfWidth = "N/A",
            PreferredCharWidth = "N/A",
            PreferredShortWidth = "N/A",
            PreferredIntWidth = "N/A",
            PreferredLongWidth = "N/A",
            PreferredFloatWidth = "N/A",
            PreferredDoubleWidth = "N/A",
            PreferredHalfWidth = "N/A"
        };
    }

    private static string FormatCpuVendor(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return "Unavailable";
        }

        return identifier.Trim() switch
        {
            "AuthenticAMD" => "AMD",
            "GenuineIntel" => "Intel",
            "ARM" => "Arm",
            "ARM64" => "Arm",
            var value => value
        };
    }

    private static ulong? TryGetSystemPhysicalMemoryBytes()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var memoryStatus = new MemoryStatusEx
        {
            Length = checked((uint)Marshal.SizeOf<MemoryStatusEx>())
        };

        return GlobalMemoryStatusEx(ref memoryStatus)
            ? memoryStatus.TotalPhysicalMemory
            : null;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(
                   value => !string.IsNullOrWhiteSpace(value))
               ?? string.Empty;
    }

    private static string FormatFlag(
        ulong? configuration,
        ulong flag)
    {
        return configuration.HasValue
            ? (configuration.Value & flag) != 0 ? "yes" : "no"
            : "Unavailable";
    }

    private static string FormatConfigurationSupport(
        ulong? configuration)
    {
        return configuration.HasValue
            ? configuration.Value != 0 ? "yes" : "no"
            : "Unavailable";
    }

    private static string FormatBoolean(
        uint? value,
        string trueValue = "yes",
        string falseValue = "no")
    {
        return value.HasValue
            ? value.Value != 0 ? trueValue : falseValue
            : "Unavailable";
    }

    private static string FormatLocalMemoryType(uint type)
    {
        return type switch
        {
            ClLocalMemory => "local",
            ClGlobalMemory => "global",
            _ => $"Unknown ({type})"
        };
    }

    private static void Check(int status, string operation)
    {
        if (status != ClSuccess)
        {
            throw new OpenClException(status, operation);
        }
    }

    private readonly record struct DiscoveredDevice(
        bool IsGpu,
        bool IsCpu,
        OpenClDeviceInfo Info);

    private sealed class OpenClException : Exception
    {
        public OpenClException(
            int? errorCode,
            string operation,
            string? detail = null)
            : base(CreateMessage(errorCode, operation, detail))
        {
            ErrorCode = errorCode;
            Operation = operation;
        }

        public int? ErrorCode { get; }

        public string Operation { get; }

        private static string CreateMessage(
            int? errorCode,
            string operation,
            string? detail)
        {
            var message = errorCode.HasValue
                ? $"{operation} failed with OpenCL error {errorCode.Value}."
                : $"{operation} failed.";

            return string.IsNullOrWhiteSpace(detail)
                ? message
                : $"{message} {detail}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalMemory;
        public ulong AvailablePhysicalMemory;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(
        ref MemoryStatusEx buffer);

    [DllImport(
        OpenClLibrary,
        CallingConvention = CallingConvention.Winapi)]
    private static extern int clGetPlatformIDs(
        uint numEntries,
        [Out] IntPtr[]? platforms,
        out uint numPlatforms);

    [DllImport(
        OpenClLibrary,
        CallingConvention = CallingConvention.Winapi)]
    private static extern int clGetPlatformInfo(
        IntPtr platform,
        uint paramName,
        nuint paramValueSize,
        [Out] byte[]? paramValue,
        out nuint paramValueSizeRet);

    [DllImport(
        OpenClLibrary,
        CallingConvention = CallingConvention.Winapi)]
    private static extern int clGetDeviceIDs(
        IntPtr platform,
        ulong deviceType,
        uint numEntries,
        [Out] IntPtr[]? devices,
        out uint numDevices);

    [DllImport(
        OpenClLibrary,
        CallingConvention = CallingConvention.Winapi)]
    private static extern int clGetDeviceInfo(
        IntPtr device,
        uint paramName,
        nuint paramValueSize,
        [Out] byte[]? paramValue,
        out nuint paramValueSizeRet);
}