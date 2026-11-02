using System.Runtime.InteropServices;
using System.Text;
using Cling.Models;
using Microsoft.Win32;

namespace Cling.Services;

public sealed class OpenClDeviceService
{
    private const int Success = 0;
    private const int DeviceNotFound = -1;
    private const int PlatformNotFound = -1001;
    private const ulong AllDeviceTypes = 0xFFFFFFFF;
    private const ulong DeviceTypeCpu = 1UL << 1;
    private const ulong DeviceTypeGpu = 1UL << 2;

    private const uint PlatformName = 0x0902;

    private const uint DeviceType = 0x1000;
    private const uint DeviceMaxComputeUnits = 0x1002;
    private const uint DeviceMaxWorkItemDimensions = 0x1003;
    private const uint DeviceMaxWorkGroupSize = 0x1004;
    private const uint DeviceMaxWorkItemSizes = 0x1005;
    private const uint DevicePreferredVectorWidthChar = 0x1006;
    private const uint DevicePreferredVectorWidthShort = 0x1007;
    private const uint DevicePreferredVectorWidthInt = 0x1008;
    private const uint DevicePreferredVectorWidthLong = 0x1009;
    private const uint DevicePreferredVectorWidthFloat = 0x100A;
    private const uint DevicePreferredVectorWidthDouble = 0x100B;
    private const uint DeviceMaxClockFrequency = 0x100C;
    private const uint DeviceAddressBits = 0x100D;
    private const uint DeviceSingleFpConfig = 0x101B;
    private const uint DeviceGlobalMemorySize = 0x101F;
    private const uint DeviceLocalMemoryType = 0x1022;
    private const uint DeviceLocalMemorySize = 0x1023;
    private const uint DeviceErrorCorrectionSupport = 0x1024;
    private const uint DeviceLittleEndian = 0x1026;
    private const uint DeviceName = 0x102B;
    private const uint DeviceVendor = 0x102C;
    private const uint DeviceVersion = 0x102F;
    private const uint DeviceDoubleFpConfig = 0x1032;
    private const uint DeviceHostUnifiedMemory = 0x1035;
    private const uint DeviceNativeVectorWidthChar = 0x1036;
    private const uint DeviceNativeVectorWidthShort = 0x1037;
    private const uint DeviceNativeVectorWidthInt = 0x1038;
    private const uint DeviceNativeVectorWidthLong = 0x1039;
    private const uint DeviceNativeVectorWidthFloat = 0x103A;
    private const uint DeviceNativeVectorWidthDouble = 0x103B;
    private const uint DeviceNativeVectorWidthHalf = 0x103C;
    private const uint DeviceOpenClCVersion = 0x103D;
    private const uint DevicePreferredVectorWidthHalf = 0x1034;
    private const ulong FloatingPointFma = 1UL << 4;

    public IReadOnlyList<OpenClDeviceInfo> GetAvailableDevices()
    {
        try
        {
            var devices = EnumerateDevices()
                .Where(device => device.IsCpu || device.IsGpu)
                .OrderByDescending(device => device.IsGpu)
                .Select(device => device.Info)
                .ToList();

            if (!devices.Any(device => device.Type == "CPU"))
            {
                devices.Add(CreateHostCpuInfo());
            }

            return devices;
        }
        catch (DllNotFoundException)
        {
            return [CreateHostCpuInfo()];
        }
        catch (EntryPointNotFoundException)
        {
            return [CreateHostCpuInfo()];
        }
        catch (BadImageFormatException)
        {
            return [CreateHostCpuInfo()];
        }
        catch (OpenClException exception)
        {
            return [CreateHostCpuInfo($"OpenCL query failed ({exception.ErrorCode}); showing host CPU information.")];
        }
    }

