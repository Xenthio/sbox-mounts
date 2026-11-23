namespace GoldSrc;

/// <summary>GoldSrc mods whose content is installed beside Half-Life's valve directory.</summary>
public abstract class HalfLifeModMount : GameMount
{
	public override long AppId => 70;
	public override IReadOnlyList<string> GameDirs => [Ident, "valve"];

	protected override void Initialize( InitializeContext context )
	{
		base.Initialize( context );
		IsInstalled = FileSystem?.DirectoryExists( Ident ) == true;
	}
}

public sealed class ScientistSlaughterhouseMount : HalfLifeModMount
{
	public override string Ident => "ssh";
	public override string Title => "Scientist Slaughterhouse";
}

public sealed class CrackLifeMount : HalfLifeModMount
{
	public override string Ident => "cracklife";
	public override string Title => "Crack-Life";
}

public sealed class CrackLifeCampaignMount : HalfLifeModMount
{
	public override string Ident => "clcampaign";
	public override string Title => "Crack-Life: Campaign Mode";
}
