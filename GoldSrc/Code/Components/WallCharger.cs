namespace GoldSrc.Maps;

public sealed class WallCharger : MapEntity, Component.IPressable
{
	[Property] public bool Armour { get; set; }
	[Property] public bool RechargesMedkit { get; set; }
	[Property] public SoundEvent StartSound { get; set; }
	[Property] public SoundEvent LoopSound { get; set; }
	[Property] public SoundEvent DenySound { get; set; }
	[Property] public int Juice { get; set; } = -1;

	private TimeUntil _nextCharge, _nextSound, _off;
	private int _stage;
	private SoundHandle _loop;

	protected override void OnStart()
	{
		if ( Juice < 0 ) Juice = SuitItem.Skill switch { <= 1 => Armour ? 75 : 50, 2 => Armour ? 50 : 40, _ => Armour ? 35 : 25 };
	}

	bool IPressable.Press( IPressable.Event e ) => UseBy( e.Source.GameObject );
	bool IPressable.Pressing( IPressable.Event e ) => UseBy( e.Source.GameObject );
	public override void Trigger( GameObject activator = null ) => UseBy( activator );
	public override void TurnOff( GameObject activator = null ) => UseBy( activator );

	private bool UseBy( GameObject activator )
	{
		if ( !Networking.IsHost || !activator.IsValid() ) return false;
		var player = activator.GetComponentInParent<PlayerController>();
		if ( !player.IsValid() || MapState.PlayerVitals( player ) is not {} vitals ) return false;
		if ( Juice == 0 || !player.GetComponent<HevSuit>().IsValid() )
		{
			if ( Juice == 0 ) Stop();
			if ( _nextSound <= 0 )
			{
				if ( DenySound is not null ) Sound.Play( DenySound, WorldPosition );
				_nextSound = 0.62f;
			}
			return true;
		}

		_off = 0.25f;
		if ( _nextCharge > 0 ) return true;
		_nextCharge = RechargesMedkit ? 1f / 15 : 0.1f;
		if ( _stage == 0 )
		{
			if ( StartSound is not null ) Sound.Play( StartSound, WorldPosition );
			_nextSound = 0.56f;
			_stage = 1;
		}
		else if ( _stage == 1 && _nextSound <= 0 )
		{
			if ( LoopSound is not null ) _loop = Sound.Play( LoopSound, WorldPosition );
			_stage = 2;
		}

		if ( !Armour && vitals.Health >= vitals.MaxHealth )
		{
			if ( RechargesMedkit && vitals.Health > 0 )
				player.GetComponent<SvenCoop.MedkitRecharge>()?.SupplyCharge( 1 );
			return true;
		}
		if ( Armour ? vitals.Armour >= 100 : vitals.Health <= 0 ) return true;
		if ( Armour ) vitals.Armour = System.Math.Min( 100, vitals.Armour + 1 );
		else vitals.Health = System.Math.Min( vitals.MaxHealth, vitals.Health + 1 );
		if ( --Juice == 0 ) GetComponent<BrushAppearance>().Frame = 1;
		return true;
	}

	protected override void OnFixedUpdate()
	{
		if ( _stage != 0 && _off <= 0 ) Stop();
	}

	private void Stop()
	{
		_loop?.Stop();
		_stage = 0;
	}
	protected override void OnDisabled() => Stop();
}
