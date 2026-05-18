using Fusion;
using UnityEngine;

// Photon이 클라이언트 입력을 Host로 전달할 때 사용하는 구조체
public struct NetworkInputData : INetworkInput
{
    public Vector2 direction; // 좌우 이동 (x만 사용, y는 중력)
    public NetworkBool jump;  // ↑키 점프
    public NetworkBool interact; // E키 상호작용
    public NetworkBool teleport; // ↓키 텔레포트
}