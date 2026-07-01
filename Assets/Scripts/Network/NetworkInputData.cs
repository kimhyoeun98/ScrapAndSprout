using System.Runtime.InteropServices;
using Fusion;
using UnityEngine;

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct NetworkInputData : INetworkInput
{
	[FieldOffset(0)]
	public Vector2 direction;

	[FieldOffset(8)]
	public NetworkBool interact;

	[FieldOffset(12)]
	public NetworkBool teleport;
}
