namespace MagmaFlow.Framework.Publishing
{
	public interface ITopic
	{
		string Name { get; }
		bool ClearOnSceneChange { get; }
	}
}
