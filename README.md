# NVIDIA Tesla Smart Fan Controller 🌬️🤖

An automatic, ultra-lightweight smart fan controller designed specifically for passive server-grade graphics cards (**NVIDIA Tesla A2, P4, T4**, etc.) installed in desktop PCs. Built with a high-performance **.NET WinForms (NVML)** background engine and an **Arduino Nano** hardware driver.

This utility eliminates the cooling headache of passive enterprise cards by dynamically calculating fan speed based on real-time GPU core temperatures using low-level API, featuring a slick Task Manager-style real-time monitor.

---

## ✨ Features
* **Zero-CPU Overhead Monitoring:** Powered by the official `nvml.dll` (NVIDIA Management Library). It directly queries the driver in-memory, bypassing heavy shell execution processes like `nvidia-smi`.
* **Hardware Failsafe Protection:** If Windows freezes or the background application is closed, the Arduino watchdog self-activates after 6 seconds and spins the fan up to 100% to save your GPU from overheating.
* **Silent 25 kHz PWM Signal:** Hardware timers on the Arduino Nano are reconfigured to output a true 25 kHz frequency. This completely eliminates high-pitched motor humming or buzzing at low fan speeds.
* **Animated Tray Icon:** Features a mini real-time hardware monitor graph right next to your Windows clock, allowing you to choose between tracking **GPU Load**, **GPU Temperature**, or **Fan Speed (RPM)**.
* **DoubleClick Real-Time Monitor:** Double-clicking the tray icon unleashes a broad, beautiful, Task Manager-style streaming graph displaying synchronized curves for both core temperature and PWM fan response.
* **Interactive Cooling Curve UI:** Right-click the tray icon to open an advanced GUI graph editor. Drag the **Min** and **Max** milestone nodes with your mouse to adjust your custom cooling targets instantly without reflashing the Arduino.

---

## 🔌 Hardware Wiring Diagram
* **Fan GND (Black/Grey)** -> **Arduino GND** AND **12V Power Supply Minus (-)** (*Mandatory: Common ground connection!*)
* **Fan 12V (Red)** -> **12V Power Supply Plus (+)**
* **Fan PWM (Blue)** -> **Pin D9 on Arduino Nano**
* **Fan Tachometer / Tach (Yellow/White)** -> **Pin D2 on Arduino Nano** (*Hardware Interrupt INT0*)

---

## 🚀 Quick Start / Deployment

### 1. Flash the Arduino
1. Open the sketch provided in the `/Arduino` directory inside your Arduino IDE.
2. Select your **Arduino Nano** (ATmega328P) board and upload the code.

### 2. Compile the Windows App
To build the fully portable, single-executable application, run the following commands in your terminal inside the `/WindowsApp` folder:
```bash
dotnet add package Microsoft.Win32.Registry
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```
Grab your standalone `TeslaFanController.exe` from the `bin/Release/.../publish/` folder and drop it into your preferred storage directory.

### 3. Execution & Configuration
1. Run the app (no administrator privileges required for core NVML reading!).
2. The application will generate a well-commented `config.txt` inside its local directory.
3. Right-click the animated tray graph, select **"Settings Panel..."**, scan your active hardware COM ports, and fine-tune your cooling lines.
4. Check **"Start with Windows"** in the context menu to safely automate your GPU cooling on system startup.

---

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
