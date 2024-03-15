<img src="AuthenticatorChooser/YubiKey.ico" height="24" alt="YubiKey 5 NFC USB-A" /> AuthenticatorChooser
===

[![Build status](https://img.shields.io/github/actions/workflow/status/Aldaviva/AuthenticatorChooser/dotnet.yml?branch=master&logo=github)](https://github.com/Aldaviva/AuthenticatorChooser/actions/workflows/dotnet.yml)

*Program that runs in the background to automatically skip the Windows "Sign in with your passkey" prompt and go straight to the USB security key option.*

<!-- MarkdownTOC autolink="true" bracket="round" autoanchor="false" levels="1,2,3" -->

- [Problem](#problem)
- [Solution](#solution)
- [Requirements](#requirements)
- [Installation](#installation)

<!-- /MarkdownTOC -->

## Problem

Windows can display a Windows Security credential prompt when requested by a program, such as a browser with WebAuthn. This allows you to authenticate using a FIDO authenticator, such as a USB security key or a passkey in your computer's TPM protected by a Windows Hello PIN or biometrics, like a fingerprint.

In Windows 10 and 11 prior to 22H2 Moment 4 (September 2023), if the TPM contains the private key needed to authenticate to the relying party (like a website), Windows will prioritize prompting for the user's challenge (like a PIN or fingerprint) for this TPM authenticator first. Windows will still provide an option to choose a different authenticator (like a USB security key) with an additional click. Otherwise, if the TPM does not contain the required secret, Windows will immediately prompt you to insert a USB security key.

<p align="center"><img src=".github/images/usb-prompt.png" alt="usb security key prompt" width="456" /></p> 

In Windows 11 [22H2 Moment 4](https://www.bleepingcomputer.com/news/microsoft/windows-11-moment-4-update-released-here-are-the-many-new-features/) (September 2023) and later (including [23H2](https://www.bleepingcomputer.com/news/microsoft/windows-11-23h2-new-features-in-the-windows-11-2023-update/)), this behavior changed to include the ability to pair with Android and iOS devices over Bluetooth to use their passkeys, which somewhat ameliorates the problem of passkeys not being portable outside their TPM. The behavior is unchanged if the Windows TPM contains the passkey. However, if the local TPM does not contain the passkey, an additional "Sign in with your passkey" step was added before you can use your USB security key.

Now it says "To sign in to “`domain`”, choose a device with a saved passkey," and you have to choose whether you want to use an "iPhone, iPad, or Android device" or a "Security key," and smartphone is the default choice. Choosing the USB security key requires two additional clicks or four additional keystrokes. It is impossible to opt out of this new prompt, even if you disable Bluetooth, don't have an Android or iOS device, or never want to use it for FIDO authentication on your Windows computer. Windows does not remember the most recently used authenticator, either. You could disable your Bluetooth device in Device Manager, but this will also prevent you from using any other Bluetooth peripherals with your computer, such as mice, keyboards, headphones, speakers, and tracker tags.

<p align="center"><img src=".github/images/authenticator-prompt.png" alt="authenticator prompt" width="456" /></p>     

## Solution

This is a background program that runs headlessly in your Windows user session. It waits for Windows FIDO credential provider prompts to appear, then chooses the Security Key option and clicks Next for you automatically. From the user's perspective, the Bluetooth screen barely even appears before it's replaced with the prompt to plug in your USB security key.

<p align="center"><img src=".github/images/demo.gif" alt="demo" width="464" /></p>     

This program does not interfere with local passkey prompts (like requesting your Windows Hello PIN or biometrics). It also does not automatically submit FIDO prompts that contain additional options besides smartphone Bluetooth and USB security keys, such as the case when you previously declined a Windows Hello factor like a PIN, but want to try a PIN again from the authenticator choice dialog.

If this program skips the authenticator choice dialog when you don't want it to, for example, if you want to use a smartphone Bluetooth passkey only once or infrequently, you can hold `Shift` when the dialog appears to temporarily suppress this program from automatically submitting the security key choice once.

Internally, this program uses [Microsoft UI Automation](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-uiautomationoverview) to read and interact with the dialog box.

## Requirements

- Windows 11 23H2 or later, or Windows 11 22H2 with Moment 4 (KB5031455 or KB5030310)
    - It can also run on earlier versions, such as Windows 11 21H2 and Windows 10, although it won't do anything there because the problem is not present on those versions.
- [.NET Desktop Runtime 8 for Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or later

## Installation

1. [Download `AuthenticatorChooser.exe` from the latest release.](https://github.com/Aldaviva/AuthenticatorChooser/releases/latest/download/AuthenticatorChooser.exe)
1. Save the `AuthenticatorChooser.exe` file to a directory of your choice, like `C:\Program Files\AuthenticatorChooser\`.
1. Run the program by double-clicking `AuthenticatorChooser.exe`.
    - Nothing will appear because it's a background program with no UI, but you can tell it's running by searching for `AuthenticatorChooser` in Task Manager.
1. Register the program to run automatically on user logon with one of the following techniques. Be sure to change the example path below if you chose a different installation directory in step 2.
    - Import a `.reg` file
        ```reg
        Windows Registry Editor Version 5.00

        [HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run]
        "AuthenticatorChooser"="\"C:\\Program Files\\AuthenticatorChooser\\AuthenticatorChooser.exe\""
        ```
    - Run a Command Prompt command
        ```bat
        reg add HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v AuthenticatorChooser /d """C:\Program Files\AuthenticatorChooser\AuthenticatorChooser.exe"""
        ```
    - Run a PowerShell cmdlet
        ```ps1
        Set-ItemProperty -Path HKCU:\Software\Microsoft\Windows\CurrentVersion\Run -Name AuthenticatorChooser -Value """C:\Program Files\AuthenticatorChooser\AuthenticatorChooser.exe"""
        ```
    - Use `regedit.exe` interactively to go to the `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` key, and then add a new String value with the Name `AuthenticatorChooser` and the Value `"C:\Program Files\AuthenticatorChooser\AuthenticatorChooser.exe"`.
