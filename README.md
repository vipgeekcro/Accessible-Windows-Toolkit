# Accessible Windows Toolkit

**Accessible Windows Toolkit** is a free and open-source Windows utility designed with a strong focus on accessibility for screen reader users.

The project grew out of a practical need for a simple and accessible way to manage selected Windows applications and settings without relying on system interfaces that can sometimes be complex or insufficiently accessible.

The application is developed and tested primarily with **NVDA**, with an emphasis on full keyboard operation, predictable focus management, clear status information, and accessible dialog windows.

**Accessible Windows Toolkit is under active development. Version 1.0 is the first public release, and additional Windows management, customization, and maintenance features are planned for future versions.**

## Version 1.0

The first public release focuses on the safe removal of unnecessary and optional Windows applications.

Accessible Windows Toolkit automatically scans the system to determine which supported applications are currently present and displays only relevant items. The user then chooses which applications to remove.

After removal, the program scans the system again to verify whether the operation actually succeeded and reports the final result.

If one application cannot be removed, processing continues with the remaining selected items.

## Accessibility

Accessibility is not an afterthought in this project; it is one of its core goals.

The interface is designed for keyboard and screen reader use. Particular attention has been given to NVDA users, including list navigation, item selection, control states, keyboard focus, operation status, and final results.

Feedback about accessibility and experiences with other screen readers are welcome.

## Download

Ready-to-use portable versions are available from the **Releases** section of this repository.

No traditional installation is required. Download the ZIP file for the desired version, extract it, and run the executable.

Because the program makes changes to Windows applications and components, administrator privileges are required for its operations.

## Source Code

The complete source code is available in this repository.

Accessible Windows Toolkit is developed in **C#**, using **WPF** and **.NET 10**.

The project is created with the assistance of AI tools, including **ChatGPT and Codex**. The author is not a professional programmer. Development is based on his ideas and practical experience as a blind Windows user. The author defines the functionality and accessibility requirements and personally tests the application with NVDA, while AI tools are used to assist with development and writing the code.

## Important Notice

Tools that remove Windows applications or change system settings should always be used with care.

The user decides which operations to perform and is responsible for changes made to their system. Review selected items before confirming their removal.

## Feedback and Issues

If you find a bug, encounter an accessibility problem, or have a suggestion for improvement, please open an **Issue** in this GitHub repository.
