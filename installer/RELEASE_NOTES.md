## Drag Tree 1.0.4

- Fixed a misleading result display where a slightly early launch could round up to a perfect-looking 0.500 (Sportsman) or 0.400 (Pro) while correctly showing a red light.
- Early results that would round to the perfect value now display <0.500 or <0.400 in the large result and recent history.
- Result details now say target RT rather than perfect RT, making the target distinct from the actual launch result.
- Red-light decisions continue using the full, unrounded release time. Added boundary checks for both tree types.

**Download:** Use `DragTree-1.0.4-win-x64-Setup.exe` for installation, or `DragTree-1.0.4-win-x64.zip` for portable use. Both packages include .NET. The installer upgrades earlier versions and preserves racer key preferences.

**Requirements:** Windows 10 22H2 or Windows 11, x64. Packages are unsigned. Download checksums are provided in `SHA256SUMS.txt`.

This is a practice tool. Display, operating-system, and input latency affect measurements; the application is not calibrated track timing equipment.
