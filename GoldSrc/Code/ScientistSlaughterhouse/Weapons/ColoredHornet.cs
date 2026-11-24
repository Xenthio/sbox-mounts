using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class ColoredHornet : Hornet
{
	[Sync( SyncFlags.FromHost )] internal HornetColor Variant { get; set; }
	GameObject _passedTarget;
	Scientist _attachedScientist;
	bool _latched;
	TimeUntil _detonateAt;

	internal override float TrackingLifetime => Networking.MaxPlayers > 1 ? 3.5f : 15;
	internal override float FlySpeed => Variant switch
	{
		HornetColor.Green => 1200,
		HornetColor.Blue => 1000,
		HornetColor.Yellow => 600,
		HornetColor.Orange => 700,
		HornetColor.LightBlue => 900,
		_ => 800
	};

	internal override Color TrailTint => Variant switch
	{
		HornetColor.Green => new Color32( 0, 255, 0, 128 ),
		HornetColor.Blue => new Color32( 0, 0, 255, 128 ),
		HornetColor.Yellow => new Color32( 255, 255, 0, 128 ),
		HornetColor.Orange => new Color32( 255, 128, 0, 128 ),
		HornetColor.LightBlue => new Color32( 128, 128, 255, 128 ),
		_ => new Color32( 255, 0, 0, 128 )
	};

	internal override string HitTag => Variant switch
	{
		HornetColor.Orange => "slowburn",
		HornetColor.LightBlue => "freeze",
		_ => base.HitTag
	};

	internal override void PrepareLaunch()
	{
		base.PrepareLaunch();
		Red = Variant == HornetColor.Yellow;
	}

	internal override Vector3 AimAt( GameObject target ) =>
		Variant == HornetColor.Yellow && target.GetComponent<Scientist>().IsValid()
			? Npc.TargetEyePosition( target )
			: base.AimAt( target );

	internal override void Guide()
	{
		if ( Variant is not (HornetColor.Green or HornetColor.Blue) )
		{
			base.Guide();
			return;
		}

		if ( Age >= 0.1f ) Velocity = Velocity.Normal * FlySpeed;
	}

	internal override SceneTrace FlightTrace( Vector3 start, Vector3 end )
	{
		var trace = base.FlightTrace( start, end );
		return _passedTarget.IsValid() ? trace.IgnoreGameObjectHierarchy( _passedTarget ) : trace;
	}

	internal override bool Bounce( SceneTraceResult hit )
	{
		if ( Variant == HornetColor.Green ) return false;
		if ( Variant != HornetColor.Blue ) return base.Bounce( hit );
		if ( Age < 0.1f || hit.GameObject?.GetComponentInParent<IDamageable>() is not null ) return false;
		var direction = (Velocity - 2 * Velocity.Dot( hit.Normal ) * hit.Normal).Normal;
		WorldPosition = hit.EndPosition + direction;
		Velocity = direction * FlySpeed;
		WorldRotation = Rotation.LookAt( direction );
		return true;
	}

	internal override void HandleContact( SceneTraceResult hit )
	{
		var victim = hit.GameObject?.GetComponentInParent<IDamageable>();
		if ( Age >= 0.1f && Variant == HornetColor.Green && victim is Component { IsValid: true } actor )
		{
			OnImpact( victim, hit.EndPosition, hit.Normal );
			_passedTarget = actor.GameObject;
			WorldPosition = hit.EndPosition + Velocity.Normal * 0.5f;
			return;
		}

		if ( Age >= 0.1f && Variant == HornetColor.Yellow && victim is Scientist { Health: > 0 } scientist )
		{
			_attachedScientist = scientist;
			_latched = true;
			_detonateAt = 2;
			Velocity = Vector3.Zero;
			WorldPosition = Npc.TargetEyePosition( scientist.GameObject );
			PlaySound( "hornet/ag_hornethit", WorldPosition );
			return;
		}

		base.HandleContact( hit );
	}

	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal )
	{
		if ( Variant is not (HornetColor.Orange or HornetColor.LightBlue) || victim is null )
		{
			base.OnImpact( victim, position, normal );
			return;
		}

		var damage = new DamageInfo { Damage = Damage, Attacker = Shooter, Weapon = GameObject, Origin = WorldPosition, Position = position, Normal = normal };
		damage.Tags.Add( HitTag );
		damage.Tags.Add( "nevergib" );
		victim.OnDamage( damage );
		PlaySound( "hornet/ag_hornethit", position );
	}

	protected override void OnFixedUpdate()
	{
		if ( !_latched )
		{
			base.OnFixedUpdate();
			return;
		}
		if ( !Networking.IsHost ) return;
		if ( !_attachedScientist.IsValid() || _attachedScientist.Health <= 0 )
		{
			GameObject.Destroy();
			return;
		}
		WorldPosition = Npc.TargetEyePosition( _attachedScientist.GameObject );
		if ( _detonateAt > 0 ) return;

		var model = _attachedScientist.Renderer?.Model;
		// The mod adds four stump choices after the original four heads.
		if ( model.IsValid() && model.Parts.All.Any( part => part.Name == "heads" && part.Choices.Count == 10 ) )
		{
			if ( _attachedScientist.Body >= 4 )
			{
				GameObject.Destroy();
				return;
			}
			_attachedScientist.Body += 4;
			_attachedScientist.ApplyAppearance( model );
		}
		var damage = new DamageInfo { Damage = _attachedScientist.Health, Attacker = Shooter, Weapon = GameObject, Origin = WorldPosition, Position = WorldPosition };
		damage.Tags.Add( "nevergib" );
		((IDamageable)_attachedScientist).OnDamage( damage );
		BloodEffects.Spray( Npc.BloodColor.Red, WorldPosition, 250, "ssh" );
		GameObject.Destroy();
	}
}
