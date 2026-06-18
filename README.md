# Thalamus
Thalamus is a program designed for dynamic C++ code injection, memory manipulation, and time (speed) modification for processes running on Windows operating systems. It is primarily built as a tool for cheating. But you can use it as you wish.

**Warning:** Creator (DevAddPhysicalMemory) is not responsible if any damage caused by/using this program
## Key Features
* **Dynamic C++ Compilation:** Uses an internal Monaco Editor to allow writing C++ code and compiling it into a `.dll` format on the fly.
* **Thread Hijacking:** Ability to hijack an existing thread to execute injected code. (See known problems)
* **Header Erasing:** Cleans the "PE Headers" of the injected DLL to reduce the risk of detection by security software.
* **Speedhack Support:** Hooks time-related functions (`GetTickCount`, `QueryPerformanceCounter`) via `ThalamusSpeed.dll` to manipulate the speed of the target application.
* **Changing parameters:** You can change g++ compiling parameters by yourself for better compatibility if you want to use your module.

## Architectural Overview

| Component | Function |
| :--- | :--- |
| **Thalamus (WPF)** | Provides the UI, manages G++ compiler integration, and coordinates injection. |
| **ThalamusCore.dll** | The main module that handles Manual Mapping, header erasing, and Thread Hijacking operations. |
| **ThalamusSpeed.dll** | The speedhack module |

### Speedhack Mechanism
`ThalamusSpeed.dll` uses `Microsoft Detours` to hook time apis. It reads a speed factor from a shared memory region (`ThalamusSharedMemory`) and scales the results returned by functions like `QueryPerformanceCounter` to effectively alter the application's perceived time.

## Known Problems (please help)
* When you hijack a thread, it doesnt execute code, does nothing.

### This program is licensed with Apache License 2.0 

<img width="1887" height="996" alt="image" src="https://github.com/user-attachments/assets/5782dd43-dbf7-4625-bedc-63cb883334c7" />
<img width="1413" height="666" alt="image" src="https://github.com/user-attachments/assets/7208af49-3269-48f3-bfdc-d8a1d9e5d99a" />
