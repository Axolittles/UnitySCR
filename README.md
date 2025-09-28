Generates a valid Screensaver file for Windows 10/11
- Tested on Windows 10 (IoT LTSC 10.0.19044) and Unity 6000.2.2f1.

<img width="330" height="410" alt="image" src="https://github.com/user-attachments/assets/7776085a-45c4-4fea-a536-bd087f06d9fc" />

- Whatever is happening in the Screensaver scene will be drawn while the Screensaver is active.
- Any mouse or keyboard input will exit the screensaver.
- Supports multi-monitor setups.
- Includes a small demo scene of an island and a small config menu for demonstration purposes.

Usage:
  - Download and load the project
  - Build for Windows
  - Output Folder > Right-click `UnitySCR.scr` > `Install`

Notes:
  - For testing, both `Build and Run` and `Play Mode` work as intended or you can Right-click `UnitySCR.scr` -> `Test`
  - Right-click `UnitySCR.scr` > `Install` causes Windows to update the Registry with the given path, but doesn't actually 'install' anything, so only need to 'install' once, even while developing.
  - Might be some bugginess with the `Preview` window in the Screensaver settings since we're tricking Unity into drawing as a subwindow. Shouldn't be an issue with normal usage.

<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/5e461078-a7fe-4573-b84e-e280e25bc87f" />

<img width="512" height="384" alt="image" src="https://github.com/user-attachments/assets/f3935367-1d29-4aca-be9e-813081b7db60" />
