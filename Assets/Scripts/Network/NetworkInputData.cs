using Fusion;
using UnityEngine;

// Photon이 클라이언트 입력을 Host로 전달할 때 사용하는 구조체
public struct NetworkInputData : INetworkInput
{
    public Vector2 direction;
}