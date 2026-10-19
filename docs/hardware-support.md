# Hardware support

The main desktop target is Windows 11 x64: Intel i7-11700K, EVGA RTX 3080, 32 GB DDR4-3200, ASUS Z590 Pro WiFi, Noctua NH-D15, and LG 24GN600-B over HDMI or DisplayPort. The development VM has virtual ARM64 hardware. No physical GPU, motherboard fan or monitor write has been validated on that desktop.

| Hardware | Implemented capability | Limits |
| --- | --- | --- |
| External monitors across brands | DDC/CI brightness, after successful Windows capability and range reads | Support follows the monitor, driver, port and cable. An LG/Dell/ASUS/Acer/Samsung/BenQ name is not a guarantee. Refresh after hot-plug. No writes happen during discovery. |
| NVIDIA GPUs across board brands | NVML temperature and board power; sustained temperature alerts | Loads the installed driver's System32 NVML library only. Unsupported readings are absent. EVGA, ASUS, MSI and other board brands use the NVIDIA driver interface, subject to its hardware support. |
| Intel/AMD CPU and GPU thermal sensors | No temperature backend yet | Windows does not supply one generic application API for all these sensors. Native CPU/memory/disk monitoring remains in Task Manager; Vela adds sustained alerts. |
| ASUS motherboard / NH-D15 fans | Firmware remains in control | No fan writes, kernel driver, voltage or clock changes. The NH-D15's PWM fans connect to motherboard headers; they are not an independent software-addressable controller. ASUS Q-Fan/Fan Xpert is separate vendor software. |
| Virtual monitors and GPUs | Clear unavailable state | No fictitious brightness range, temperature, fan curve or power value. |

DDC operations run off the UI thread and serialize access. Physical handles are destroyed after every operation; they are not cached across topology changes. A brightness value is applied only after the user chooses Apply. Microsoft cautions that MCCS implementations vary, so small changes should be checked physically before depending on a monitor's behavior.

NVIDIA's fan setter switches the GPU to manual fan policy and makes temperature management the caller's responsibility. A production fan backend needs tested firmware restoration and a separate watchdog for process failure. Merely finding the DLL export is not sufficient support; this build exposes no fan-writing control.

Sources: [Windows monitor brightness API](https://learn.microsoft.com/en-us/windows/win32/api/highlevelmonitorconfigurationapi/nf-highlevelmonitorconfigurationapi-getmonitorbrightness), [NVIDIA NVML queries](https://docs.nvidia.com/deploy/nvml-api/latest/api/group__nvmlDeviceQueries.html), [NVIDIA fan commands](https://docs.nvidia.com/deploy/nvml-api/latest/api/group__nvmlDeviceCommands.html), [ASUS Fan Xpert](https://www.asus.com/support/faq/1034000/), [Noctua fan configuration](https://www.noctua.at/en/support/faqs/how-do-i-optimise-my-fan-settings), [LG product information](https://www.lg.com/us/monitors/lg-24gn600-b-gaming-monitor).
