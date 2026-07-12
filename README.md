# 🎮 Crazy Arcade Multiplayer

<img width="400" height="336" alt="image" src="https://github.com/user-attachments/assets/29d9fbc1-6b6f-46b5-af2c-753106140c58" />


[![Unity](https://img.shields.io/badge/Unity-100000?style=flat&logo=unity&logoColor=white)](#)
[![C#](https://img.shields.io/badge/C%23-239120?style=flat&logo=c-sharp&logoColor=white)](#)
[![TCP Socket](https://img.shields.io/badge/TCP-Socket-blue)](#)
[![JSON](https://img.shields.io/badge/JSON-000000?style=flat&logo=json&logoColor=white)](#)

---

## 📋 프로젝트 개요

TCP Socket 기반 실시간 멀티플레이 서버/클라이언트 프로젝트

| 항목 | 내용 |
|---|---|
| 기간 | 2026.01 ~ 2026.02 (6주) |
| 인원 | 1인 개발 |
| 플랫폼 | PC |
| 기술 | Unity, C#, TCP Socket, JSON |
| 역할 | 클라이언트 및 서버 전체 구현 |

---

## 🎮 구현 기능

- **타일 기반 이동 시스템** : 타일 좌표, 월드 좌표 변환, 대각선 이동 방지, 부드러운 이동 처리
- **물풍선·폭발 시스템** : 십자 방향 전파, 벽/블록 충돌 차단, 연쇄 폭발
- **멀티플레이 동기화** : TCP 소켓 기반 서버, 플레이어,블록 상태 동기화
- **랜덤 아이템 시스템** : 서버 기반 아이템 생성·배포, 효과 적용 및 UI 갱신

---

## 2. 🛠️ 기술 구현 포인트

### 📊 핵심 지표 요약

| 항목 | 수치 |
|---|---|
| 동시 접속 | 8명 |
| 평균 RTT | 3~12ms |
| 최대 RTT | 17ms |
| 최대 처리량 | 112 pkt/s |

### Server-Authoritative 구조

- 서버가 블록 파괴, 아이템 획득, 갇힘 상태를 최종 판정
- 클라이언트는 렌더링 및 입력만 담당

### TCP Packet Framing

- 4byte 길이 헤더 기반 패킷 경계 처리
- 헤더와 페이로드를 단일 버퍼로 합쳐 전송해 비동기 환경에서의 순서 뒤섞임 방지

### 동시성 처리

- ConcurrentDictionary 기반 상태 관리
- async/await 기반 비동기 처리 구조 구현

---

## 3. 문제 해결

### 이슈 1) TCP 패킷 경계 미처리로 인한 RTT 스파이크 발생

**문제**

멀티플레이 테스트 중 JSON 파싱 오류와 함께 RTT가 순간적으로 최대 617ms까지 증가하는 문제가 발생했습니다.
<img width="1200" height="404" alt="image" src="https://github.com/user-attachments/assets/477b655b-5298-4682-bf60-c363f4384f66" />


**원인**

TCP는 스트림 기반 프로토콜이기 때문에 여러 패킷이 하나의 버퍼에 합쳐져 수신될 수 있었지만, 서버는 수신 데이터를 단일 패킷으로 가정하고 처리하고 있었습니다.

```json
{"Type":10}{"Type":11}
```

**해결**

패킷 앞에 4byte 길이 헤더를 추가하여 수신 측에서 패킷 길이만큼 정확히 읽도록 프로토콜 구조를 개선했습니다. 또한 서버 측 Throughput 및 RTT 측정 로직을 추가하여 병목 구간을 직접 분석했습니다.

> 8명 동시 접속 초기 구간에서 최대 RTT 17ms가 발생했으나, 플레이 진행 중에는 대부분 1~12ms 범위로 유지되었습니다.

```csharp
// 수신측: 4byte 헤더로 길이 읽기 → 정확한 길이만큼 페이로드 수신 → 처리
byte[] lengthBuffer = new byte[4];
while (connection.IsConnected)
{
    await stream.ReadAsync(lengthBuffer, 0, 4);
    int len = BitConverter.ToInt32(lengthBuffer, 0); // 길이 확인

    byte[] buf = new byte[len];
    while (totalRead < len) // 정확히 len만큼 수신
        totalRead += await stream.ReadAsync(buf, totalRead, len - totalRead);

    await ProcessPacket(connection, buf); // 처리
}
```

**추가 개선**

길이 헤더 적용 후에도 폭발/블록 파괴처럼 여러 패킷이 연속 전송되는 구간에서 일부 동기화 순서 문제가 발생했습니다.

원인은 길이 헤더와 payload를 두 번의 `WriteAsync`로 나눠 전송하면서, 비동기 전송이 겹칠 경우 전송 단위가 섞일 가능성이 있었기 때문입니다.

이를 해결하기 위해 길이 헤더와 payload를 하나의 `sendBuffer`로 합친 뒤 한 번의 `WriteAsync`로 전송하도록 수정했습니다.

**결과**

- 패킷 경계 처리 안정화
- 폭발/블록 파괴/아이템 생성 패킷 순서 안정화
- 8명 동시 접속 환경에서 최대 RTT 17ms, 대부분 1~12ms 유지
- 최대 송신 처리량 112 packets/s 측정

---

### 이슈 2) 서버-클라이언트 블록 상태 불일치 문제

**문제**

플레이어가 블록을 파괴해도 다른 플레이어 화면에서는 블록이 제거되지 않는 문제가 발생했습니다.

**원인**

각 클라이언트가 블록 상태를 개별적으로 처리하면서 게임 상태가 서로 달라졌습니다.

**해결**

서버 권한(Server Authoritative) 구조로 변경하여 서버가 블록 파괴를 최종 판정하고 이러한 정보를 담은 `BlockDestroyPacket`을 모든 클라이언트에 브로드캐스트하도록 수정했습니다.

**결과**

- 클라이언트 간 블록 상태 일관성 확보
- 멀티플레이 환경에서 동기화 안정성 개선

---

## 6. 회고

- 네트워크 구조와 동기화 문제를 직접 분석하며 해결할 수 있었습니다.
- 서버 권한 구조와 상태 기반 로직을 적용하며 멀티플레이 구조를 학습할 수 있었습니다.
