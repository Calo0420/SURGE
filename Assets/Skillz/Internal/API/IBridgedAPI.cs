namespace SkillzSDK.Internal.API
{
	/// <summary>
	/// Consolidated APIs that are a bridge to a platform specific implementation
	/// (i.e., Android, iOS, etc).
	/// </summary>
	internal interface IBridgedAPI : IAsyncAPI
	{
		void Initialize(int gameID, Environment environment, Orientation orientation);

		// Add this as an optional method
		void InitializeSimulatedMatch(string matchInfoJson, int randomSeed);
	}
}