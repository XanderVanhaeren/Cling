# CL!ng — OpenCL Device Information Viewer

A lightweight WPF desktop application for querying and displaying OpenCL device information on Windows systems.

## Features

- **Device Enumeration**: Lists all available OpenCL devices (GPU, CPU, accelerator)
- **Comprehensive Device Info**: Displays vendor, name, version, compute units, clock speed, memory specs
- **Memory Details**: Global/local memory sizes, ECC support, endianness, vector widths
- **Performance Metrics**: Memory bandwidth measurements with visual charts
- **Fallback Support**: Shows host CPU information when OpenCL is unavailable

## Requirements

- Windows 10/11
- [.NET 10.0](https://dotnet.microsoft.com/download) SDK or later
- OpenCL runtime (AMD, Intel, or NVIDIA) installed on the system

## Building

```bash
# Restore dependencies
dotnet restore

# Build in Release mode
dotnet build -c Release

# Run the application
dotnet run --project Cling.csproj
```

Or use Visual Studio:
1. Open `Cling.slnx`
2. Set startup project to `Cling`
3. Press `F5` or `Ctrl+F5`

## Usage

### Device Tab
Shows common device properties:
- Name, Platform, Version, Vendor, Type
- Compute units, Clock frequency, Address space
- Max work group size, Work item sizes

### Memory Tab
Displays memory-related specifications:
- Global and local memory sizes
- Local memory type (local/global)
- Unified memory with host
- Endianness, ECC support
- Native and preferred vector widths for all integer/float types

### Performance Tab
Shows memory bandwidth measurements:
- Host to Device transfer speeds
- Device to Host transfer speeds
- Device to Device transfer speeds
- Speed over time charts

### About Tab
Application version and credits.

## Project Structure

```
src/
├── App.xaml                 # Application resources and styles
├── App.xaml.cs              # Application entry point
├── AssemblyInfo.cs          # Assembly attributes
├── Cling.csproj             # Project file (.NET 10.0)
├── MainWindow.xaml          # Main window with tabs
├── MainWindow.xaml.cs       # Window logic and device selection
├── Controls/                # Reusable UI controls (empty)
├── Models/
│   └── OpenClDeviceInfo.cs  # Device info data model
├── obj/                     # Build output
├── Services/
│   └── OpenClDeviceService.cs # P/Invoke OpenCL wrapper
└── Views/
    ├── AboutView.xaml/.cs   # About tab
    ├── DeviceView.xaml/.cs  # Device info tab
    ├── MemoryView.xaml/.cs  # Memory specs tab
    └── PerformanceView.xaml/.cs # Performance metrics tab
```

## How It Works

1. **Device Enumeration**: Queries OpenCL.dll to enumerate platforms and devices
2. **Property Querying**: Uses `clGetDeviceInfo` to retrieve device properties via P/Invoke
3. **Data Binding**: XAML DataBindings populate read-only TextBox controls
4. **Fallback Logic**: If OpenCL.dll is missing or unavailable, shows host CPU info

## API Reference (P/Invoke)

The application uses the following OpenCL DLL functions:

| Function | Description |
|----------|-------------|
| `clGetPlatformIDs` | Enumerate OpenCL platforms |
| `clGetPlatformInfo` | Get platform properties (name, vendor) |
| `clGetDeviceIDs` | Enumerate devices on a platform |
| `clGetDeviceInfo` | Query device properties by ID |

### Common Device Properties

| Property ID | Name | Description |
|-------------|------|-------------|
| 0x1000 | `CL_DEVICE_TYPE` | Device type (GPU, CPU, accelerator) |
| 0x1002 | `CL_DEVICE_MAX_COMPUTE_UNITS` | Number of compute units |
| 0x1004 | `CL_DEVICE_MAX_WORK_GROUP_SIZE` | Maximum work group size |
| 0x100C | `CL_DEVICE_MAX_CLOCK_FREQUENCY` | Max clock frequency (MHz) |
| 0x100D | `CL_DEVICE_ADDRESS_BITS` | Address space width |
| 0x101F | `CL_DEVICE_GLOBAL_MEM_SIZE` | Global memory size |
| 0x102B | `CL_DEVICE_NAME` | Device name |
| 0x102C | `CL_DEVICE_VENDOR` | Vendor name |
| 0x102F | `CL_DEVICE_VERSION` | OpenCL version string |

## License

MIT License — See LICENSE file for details.

## Credits

- **Author**: Xander Vanhaeren (2026)
- **Platform**: Windows WPF with .NET 10.0

---

*Note: This application requires an OpenCL runtime to be installed on the system. Without it, host CPU information is displayed instead.*