    private static List<(bool IsGpu, bool IsCpu, OpenClDeviceInfo Info)> EnumerateDevices()
    {
        var platformStatus = clGetPlatformIDs(0, null, out uint platformCount);
        if (platformStatus == PlatformNotFound)
        {
            return [];
        }
        Check(platformStatus);
        if (platformCount == 0)
        {
            return [];
        }

        var platforms = new IntPtr[checked((int)platformCount)];
        Check(clGetPlatformIDs(platformCount, platforms, out _));
        var devices = new List<(bool IsGpu, bool IsCpu, OpenClDeviceInfo Info)>();

        foreach (var platform in platforms)
        {
            var platformName = GetPlatformString(platform, PlatformName);

            var status = clGetDeviceIDs(platform, AllDeviceTypes, 0, null, out uint deviceCount);
            if (status == DeviceNotFound)
            {
                continue;
            }
            Check(status);
            if (deviceCount == 0)
            {
                continue;
            }

            var platformDevices = new IntPtr[checked((int)deviceCount)];
            Check(clGetDeviceIDs(platform, AllDeviceTypes, deviceCount, platformDevices, out _));

            foreach (var device in platformDevices)
            {
                try
                {
                    var type = GetValue<ulong>(device, DeviceType);
                    devices.Add(((type & DeviceTypeGpu) != 0, (type & DeviceTypeCpu) != 0, ReadDeviceInfo(platformName, device, type)));
                }
                catch (OpenClException)
                {
                }
            }
        }

        return devices;
    }

    private static OpenClDeviceInfo ReadDeviceInfo(string platform, IntPtr device, ulong type)
    {
        var name = GetString(device, DeviceName);
        var vendor = GetString(device, DeviceVendor);
        var deviceType = GetDeviceTypeName(type);
        var workItemDimensions = GetValue<uint>(device, DeviceMaxWorkItemDimensions);
        var workItemSizes = GetArray<nuint>(device, DeviceMaxWorkItemSizes, checked((int)workItemDimensions));
        var singleFpConfig = GetOptionalValue<ulong>(device, DeviceSingleFpConfig);
        var doubleFpConfig = GetOptionalValue<ulong>(device, DeviceDoubleFpConfig);
        var openClCVersion = GetOptionalString(device, DeviceOpenClCVersion);
        var version = GetOptionalString(device, DeviceVersion);

        return new OpenClDeviceInfo
        {
            DisplayName = $"{deviceType} — {vendor} {name} ({platform})",
            Name = name,
            Platform = platform,
            Version = string.IsNullOrWhiteSpace(version) ? openClCVersion : version,
            Vendor = vendor,
            Type = deviceType,
            ComputeUnits = GetValue<uint>(device, DeviceMaxComputeUnits).ToString(),
            Clock = $"{GetValue<uint>(device, DeviceMaxClockFrequency)} MHz",
            AddressSpace = $"{GetValue<uint>(device, DeviceAddressBits)} bit",
            MaxWorkGroupSize = GetValue<nuint>(device, DeviceMaxWorkGroupSize).ToString(),
            WorkItemSizes = string.Join(" × ", workItemSizes),
            SinglePrecisionFma = FormatSupport(singleFpConfig, FloatingPointFma),
            DoublePrecisionSupport = FormatSupport(doubleFpConfig, 0),
            GlobalMemory = FormatBytes(GetGlobalMemoryBytes(device, type), 1_000_000d, "MB"),
            LocalMemory = FormatBytes(GetValue<ulong>(device, DeviceLocalMemorySize), 1024d, "KiB"),
            LocalMemoryType = FormatLocalMemoryType(GetValue<uint>(device, DeviceLocalMemoryType)),
            UnifiedMemoryWithHost = FormatBoolean(GetOptionalValue<uint>(device, DeviceHostUnifiedMemory)),
            Endianness = FormatBoolean(GetOptionalValue<uint>(device, DeviceLittleEndian), "little endian", "big endian"),
            EccSupport = FormatBoolean(GetOptionalValue<uint>(device, DeviceErrorCorrectionSupport)),
            NativeCharWidth = FormatOptionalValue<uint>(device, DeviceNativeVectorWidthChar),
            NativeShortWidth = FormatOptionalValue<uint>(device, DeviceNativeVectorWidthShort),
            NativeIntWidth = FormatOptionalValue<uint>(device, DeviceNativeVectorWidthInt),
            NativeLongWidth = FormatOptionalValue<uint>(device, DeviceNativeVectorWidthLong),
            NativeFloatWidth = FormatOptionalValue<uint>(device, DeviceNativeVectorWidthFloat),
            NativeDoubleWidth = FormatOptionalValue<uint>(device, DeviceNativeVectorWidthDouble),
            NativeHalfWidth = FormatOptionalValue<uint>(device, DeviceNativeVectorWidthHalf),
            PreferredCharWidth = FormatOptionalValue<uint>(device, DevicePreferredVectorWidthChar),
            PreferredShortWidth = FormatOptionalValue<uint>(device, DevicePreferredVectorWidthShort),
            PreferredIntWidth = FormatOptionalValue<uint>(device, DevicePreferredVectorWidthInt),
            PreferredLongWidth = FormatOptionalValue<uint>(device, DevicePreferredVectorWidthLong),
            PreferredFloatWidth = FormatOptionalValue<uint>(device, DevicePreferredVectorWidthFloat),
            PreferredDoubleWidth = FormatOptionalValue<uint>(device, DevicePreferredVectorWidthDouble),
            PreferredHalfWidth = FormatOptionalValue<uint>(device, DevicePreferredVectorWidthHalf)
        };
    }

