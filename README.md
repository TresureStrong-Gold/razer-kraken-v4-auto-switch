# Kraken Auto Switch

Razer Kraken V4 (무선 동글 모델) 사용자를 위한 Windows 트레이 프로그램.
**동글은 항상 꽂아 두고 헤드폰만 껐다 켤 때**, 헤드폰 전원에 맞춰 기본 오디오 출력 장치를 자동으로 바꿔 줍니다.

| 헤드폰 | 동작 |
|---|---|
| ON  | 재생 장치 → `Razer Kraken V4 - Game`, 통신 장치 → `Razer Kraken V4 - Chat` (직전 기본 장치는 저장) |
| OFF | 저장해 둔 직전 장치(스피커, 모니터 등)로 복원 |

*English: a tiny Windows tray app that switches the default playback device when the Razer Kraken V4 headset powers on/off while its USB dongle stays plugged in.*

## 원리
헤드폰이 꺼져도 동글의 오디오 장치는 Windows에서 계속 활성 상태라 장치 목록으로는 on/off를 알 수 없습니다.
대신 동글(`VID 1532 / PID 056C`)의 벤더 HID 인터페이스(Usage Page `0xFF14`)로 들어오는 이벤트를 읽습니다.

```
02 0A 60 .. .. .. 05 00 80 80 20 02 01 <상태>      상태: 01 = 켜짐, 00 = 꺼짐
```

오디오 전환은 Core Audio + `IPolicyConfigVista`(비공개 COM 인터페이스, SoundSwitch 등 다수 도구가 사용)로 합니다.
Synapse가 실행 중이어도 HID를 공유 모드로 읽기 때문에 충돌하지 않습니다.

## 사용법
1. `build.bat` 실행 (Windows 기본 .NET Framework 컴파일러 사용, 별도 설치 불필요)
2. `KrakenAutoSwitch.exe` 실행 → 트레이 아이콘 (초록=켜짐, 회색=꺼짐, 주황=아직 모름)
3. 트레이 우클릭 메뉴에서 "Windows 시작 시 자동 실행" 체크

첫 실행 시 `KrakenAutoSwitch.ini`가 생성됩니다.

```ini
game=Razer Kraken V4 - Game   ; 장치 이름 '포함' 일치
chat=Razer Kraken V4 - Chat
fallback=                     ; 저장된 직전 장치가 없을 때 복원할 장치
```

## 한계
- 프로그램 시작 시점의 헤드폰 상태는 알 수 없고, 다음 on/off 이벤트부터 맞춰집니다 (트레이 메뉴에서 수동 전환 가능).
- Kraken V4 + 동글(PID `056C`)에서만 확인했습니다. 다른 모델은 `tools/HidDiag.cs`(HID 리포트 덤프 도구)로 이벤트를 직접 확인해야 합니다.
- 비공식 프로젝트이며 Razer와 무관합니다.

## License
MIT
