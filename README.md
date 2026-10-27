# Razer Kraken V4 Auto Switch

**한국어 | English** (병기 / bilingual)

Razer Kraken V4 (무선 USB 동글 모델) 사용자를 위한 Windows 트레이 프로그램입니다.
**동글은 항상 꽂아 두고 헤드폰만 껐다 켤 때**, 헤드폰 전원에 맞춰 기본 오디오 출력 장치를 자동으로 바꿔 줍니다.

A tiny Windows tray app for the **Razer Kraken V4** (wireless USB dongle model).
When the dongle stays plugged in and you only power the headset on/off, it automatically switches the default Windows audio output to match the headset's power state.

| 헤드폰 / Headset | 동작 / Action |
|---|---|
| ON  | 재생 장치 → `Razer Kraken V4 - Game`, 통신 장치 → `Razer Kraken V4 - Chat` (직전 기본 장치는 저장)<br>Playback → `Razer Kraken V4 - Game`, communications → `Razer Kraken V4 - Chat` (previous defaults are remembered) |
| OFF | 저장해 둔 직전 장치(스피커, 모니터 등)로 복원<br>Restores the previously remembered devices (speakers, monitor, etc.) |

## 원리 / How it works

헤드폰이 꺼져도 동글의 오디오 장치는 Windows에서 계속 활성 상태라 장치 목록으로는 on/off를 알 수 없습니다.
대신 동글(`VID 1532 / PID 056C`)의 벤더 HID 인터페이스(Usage Page `0xFF14`)로 들어오는 이벤트를 읽습니다.

Even with the headset off, the dongle's audio endpoints stay active in Windows, so the device list can't tell you the power state.
Instead, the app reads events from the dongle's vendor HID interface (`VID 1532 / PID 056C`, Usage Page `0xFF14`):

```
02 0A 60 .. .. .. 05 00 80 80 20 02 01 <state>      state: 01 = on, 00 = off
```

오디오 전환은 Core Audio + `IPolicyConfigVista`(비공개 COM 인터페이스, SoundSwitch 등 다수 도구가 사용)로 합니다.
Synapse가 실행 중이어도 HID를 공유 모드로 읽기 때문에 충돌하지 않습니다.

Switching uses Core Audio plus `IPolicyConfigVista` (an undocumented COM interface also used by tools such as SoundSwitch).
HID is opened in shared mode, so it coexists with Razer Synapse.

## 사용법 / Usage

1. `build.bat` 실행 (Windows 기본 .NET Framework 컴파일러 사용, 별도 설치 불필요)
   Run `build.bat` (uses the .NET Framework compiler built into Windows; nothing to install).
2. `KrakenAutoSwitch.exe` 실행 → 트레이 아이콘 (초록=켜짐, 회색=꺼짐, 주황=아직 모름)
   Run `KrakenAutoSwitch.exe` → tray icon (green = on, gray = off, orange = not known yet).
3. 트레이 우클릭 메뉴에서 "Windows 시작 시 자동 실행" 체크
   Right-click the tray icon and enable "Windows 시작 시 자동 실행" (start with Windows).

첫 실행 시 `KrakenAutoSwitch.ini`가 생성됩니다. / A `KrakenAutoSwitch.ini` is created on first run.

```ini
game=Razer Kraken V4 - Game   ; device name, substring match / 장치 이름 '포함' 일치
chat=Razer Kraken V4 - Chat
fallback=                     ; device to restore if nothing was remembered / 저장된 장치가 없을 때 복원할 장치
```

## 한계 / Limitations

- 프로그램 시작 시점의 헤드폰 상태는 알 수 없고, 다음 on/off 이벤트부터 맞춰집니다 (트레이 메뉴에서 수동 전환 가능).
  The headset state at app start is unknown; it syncs on the next on/off event (manual switch is available from the tray menu).
- Kraken V4 + 동글(PID `056C`)에서만 확인했습니다. 다른 모델은 `tools/HidDiag.cs`(HID 리포트 덤프 도구)로 이벤트를 직접 확인해야 합니다.
  Tested only with the Kraken V4 + dongle (PID `056C`). For other models, use `tools/HidDiag.cs` (HID report dumper) to find the equivalent event.
- 비공식 프로젝트이며 Razer와 무관합니다. / Unofficial project, not affiliated with Razer.

## License

MIT