    private static string GetPlatformString(IntPtr platform, uint property)
    {
        Check(clGetPlatformInfo(platform, property, 0, null, out nuint size));
        var buffer = new byte[checked((int)size)];
        Check(clGetPlatformInfo(platform, property, size, buffer, out _));
        return DecodeString(buffer);
    }

    private static string GetString(IntPtr device, uint property)
    {
        Check(clGetDeviceInfo(device, property, 0, null, out nuint size));
        var buffer = new byte[checked((int)size)];
        Check(clGetDeviceInfo(device, property, size, buffer, out _));
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

    private static T GetValue<T>(IntPtr device, uint property) where T : unmanaged
    {
        var buffer = ReadDeviceProperty(device, property, checked((nuint)Marshal.SizeOf<T>()));
        return MemoryMarshal.Read<T>(buffer);
    }

    private static T? GetOptionalValue<T>(IntPtr device, uint property) where T : unmanaged
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

    private static string FormatOptionalValue<T>(IntPtr device, uint property) where T : unmanaged
    {
        return GetOptionalValue<T>(device, property)?.ToString() ?? "Unavailable";
    }

    private static T[] GetArray<T>(IntPtr device, uint property, int count) where T : unmanaged
    {
        var elementSize = Marshal.SizeOf<T>();
        var buffer = ReadDeviceProperty(device, property, checked((nuint)(elementSize * count)));
        var values = new T[count];
        for (var index = 0; index < count; index++)
        {
            values[index] = MemoryMarshal.Read<T>(buffer.AsSpan(index * elementSize, elementSize));
        }

        return values;
    }

    private static byte[] ReadDeviceProperty(IntPtr device, uint property, nuint size)
    {
        var buffer = new byte[checked((int)size)];
        Check(clGetDeviceInfo(device, property, size, buffer, out _));
        return buffer;
    }

    private static string DecodeString(byte[] buffer)
    {
        return Encoding.UTF8.GetString(buffer).TrimEnd('\0').Trim();
    }

    private static string GetDeviceTypeName(ulong type)
    {
        if ((type & DeviceTypeGpu) != 0) return "GPU";
        if ((type & (1UL << 1)) != 0) return "CPU";
        if ((type & (1UL << 3)) != 0) return "Accelerator";
        if ((type & (1UL << 0)) != 0) return "Default device";
        if ((type & (1UL << 4)) != 0) return "Custom device";
        return "Unknown";
    }

    private static string FormatBytes(ulong bytes, double divisor, string unit)
    {
        return $"{bytes / divisor:N0} {unit}";
    }

    private static ulong GetGlobalMemoryBytes(IntPtr device, ulong type)
    {
        if ((type & DeviceTypeCpu) != 0 && TryGetSystemPhysicalMemoryBytes() is ulong systemMemory)
            return systemMemory;

        return GetValue<ulong>(device, DeviceGlobalMemorySize);
    }

    private static OpenClDeviceInfo CreateHostCpuInfo(string? statusMessage = null)
    {
        using var processorKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        var name = processorKey?.GetValue("ProcessorNameString") as string ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Host CPU";
        var vendorIdentifier = processorKey?.GetValue("VendorIdentifier") as string ?? string.Empty;
        var vendor = vendorIdentifier switch
        {
            "AuthenticAMD" => "AMD",
            "GenuineIntel" => "Intel",
            "ARM" => "Arm",
            _ => vendorIdentifier
        };
        var clockMhz = processorKey?.GetValue("~MHz") is int frequency && frequency > 0
            ? $"~{frequency} MHz (OS-reported)"
            : "Unavailable";
        var totalMemory = TryGetSystemPhysicalMemoryBytes();

        return new OpenClDeviceInfo
        {
            DisplayName = $"CPU — {vendor} {name} (Windows host)",
            StatusMessage = statusMessage ?? "Host CPU information; OpenCL-specific capabilities are not available.",
            Name = name,
            Platform = "Windows host",
            Version = "Not an OpenCL device",
            Vendor = string.IsNullOrWhiteSpace(vendor) ? "Unavailable" : vendor,
            Type = "CPU",
            ComputeUnitsLabel = "Logical processors",
            ComputeUnits = Environment.ProcessorCount.ToString(),
            Clock = clockMhz,
            AddressSpace = Environment.Is64BitOperatingSystem ? "64 bit" : "32 bit",
            MaxWorkGroupSize = "N/A",
            WorkItemSizes = "N/A",
            SinglePrecisionFma = "N/A",
            DoublePrecisionSupport = "N/A",
            GlobalMemory = totalMemory is ulong bytes ? FormatBytes(bytes, 1_000_000d, "MB") : "Unavailable",
            LocalMemory = "N/A",
            LocalMemoryType = "N/A",
            UnifiedMemoryWithHost = "yes",
            Endianness = BitConverter.IsLittleEndian ? "little endian" : "big endian",
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

    private static ulong? TryGetSystemPhysicalMemoryBytes()
    {
        var memoryStatus = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        return GlobalMemoryStatusEx(ref memoryStatus) ? memoryStatus.TotalPhysicalMemory : null;
    }

    private static string FormatSupport(ulong? configuration, ulong flag)
    {
        if (!configuration.HasValue) return "Unavailable";
        return flag == 0 ? (configuration.Value != 0 ? "yes" : "no") : ((configuration.Value & flag) != 0 ? "yes" : "no");
    }

    private static string FormatBoolean(uint? value, string trueValue = "yes", string falseValue = "no")
    {
        return value.HasValue ? (value.Value != 0 ? trueValue : falseValue) : "Unavailable";
    }

    private static string FormatLocalMemoryType(uint type)
    {
        return type switch
        {
            1 => "local",
            2 => "global",
            _ => $"Unknown ({type})"
        };
    }

    private static void Check(int status)
    {
        if (status != Success)
        {
            throw new OpenClException(status);
        }
    }

    private sealed class OpenClException(int errorCode) : Exception
    {
        public int ErrorCode { get; } = errorCode;
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Winapi)]
    private static extern int clGetPlatformIDs(uint numEntries, [Out] IntPtr[]? platforms, out uint numPlatforms);

    [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Winapi)]
    private static extern int clGetPlatformInfo(IntPtr platform, uint paramName, nuint paramValueSize, [Out] byte[]? paramValue, out nuint paramValueSizeRet);

    [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Winapi)]
    private static extern int clGetDeviceIDs(IntPtr platform, ulong deviceType, uint numEntries, [Out] IntPtr[]? devices, out uint numDevices);

    [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Winapi)]
    private static extern int clGetDeviceInfo(IntPtr device, uint paramName, nuint paramValueSize, [Out] byte[]? paramValue, out nuint paramValueSizeRet);
}
