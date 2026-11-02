# CL!ng - OpenCL Device Information Viewer

A modern WPF application that displays detailed information about OpenCL devices (GPUs, CPUs) on your system.

## Features

- **Device Information**: View comprehensive details about available OpenCL devices including:
  - Device name, vendor, and platform
  - Compute units and clock frequency
  - Memory specifications (global and local memory)
  - Supported precision levels (float, double, half)
  - ECC support and endianness

- **Memory Information**: Access detailed memory properties for each device

- **Performance Metrics**: View memory bandwidth and other performance indicators

- **Dark Mode UI**: Modern dark-themed interface with:
  - Consistent color scheme across all views
  - Blue-accented selected tabs for clear visual feedback
  - Dark scrollbars and drop-down menus
  - Helvetica font family for clean typography

## Building

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version 8.0 or later)
- Windows 10/11

### Build Steps

```bash
# Restore dependencies
dotnet restore

# Build the project (x64 platform target)
dotnet build -c Release

# Run the application
dotnet run
```

## Usage

The application automatically detects and displays available OpenCL devices when an OpenCL runtime is installed on your system.

Supported OpenCL runtimes:
- AMD ROCm/OpenCL
- Intel oneAPI
- NVIDIA CUDA

### Note

If no OpenCL runtime is installed, the device selector will be disabled until a valid OpenCL device is detected.

## Dark Mode Styling

The application uses a consistent dark theme with the following styling:

- **Background**: `#1a1a1a` (dark gray)
- **Text**: `#e0e0e0` (light gray)
- **Selection**: `#3a95d8` (blue accent)
- **Scrollbar**: Dark theme with hover states
- **Font**: Helvetica Neue, Helvetica, Arial, sans-serif

### Custom Controls

The application provides custom styles for:
- `ClingWindow`: Main window styling
- `ClingTextBox`: Read-only info fields
- `ClingComboBox`: Drop-down menus with dark background
- `ClingButton`: Buttons with dark theme
- `ClingTabControl`: Tab controls with blue selection
- `ClingTabItemHeader`: Tab headers with rounded corners
- `ClingScrollViewer`: Scroll viewers with dark scrollbars

## OpenCL Runtime Support

The application supports OpenCL runtimes from:
- AMD
- Intel
- NVIDIA (via CUDA)

## Project Structure

```
src/
├── App.xaml                 # Application-wide styles and resources
├── MainWindow.xaml          # Main window with tabs
├── MainWindow.xaml.cs       # Window logic and device selection
├── Services/
│   └── OpenClDeviceService.cs  # OpenCL P/Invoke wrapper
└── Views/
    ├── DeviceView.xaml      # Device information tab
    ├── MemoryView.xaml      # Memory specifications tab
    ├── PerformanceView.xaml # Performance metrics tab
    └── AboutView.xaml       # About page with logo
```

## Troubleshooting

### OpenCL Runtime Not Detected

If the application reports that OpenCL.dll could not be found:

1. **Install an OpenCL-capable driver**: Ensure your GPU or CPU has an OpenCL driver installed.
   - AMD: Install AMD Software: Adrenalin Edition
   - Intel: Install Intel oneAPI Base Toolkit
   - NVIDIA: Install CUDA Toolkit (includes OpenCL support)

2. **Architecture match**: Ensure the OpenCL runtime architecture matches your application (x64).

3. **System32 stub**: The stub `OpenCL.dll` in `C:\Windows\System32` (less than 1MB) is automatically skipped as it's not a functional runtime.

### Dark Mode Issues

If you notice any white elements that should be dark:

- Buttons will turn blue on hover
- Selected tabs have a blue background
- Scrollbars are dark gray with lighter thumb
- Drop-down menus have dark backgrounds

### Windows Title Bar

The Windows title bar (drag handle area) is a system UI element. The application uses rounded top corners as a visual workaround to create a consistent dark appearance.

## License

Copyright © 2026 Xander Vanhaeren. All rights reserved.