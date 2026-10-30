using Photon.Pun;
using Portningsbolaget;
using UnityEngine;
using Zorro.Core;
using Zorro.Settings;

public class Dynamite : ItemComponent
{
	private bool _hasExploded;

	private TrackableNetworkObject trackable;

	public TrackNetworkedObject smokeVFXPrefab;

	public GameObject explosionPrefab;

	public float startingFuseTime;

	public float lightFuseRadius;

	[SerializeField]
	private float fuseTime;

	public Transform sparks;

	public Transform sparksPhotosensitive;

	private PhotosensitiveSetting setting;

	public bool DEBUG_PauseOnExplode;

	private Vector3 dynamitePosition;

	private HashGridObject hashGridObject;

	public override void Awake()
	{
		base.Awake();
		hashGridObject = GetComponent<HashGridObject>();
		trackable = GetComponent<TrackableNetworkObject>();
		item.UIData.canPocket = false;
		lightFuseRadius *= lightFuseRadius;
		dynamitePosition = transform.position;
	}

	private void Start()
	{
		setting = GameHandler.Instance.SettingsHandler.GetSetting<PhotosensitiveSetting>();
	}

	public override void OnInstanceDataSet()
	{
		fuseTime = GetData(DataEntryKey.Fuel, SetupDefaultFuel).Value;
	}

	private void Update()
	{
		if (!GetData<BoolItemData>(DataEntryKey.FlareActive).Value)
		{
			TestLightWick();
			return;
		}
		if (!trackable.hasTracker)
		{
			EnableFlareVisuals();
		}
		if (setting.Value != OffOnMode.ON)
		{
			sparks.gameObject.SetActive(value: true);
		}
		else
		{
			sparksPhotosensitive.gameObject.SetActive(value: true);
		}
		fuseTime = GetData(DataEntryKey.Fuel, SetupDefaultFuel).Value;
		item.SetUseRemainingPercentage(fuseTime / startingFuseTime);
		if (!photonView.IsMine)
		{
			return;
		}
		fuseTime -= Time.deltaTime;
		if (fuseTime <= 0f)
		{
			_ = _hasExploded;
			if (Character.localCharacter.data.currentItem == item)
			{
				Character.localCharacter.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.25f);
				Player.localPlayer.EmptySlot(Character.localCharacter.refs.items.currentSelectedSlot);
				Character.localCharacter.refs.afflictions.UpdateWeight();
			}
			photonView.RPC("RPC_Explode", RpcTarget.All);
			PhotonNetwork.Destroy(gameObject);
			item.ClearDataFromBackpack();
			fuseTime = 0f;
		}
		GetData(DataEntryKey.Fuel, SetupDefaultFuel).Value = fuseTime;
	}

	[PunRPC]
	private void RPC_Explode()
	{
		if (DEBUG_PauseOnExplode)
		{
			Debug.Break();
		}
		Object.Instantiate(explosionPrefab, transform.position, transform.rotation);
		gameObject.SetActive(value: false);
		_hasExploded = true;
	}

	private void TestLightWick()
	{
		if (!GetData<BoolItemData>(DataEntryKey.FlareActive).Value && Vector3.SqrMagnitude(Character.localCharacter.Center - dynamitePosition) < lightFuseRadius)
		{
			LightFlare();
		}
	}

	private FloatItemData SetupDefaultFuel()
	{
		return new FloatItemData
		{
			Value = startingFuseTime
		};
	}

	[PunRPC]
	public void TriggerHelicopter()
	{
		Singleton<PeakHandler>.Instance.SummonHelicopter();
	}

	public void LightFlare()
	{
		GetComponent<PhotonView>().RPC("SetFlareLitRPC", RpcTarget.All);
	}

	[PunRPC]
	public void SetFlareLitRPC()
	{
		hashGridObject.RemoveFromGrid();
		GetData<BoolItemData>(DataEntryKey.FlareActive).Value = true;
	}

	public void EnableFlareVisuals()
	{
		Object.Instantiate(smokeVFXPrefab, transform.position, transform.rotation).GetComponent<TrackNetworkedObject>().SetObject(trackable);
	}
}
