using Photon.Pun;

public class ItemActionBase : MonoBehaviourPun
{
	protected Item item;

	protected Character character => item.holderCharacter;

	public virtual void RunAction()
	{
	}

	protected virtual void OnEnable()
	{
		Init();
		Subscribe();
	}

	protected virtual void Start()
	{
		Unsubscribe();
		Subscribe();
	}

	protected virtual void OnDisable()
	{
		Unsubscribe();
	}

	protected virtual void Subscribe()
	{
	}

	protected virtual void Unsubscribe()
	{
	}

	private void Init()
	{
		item = GetComponent<Item>();
	}
}
