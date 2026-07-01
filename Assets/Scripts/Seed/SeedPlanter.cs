using Fusion;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.Tilemaps;

public class SeedPlanter : NetworkBehaviour
{
	[Header("타일맵 연결")]
	[Tooltip("씬의 Grid > Tilemap 오브젝트를 드래그해서 연결하세요.")]
	public Tilemap targetTilemap;

	[Header("타일 에셋 설정")]
	[Tooltip("씨앗을 심을 수 있는 오염 타일 (기준 타일)")]
	public TileBase pollutedTile;

	[Tooltip("씨앗을 심으면 이 타일로 교체됩니다")]
	public TileBase purifiedTile;

	[Header("나무 프리팹")]
	[Tooltip("씨앗을 심으면 생성될 나무/식물 프리팹")]
	public GameObject treePrefab;

	[Header("식재 설정")]
	[Tooltip("식재 모드 활성화 키 (기본: Q)")]
	public KeyCode plantModeKey = KeyCode.Q;

	[Tooltip("정화 반경 (1=해당 칸만, 2=주변 1칸까지)")]
	public int purifyRadius = 1;

	private bool _isPlantMode;

	private TrashCollector _trashCollector;

	private Camera _mainCamera;

	private int _treeCount;

	public int TreeCount => _treeCount;

	private void Awake()
	{
		base.enabled = false;
	}

	private void Start()
	{
		_trashCollector = GetComponent<TrashCollector>();
		_mainCamera = Camera.main;
		if (_trashCollector == null)
		{
			Debug.LogError("[SeedPlanter] ❌ TrashCollector 컴포넌트를 찾을 수 없습니다!");
		}
		if (targetTilemap == null)
		{
			targetTilemap = UnityEngine.Object.FindFirstObjectByType<Tilemap>();
		}
		if (targetTilemap == null)
		{
			Debug.LogError("[SeedPlanter] ❌ targetTilemap이 연결되지 않았습니다!");
		}
	}

	private void Update()
	{
		if (base.HasInputAuthority)
		{
			if (Input.GetKeyDown(plantModeKey))
			{
				TogglePlantMode();
			}
			if (_isPlantMode && Input.GetMouseButtonDown(0))
			{
				TryPlantSeed();
			}
		}
	}

	private void TogglePlantMode()
	{
		if (!_isPlantMode && !HasSeed())
		{
			Debug.Log("[식재] 씨앗이 없습니다! NPC에서 구매하세요.");
			return;
		}
		_isPlantMode = !_isPlantMode;
		if (_isPlantMode)
		{
			Debug.Log($"\ud83c\udf31 [식재 모드 ON] 심을 위치를 클릭하세요. 남은 씨앗: {GetSeedCount()}개");
			Cursor.visible = true;
		}
		else
		{
			Debug.Log("\ud83d\udeab [식재 모드 OFF]");
		}
	}

	private void TryPlantSeed()
	{
		PlayerMovement component = GetComponent<PlayerMovement>();
		if (component != null && !component.CanAct)
		{
			Debug.Log("[식재] 배터리 방전 상태에서는 식재할 수 없습니다!");
			return;
		}
		if (!HasSeed())
		{
			Debug.Log("[식재] 씨앗이 없습니다!");
			_isPlantMode = false;
			return;
		}
		if (targetTilemap == null)
		{
			Debug.LogError("[식재] targetTilemap이 없습니다!");
			return;
		}
		Vector3 mousePosition = Input.mousePosition;
		mousePosition.z = Mathf.Abs(_mainCamera.transform.position.z);
		Vector3 worldPosition = _mainCamera.ScreenToWorldPoint(mousePosition);
		Vector3Int vector3Int = targetTilemap.WorldToCell(worldPosition);
		TileBase tile = targetTilemap.GetTile(vector3Int);
		if (tile == null)
		{
			Debug.Log("[식재] 이 위치에는 타일이 없습니다. 다른 위치를 클릭해주세요.");
			return;
		}
		if (tile == purifiedTile)
		{
			Debug.Log("[식재] 이미 정화된 땅입니다. 오염된 타일에만 심을 수 있습니다.");
			return;
		}
		RPC_PlantSync(vector3Int);
		ConsumeSeed();
		GetComponent<PlayerMovement>()?.DrainBattery(10f);
		_treeCount++;
		Debug.Log($"\ud83c\udf33 식재 완료! 총 {_treeCount}그루 | 남은 씨앗: {GetSeedCount()}개");
		ReportPlantingToServer(vector3Int);
		if (!HasSeed())
		{
			_isPlantMode = false;
			Debug.Log("[식재] 씨앗을 모두 사용했습니다. 식재 모드를 종료합니다.");
		}
	}

	[Rpc(RpcSources.All, RpcTargets.All)]
	private void RPC_PlantSync(Vector3Int cellPos)
	{

		PurifyTiles(cellPos);
		SpawnTree(cellPos);
		GameManager.Instance?.RPC_AddDecorScore(20);
	}

	private void PurifyTiles(Vector3Int centerCell)
	{
		for (int i = -(purifyRadius - 1); i <= purifyRadius - 1; i++)
		{
			for (int j = -(purifyRadius - 1); j <= purifyRadius - 1; j++)
			{
				Vector3Int position = new Vector3Int(centerCell.x + i, centerCell.y + j, centerCell.z);
				TileBase tile = targetTilemap.GetTile(position);
				if (tile != null && tile != purifiedTile)
				{
					targetTilemap.SetTile(position, purifiedTile);
				}
			}
		}
	}

	private void SpawnTree(Vector3Int cellPosition)
	{
		if (treePrefab == null)
		{
			Debug.LogWarning("[식재] treePrefab이 연결되지 않았습니다. Inspector를 확인하세요.");
			return;
		}
		Vector3 cellCenterWorld = targetTilemap.GetCellCenterWorld(cellPosition);
		UnityEngine.Object.Instantiate(treePrefab, cellCenterWorld, Quaternion.identity);
	}

	private void ReportPlantingToServer(Vector3Int cellPosition)
	{
		if (ApiManager.Instance == null)
		{
			Debug.LogWarning("[식재] ApiManager가 없습니다. 서버 기록을 건너뜁니다.");
			return;
		}
		Vector3 cellCenterWorld = targetTilemap.GetCellCenterWorld(cellPosition);
		PlantRequest request = new PlantRequest
		{
			playerId = ApiManager.Instance.playerId,
			posX = cellCenterWorld.x,
			posY = cellCenterWorld.y
		};
		ApiManager.Instance.PlantSeed(request, delegate(PlantResponse response)
		{
			Debug.Log($"[서버 기록] 식재 완료 | 총 나무: {response.treeCount} | {response.message}");
		}, delegate(string error)
		{
			Debug.LogWarning("[서버 기록] 실패 (클라이언트 타일은 유지됨): " + error);
		});
	}

	private bool HasSeed()
	{
		if (_trashCollector != null && _trashCollector.inventory.ContainsKey("Seed"))
		{
			return _trashCollector.inventory["Seed"] > 0;
		}
		return false;
	}

	private int GetSeedCount()
	{
		if (!HasSeed())
		{
			return 0;
		}
		return _trashCollector.inventory["Seed"];
	}

	private void ConsumeSeed()
	{
		if (HasSeed())
		{
			_trashCollector.inventory["Seed"]--;
			if (_trashCollector.inventory["Seed"] <= 0)
			{
				_trashCollector.inventory.Remove("Seed");
			}
			_trashCollector.RefreshUI();
		}
	}

}
